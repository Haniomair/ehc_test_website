using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace EHC.Web.Composers;

/// <summary>
/// Builds the health library showcase once: the Conditions, Tests and procedures and Healthy living sections under the
/// Health library page (its existing articles move into Healthy living), the sample conditions and tests
/// (HealthLibraryContent) linked to each other, to specialties, facilities and health tools, and the editorial policy
/// page; everything published. Pages show "Awaiting clinical review" until a clinician sets a review date.
/// The page types come from uSync: when they are not imported yet (first start of a development site, where the import
/// runs after start-up), it tries again for two minutes.
/// </summary>
public sealed partial class HealthLibrarySeeder(IContentService contents, IContentTypeService contentTypes, IKeyValueService keyValues, IRuntimeState runtime, ILogger<HealthLibrarySeeder> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private const string Ar = BlockJson.Ar, En = BlockJson.En;
    private const string DoneKey = "Ehc.HealthLibrary.Seeded";
    private const string DedupedKey = "Ehc.HealthLibrary.Deduped";

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        RemoveDuplicates();
        if (keyValues.GetValue(DoneKey) is not null) return;
        if (TryRun()) return;
        _ = Task.Run(async () =>
        {
            for (var i = 0; i < 24; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                if (TryRun()) return;
            }
            logger.LogWarning("Health library not seeded: its page types are missing (import uSync Settings, then restart)");
        });
    }

    /// <summary>False while the page types are missing; true when done (or nothing to do).</summary>
    private bool TryRun()
    {
        try
        {
            if (new[] { "healthLibrary", "healthLibrarySection", "healthCondition", "healthTest", "contentPage" }.Any(a => contentTypes.Get(a) is null)) return false;
            if (contentTypes.Get("healthLibrarySection")!.PropertyTypes.All(p => p.Alias != "sectionKind")) return false;
            Run();
            keyValues.SetValue(DoneKey, DateTime.UtcNow.ToString("O"));
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Seeding the health library failed");
            keyValues.SetValue(DoneKey, "failed");
            return true;
        }
    }

    private void Run()
    {
        var libraryType = contentTypes.Get("healthLibrary")!;
        var library = contents.GetPagedOfTypes([libraryType.Id], 0, 10, out _, null, null).FirstOrDefault(l => !l.Trashed);
        if (library is null) { logger.LogInformation("Health library not seeded: there is no Health library page"); return; }

        // sections (reuse any that exist)
        var sectionType = contentTypes.Get("healthLibrarySection")!;
        var childTypes = new[] { "healthLibrarySection", "healthArticle", "contentPage" }.Select(a => contentTypes.Get(a)?.Id).OfType<int>().ToArray();
        var existing = contents.GetPagedOfTypes(childTypes, 0, 2000, out _, null, null).Where(c => c.ParentId == library.Id && !c.Trashed).ToList();
        var sections = new Dictionary<string, IContent>();
        foreach (var (slug, name, intro) in HealthLibraryContent.Sections)
        {
            var section = existing.FirstOrDefault(c => c.ContentTypeId == sectionType.Id && (c.GetValue<string>("sectionKind") ?? "").Contains($"\"{slug}\""));
            if (section is null)
            {
                section = contents.Create(name.Ar, library.Key, "healthLibrarySection");
                section.SetCultureName(name.Ar, Ar);
                section.SetCultureName(name.En, En);
                section.SetValue("sectionKind", JsonSerializer.Serialize(new[] { slug }));
                section.SetValue("intro", intro.Ar, Ar);
                section.SetValue("intro", intro.En, En);
                contents.Save(section);
                Publish(section);
            }
            sections[slug] = section;
        }

        // the library's existing articles become Healthy living
        var articleType = contentTypes.Get("healthArticle");
        foreach (var article in existing.Where(c => articleType is not null && c.ContentTypeId == articleType.Id))
        {
            contents.Move(article, sections["living"].Id);
        }

        // conditions and tests: create, then link (every page needs its key first)
        var pages = new Dictionary<string, IContent>();
        var itemTypes = new[] { "healthCondition", "healthTest" }.Select(a => contentTypes.Get(a)!.Id).ToArray();
        var present = contents.GetPagedOfTypes(itemTypes, 0, 5000, out _, null, null)
            .Where(c => !c.Trashed && sections.Values.Any(s => s.Id == c.ParentId))
            .Select(c => c.GetCultureName(En)).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in HealthLibraryContent.Items)
        {
            if (present.Contains(item.Name.En)) continue;   // never twice, whatever the flag says
            var page = contents.Create(item.Name.Ar, sections[item.IsTest ? "tests" : "conditions"].Key, item.IsTest ? "healthTest" : "healthCondition");
            page.SetCultureName(item.Name.Ar, Ar);
            page.SetCultureName(item.Name.En, En);
            Text(page, "intro", item.Intro);
            Text(page, "alsoKnownAs", item.Aka);
            Text(page, "reviewedBy", HealthLibraryContent.Reviewer);
            page.SetValue("bodySystems", JsonSerializer.Serialize(item.Systems));
            if (!item.IsTest)
            {
                if (item.Audiences is { Length: > 0 } a) page.SetValue("audiences", JsonSerializer.Serialize(a));
                page.SetValue("featured", item.Featured);
                if (item.Emergency is { } e) Text(page, "emergencyWhen", e);
                if (item.Urgent is { } u) Text(page, "urgentWhen", u);
                if (item.Primary is { } p) Text(page, "primaryWhen", p);
            }
            foreach (var (alias, text) in item.Sections)
            {
                page.SetValue(alias, BlockField.RichValue(HealthLibraryContent.Html(text.Ar)), Ar);
                page.SetValue(alias, BlockField.RichValue(HealthLibraryContent.Html(text.En)), En);
            }
            if (item.Sources is { Length: > 0 } sources)
            {
                page.SetValue("sources", Links(sources.Select(s => new SeedLink(s.Name.Ar, s.UrlAr ?? s.UrlEn, NewWindow: true))), Ar);
                page.SetValue("sources", Links(sources.Select(s => new SeedLink(s.Name.En, s.UrlEn, NewWindow: true))), En);
            }
            contents.Save(page);
            pages[item.Slug] = page;
        }

        var specialties = ByEnglishName("specialty");
        var facilities = ByEnglishName("facility");
        var tools = contentTypes.Get("healthTool") is { } toolType
            ? contents.GetPagedOfTypes([toolType.Id], 0, 200, out _, null, null).Where(t => !t.Trashed).ToList() : [];
        foreach (var item in HealthLibraryContent.Items.Where(i => pages.ContainsKey(i.Slug)))
        {
            var page = pages[item.Slug];
            Pick(page, "specialties", (item.Specialties ?? []).Select(n => specialties.GetValueOrDefault(n)));
            Pick(page, "careFacilities", (item.Facilities ?? []).Select(n => facilities.GetValueOrDefault(n)));
            Pick(page, item.IsTest ? "relatedConditions" : "relatedTests", (item.Related ?? []).Select(s => pages.GetValueOrDefault(s)));
            if (!item.IsTest)
            {
                Pick(page, "relatedTools", (item.Tools ?? []).Select(k => tools.FirstOrDefault(t => (t.GetValue<string>("tool") ?? "").Contains($"\"{k}\""))));
            }
            contents.Save(page);
            Publish(page);
        }

        // editorial policy
        var contentPageType = contentTypes.Get("contentPage")!;
        if (!existing.Any(c => c.ContentTypeId == contentPageType.Id && c.GetCultureName(En) == HealthLibraryContent.PolicyName.En))
        {
            var policy = contents.Create(HealthLibraryContent.PolicyName.Ar, library.Key, "contentPage");
            policy.SetCultureName(HealthLibraryContent.PolicyName.Ar, Ar);
            policy.SetCultureName(HealthLibraryContent.PolicyName.En, En);
            Text(policy, "intro", HealthLibraryContent.PolicyIntro);
            policy.SetValue("body", BlockField.RichValue(HealthLibraryContent.PolicyBody.Ar), Ar);
            policy.SetValue("body", BlockField.RichValue(HealthLibraryContent.PolicyBody.En), En);
            contents.Save(policy);
            Publish(policy);
        }
        logger.LogInformation("Health library seeded: {Sections} sections, {Pages} conditions and tests under {Library}", sections.Count, pages.Count, library.Name);
    }

    [GeneratedRegex(@"\s*\(\d+\)$")]
    private static partial Regex CopySuffix();

    /// <summary>
    /// Once: a second seeding run (October 2026, after the run flag was renamed by mistake) created a copy of every
    /// condition and test, named "… (1)". Each copy goes to the recycle bin; the first page of each name, which the other
    /// pages link to, stays.
    /// </summary>
    private void RemoveDuplicates()
    {
        if (keyValues.GetValue(DedupedKey) is not null) return;
        try
        {
            var ids = new[] { "healthCondition", "healthTest" }.Select(a => contentTypes.Get(a)?.Id).OfType<int>().ToArray();
            if (ids.Length < 2) return;
            var moved = 0;
            foreach (var group in contents.GetPagedOfTypes(ids, 0, 5000, out _, null, null).Where(c => !c.Trashed)
                         .GroupBy(c => (c.ParentId, Name: CopySuffix().Replace(c.GetCultureName(En) ?? c.Name ?? "", ""))))
            {
                foreach (var copy in group.OrderBy(c => c.CreateDate).ThenBy(c => c.Id).Skip(1))
                {
                    contents.MoveToRecycleBin(copy);
                    moved++;
                }
            }
            keyValues.SetValue(DedupedKey, moved.ToString());
            if (moved > 0) logger.LogInformation("Health library: {Count} duplicate pages moved to the recycle bin", moved);
        }
        catch (Exception e) { logger.LogError(e, "Removing duplicate health library pages failed"); }
    }

    private static void Text(IContent page, string alias, HealthLibraryContent.T text)
    {
        page.SetValue(alias, text.Ar, Ar);
        page.SetValue(alias, text.En, En);
    }

    private static string Links(IEnumerable<SeedLink> links) => JsonSerializer.Serialize(links.Select(l => l.Json()));

    /// <summary>A multi-node picker value: the pages' UDIs, comma separated (missing pages skipped).</summary>
    private static void Pick(IContent page, string alias, IEnumerable<IContent?> targets)
    {
        var udis = targets.OfType<IContent>().Select(t => Udi.Create(Constants.UdiEntityType.Document, t.Key).ToString()).Distinct().ToList();
        if (udis.Count > 0) page.SetValue(alias, string.Join(",", udis));
    }

    private Dictionary<string, IContent> ByEnglishName(string alias)
    {
        var type = contentTypes.Get(alias);
        if (type is null) return [];
        return contents.GetPagedOfTypes([type.Id], 0, 2000, out _, null, null)
            .Where(c => !c.Trashed && c.GetCultureName(En) is { Length: > 0 })
            .GroupBy(c => c.GetCultureName(En)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private void Publish(IContent page)
    {
        var result = contents.Publish(page, [Ar, En]);
        if (!result.Success) logger.LogWarning("{Page} saved as a draft, publishing failed: {Status}", page.GetCultureName(En), result.Result);
    }
}
