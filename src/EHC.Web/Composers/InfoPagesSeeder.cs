using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using EHC.Web.Site;

namespace EHC.Web.Composers;

/// <summary>
/// Once per page: creates and publishes the information pages that the navigation still sent to the old site
/// (ehc.med.sa, October 2026) — privacy policy, terms and conditions, patient &amp; visitor guide, for researchers,
/// research projects, academic affairs &amp; training — and a patients' rights and e-participation page (the menus sent
/// "Patient rights" to the Ministry of Health), with their texts (InfoPagesContent). Then, once per page, points the
/// navigation and contact page links to the pages they replace at the new ones. A page an editor deletes is not recreated.
/// </summary>
public sealed class InfoPagesSeeder(
    IContentService contents,
    IContentTypeService contentTypes,
    IKeyValueService keyValues,
    IRuntimeState runtime,
    IOptions<Contact.ContactOptions> contact,
    IOptions<Feedback.FeedbackOptions> feedback,
    IOptions<Stats.StatsOptions> stats,
    IOptions<Vitals.VitalsOptions> vitals,
    ILogger<InfoPagesSeeder> logger) : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private const string Prefix = "Ehc.InfoPages.";            // + page id; value: the page's key
    private const string LinkedKey = "Ehc.InfoPages.Linked";   // + "." + page id; without an id: the first six pages (before per-page flags)
    private static readonly string[] FirstSix = ["privacy", "terms", "guide", "projects", "researchers", "academic"];
    private const string Ar = BlockJson.Ar, En = BlockJson.En;

    /// <summary>A page to create: where it goes, its names and header intro, and the addresses (host/path) it replaces.</summary>
    private sealed record Spec(string Id, string Parent, string NameAr, string NameEn, string IntroAr, string IntroEn, string[] Replaces);

    // Parent: "home", or the English name of a page under the home page (falls back to the home page)
    private static readonly Spec[] Pages =
    [
        new("privacy", "home", "سياسة الخصوصية", "Privacy policy",
            "كيف نتعامل مع بياناتك عند استخدام هذا الموقع.", "How we handle your data when you use this website.",
            ["ehc.med.sa/ar/privacy-policy", "ehc.med.sa/privacy-policy"]),
        new("terms", "home", "الشروط والأحكام", "Terms and conditions",
            "الشروط اللازمة لاستخدام موقع تجمع الشرقية الصحي.", "The terms for using the Eastern Health Cluster website.",
            ["ehc.med.sa/ar/الشروط-والأحكام", "ehc.med.sa/terms-conditions"]),
        new("guide", "Care & Services", "دليل المرضى والزوار", "Patient and visitor guide",
            "كل ما تحتاجه للتخطيط لزيارتك، من لحظة وصولك حتى انتهاء رحلتك العلاجية.", "Everything you need to plan your visit, from the moment you arrive until your care is complete.",
            ["ehc.med.sa/ar/دليل-المرضى-والزوار", "ehc.med.sa/patient-visitor-guide"]),
        new("projects", "Research & Innovation", "المشاريع البحثية", "Research projects",
            "مختارات من الأبحاث المنشورة لباحثي التجمع.", "A selection of studies published by EHC researchers.",
            ["ehc.med.sa/ar/المشاريع-البحثية", "ehc.med.sa/research-projects"]),
        new("researchers", "Research & Innovation", "للباحثين", "For researchers",
            "بيئة بحثية تدعمك في كل مرحلة من مراحل مشروعك.", "A research environment that supports you at every stage of your project.",
            ["ehc.med.sa/ar/للباحثين", "ehc.med.sa/for-researchers"]),
        new("academic", "Research & Innovation", "الشؤون الأكاديمية والتدريب", "Academic affairs and training",
            "مستقبل صحي... يبدأ بك.", "A healthier future... starts with you.",
            ["ehc.med.sa/ar/الشؤون-الأكاديمية-والتدريب", "ehc.med.sa/academic-affairs-training"]),
        new("rights", "Care & Services", "حقوق المرضى والمشاركة الإلكترونية", "Patient rights and e-participation",
            "حقوقك محفوظة، وصوتك يساعدنا على التحسين.", "Your rights are protected, and your voice helps us improve.",
            ["moh.gov.sa/awarenessplateform/patientsrights/pages/default.aspx", "moh.gov.sa/en/awarenessplateform/patientsrights/pages/default.aspx"]),
    ];

    /// <summary>"host/path" of an absolute link in lower case, without "www." and slashes at the ends (null when not absolute).</summary>
    internal static string? HostPath(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? (uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host).ToLowerInvariant() + "/" + Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').ToLowerInvariant()
            : null;

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        try { Run(); }
        catch (Exception e) { logger.LogError(e, "Creating the information pages failed"); }
    }

    private void Run()
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        var homeType = contentTypes.Get("home");
        if (homeType is null || contentTypes.Get("landingPage") is null) return;
        var home = contents.GetRootContent().FirstOrDefault(c => c.ContentTypeId == homeType.Id && !c.Trashed);
        if (home is null) return;

        // every page gets its key up front, so pages created in the same run can link to each other
        var keys = Pages.ToDictionary(p => p.Id, p => Guid.TryParse(keyValues.GetValue(Prefix + p.Id), out var k) ? k : Guid.NewGuid());
        var todo = Pages.Where(p => keyValues.GetValue(Prefix + p.Id) is null).ToList();
        if (todo.Count > 0)
        {
            var content = new InfoPagesContent(Links(keys), alias => contentTypes.Get(alias)?.Key ?? throw new InvalidOperationException($"Element type {alias} is missing"),
                new InfoPagesContent.Retention(contact.Value.RetentionMonths, feedback.Value.RetentionMonths, stats.Value.RawRetentionDays, vitals.Value.RetentionDays));
            var landing = contentTypes.Get("landingPage")!;
            var children = contents.GetPagedOfTypes([landing.Id], 0, 500, out _, null, null).Where(c => c.ParentId == home.Id).ToList();
            foreach (var spec in todo)
            {
                var parent = spec.Parent == "home" ? home : children.FirstOrDefault(c => c.GetCultureName(En) == spec.Parent && !c.Trashed) ?? home;
                var page = contents.Create(spec.NameAr, parent.Key, "landingPage");
                page.Key = keys[spec.Id];
                page.SetCultureName(spec.NameAr, Ar);
                page.SetCultureName(spec.NameEn, En);
                page.SetValue("intro", spec.IntroAr, Ar);
                page.SetValue("intro", spec.IntroEn, En);
                page.SetValue("blocks", content.Blocks(spec.Id));
                contents.Save(page);
                var published = contents.Publish(page, [Ar, En]);
                if (!published.Success) logger.LogWarning("{Page} saved as a draft, publishing failed: {Status}", spec.NameEn, published.Result);
                keyValues.SetValue(Prefix + spec.Id, page.Key.ToString());
                logger.LogInformation("Created the page {Page} under {Parent}", spec.NameEn, parent.Name);
            }
        }

        var linked = keyValues.GetValue(LinkedKey) is not null;
        var unlinked = Pages.Where(p => keyValues.GetValue(LinkedKey + "." + p.Id) is null && !(linked && FirstSix.Contains(p.Id))).ToList();
        if (unlinked.Count > 0) LinkFromNavigation(unlinked, keys);
    }

    private InfoPagesContent.Pages Links(IReadOnlyDictionary<string, Guid> keys)
    {
        Guid? Folder(string alias) => contentTypes.Get(alias) is { } t && contents.GetPagedOfTypes([t.Id], 0, 1, out _, null, null).FirstOrDefault() is { } f ? f.Key : null;
        return new InfoPagesContent.Pages(
            keys["privacy"], keys["terms"], keys["guide"], keys["projects"], keys["researchers"], keys["academic"], keys["rights"],
            Guid.TryParse(keyValues.GetValue("Ehc.Contact.PageSeeded"), out var c) ? c : null,
            Folder("facilityFolder"), Folder("eServicesFolder"));
    }

    /// <summary>Navigation and contact page links to the pages these new pages replace now go to the new ones.</summary>
    private void LinkFromNavigation(IReadOnlyList<Spec> specs, IReadOnlyDictionary<string, Guid> keys)
    {
        var map = specs.SelectMany(p => p.Replaces.Select(path => (path, key: keys[p.Id]))).ToDictionary(x => x.path, x => x.key);
        Guid? Target(string url) => HostPath(url) is { } path && map.TryGetValue(path, out var key) ? key : null;
        var hosts = specs.SelectMany(p => p.Replaces).Select(r => r[..r.IndexOf('/')]).Distinct().ToArray();

        var items = new List<IContent>();
        if (contentTypes.Get("navigation") is { } nav) items.AddRange(contents.GetPagedOfTypes([nav.Id], 0, 10, out _, null, null));
        if (Guid.TryParse(keyValues.GetValue("Ehc.Contact.PageSeeded"), out var contactKey) && contents.GetById(contactKey) is { Trashed: false } page) items.Add(page);
        foreach (var item in items) LinkRewriter.Apply(contents, item, Target, logger, hosts);
        foreach (var spec in specs) keyValues.SetValue(LinkedKey + "." + spec.Id, "1");
    }
}
