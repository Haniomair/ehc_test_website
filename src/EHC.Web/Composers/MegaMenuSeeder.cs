using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using EHC.Web.Site;
using static EHC.Web.Composers.BlockField;

namespace EHC.Web.Composers;

/// <summary>
/// Once: the mega menu item that has no featured card (Services) gets one — "Not sure where to go?", linking to the care
/// navigator on the home page — so every menu shares the same layout. The navigator section gets the anchor
/// "care-navigator" for that link when it has none. Also once: the shortest menus get links to the pages that now exist
/// (Balance). Editors can change or remove all of it afterwards. Content with unpublished edits is saved as a draft
/// (ContentChanges).
/// </summary>
public sealed class MegaMenuSeeder(IContentService contents, IContentTypeService contentTypes, IKeyValueService keyValues, IRuntimeState runtime, ILogger<MegaMenuSeeder> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private const string DoneKey = "Ehc.Mega.NavigatorCard";
    private const string BalanceKey = "Ehc.Mega.Balanced";
    private const string Anchor = "care-navigator";

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        try { Run(); }
        catch (Exception e) { logger.LogError(e, "Adding the care navigator card to the mega menu failed"); }
    }

    private void Run()
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        if (keyValues.GetValue(DoneKey) is null) AddNavigatorCard();
        if (keyValues.GetValue(BalanceKey) is null && contentTypes.Get("navigation") is { } navType && contentTypes.Get("megaLink") is not null && contentTypes.Get("megaColumn") is not null)
        {
            foreach (var nav in contents.GetPagedOfTypes([navType.Id], 0, 10, out _, null, null)) Balance(nav);
            keyValues.SetValue(BalanceKey, "1");
        }
    }

    private void AddNavigatorCard()
    {
        var homeType = contentTypes.Get("home");
        var navType = contentTypes.Get("navigation");
        var promo = contentTypes.Get("megaFeaturedPromo");
        var navigator = contentTypes.Get("careNavigatorBlock");
        if (homeType is null || navType is null || promo is null || navigator is null) return;
        var home = contents.GetRootContent().FirstOrDefault(c => c.ContentTypeId == homeType.Id && !c.Trashed);
        if (home is null) return;

        AnchorNavigator(home, navigator.Key);

        foreach (var nav in contents.GetPagedOfTypes([navType.Id], 0, 10, out _, null, null))
        {
            if (nav.GetValue<string>("megaMenu") is not { } raw || JsonNode.Parse(raw) is not JsonObject root) continue;
            var added = 0;
            foreach (var item in root["contentData"]?.AsArray() ?? [])
            {
                var values = item!["values"]!.AsArray();
                var featured = values.FirstOrDefault(v => (string?)v?["alias"] == "featured");
                if (featured?["value"] is JsonValue fv && fv.TryGetValue<string>(out var f) && f.Contains("contentData") && !f.Contains("\"contentData\":[]")) continue;
                var columns = values.FirstOrDefault(v => (string?)v?["alias"] == "columns");
                if (columns is null) continue;   // a plain link item: no panel to fill
                var card = new BlockJson(a => contentTypes.Get(a)?.Key ?? throw new InvalidOperationException($"Element type {a} is missing")).Items()
                    .Add("megaFeaturedPromo", Pick("icon", "compass"),
                        Text("title", "لا تعرف أين تذهب؟", "Not sure where to go?"),
                        Text("text", "ابحث حسب العَرَض، واعرف مستوى الرعاية المناسب وأقرب منشأة إليك.", "Search by symptom to find the right level of care and the nearest facility."),
                        Link("link", new SeedLink("مرشد الرعاية", Page: home.Key, Anchor: "#" + Anchor), new SeedLink("Care navigator", Page: home.Key, Anchor: "#" + Anchor)))
                    .Build();
                if (featured is not null) featured["value"] = card;
                else values.Add(new JsonObject { ["alias"] = "featured", ["culture"] = null, ["segment"] = null, ["value"] = card });
                added++;
            }
            if (added == 0) continue;
            var hadDraft = nav.Edited;
            nav.SetValue("megaMenu", root.ToJsonString());
            ContentChanges.SaveKeepingDrafts(contents, nav, hadDraft, logger, $"Care navigator card added to {added} mega menu item(s)");
        }
        keyValues.SetValue(DoneKey, "1");
    }

    /// <summary>
    /// Once: the two shortest menus get their content filled in with pages that now exist. Research: its placeholder
    /// links (no target yet) go to For researchers / Research projects / Academic affairs, plus a link to the university
    /// training e-service. About: a "Contact &amp; policies" group (contact, patient rights, privacy, terms). Links an
    /// editor has already set are never changed.
    /// </summary>
    private void Balance(IContent nav)
    {
        Guid? Page(string key) => Guid.TryParse(keyValues.GetValue(key), out var g) && contents.GetById(g) is { Trashed: false } ? g : null;
        var researchers = Page("Ehc.InfoPages.researchers");
        var projects = Page("Ehc.InfoPages.projects");
        var academic = Page("Ehc.InfoPages.academic");
        var training = contentTypes.Get("eService") is { } es
            ? contents.GetPagedOfTypes([es.Id], 0, 200, out _, null, null).FirstOrDefault(p => !p.Trashed && p.GetCultureName("en-US") == "Training")?.Key
            : null;
        var targets = new Dictionary<string, Guid?>
        {
            ["Clinical research"] = researchers, ["Research ethics (IRB)"] = researchers, ["Publications"] = projects, ["Residency & fellowship"] = academic,
        };

        if (nav.GetValue<string>("megaMenu") is not { } raw || JsonNode.Parse(raw) is not JsonObject root) return;
        var changed = 0;
        foreach (var item in root["contentData"]?.AsArray() ?? [])
        {
            var label = Value(item!, "label", "en-US");
            if (label is not ("Research" or "About")) continue;
            var columnsValue = item!["values"]!.AsArray().FirstOrDefault(v => (string?)v?["alias"] == "columns");
            if (columnsValue?["value"] is not JsonValue cv || !cv.TryGetValue<string>(out var columnsRaw) || JsonNode.Parse(columnsRaw) is not JsonObject columns) continue;

            if (label == "Research")
            {
                foreach (var column in columns["contentData"]?.AsArray() ?? [])
                {
                    var linksValue = column!["values"]!.AsArray().FirstOrDefault(v => (string?)v?["alias"] == "links");
                    if (linksValue?["value"] is not JsonValue lv || !lv.TryGetValue<string>(out var linksRaw) || JsonNode.Parse(linksRaw) is not JsonObject links) continue;
                    var columnChanged = false;
                    foreach (var link in links["contentData"]?.AsArray() ?? [])
                    {
                        var title = Value(link!, "title", "en-US");
                        if (title is null || !targets.TryGetValue(title, out var target) || target is not { } key) continue;
                        var values = link!["values"]!.AsArray();
                        var existing = values.FirstOrDefault(v => (string?)v?["alias"] == "link");
                        if (existing?["value"] is JsonValue ev && ev.TryGetValue<string>(out var e) && e.Length > 2) continue;   // set by an editor
                        var json = System.Text.Json.JsonSerializer.Serialize(new[] { new SeedLink(title, Page: key).Json() });
                        if (existing is not null) existing["value"] = json;
                        else values.Add(new JsonObject { ["alias"] = "link", ["culture"] = null, ["segment"] = null, ["value"] = json });
                        columnChanged = true;
                        changed++;
                    }
                    if (Value(column, "heading", "en-US") == "Education & innovation" && training is { } t
                        && !(links["contentData"]?.AsArray().Any(l => Value(l!, "title", "en-US") == "University student training") ?? false))
                    {
                        AppendLink(links, "book", "تدريب طلاب الجامعات", "University student training", "التدريب الجامعي والامتياز", "Undergraduate and internship training", t);
                        columnChanged = true;
                        changed++;
                    }
                    if (columnChanged) linksValue["value"] = links.ToJsonString();
                }
            }
            else if (!(columns["contentData"]?.AsArray().Any(c => Value(c!, "heading", "en-US") == "Contact & policies") ?? false))
            {
                var group = new BlockJson(TypeKey).Items();
                var groupLinks = new JsonObject { ["contentData"] = new JsonArray(), ["settingsData"] = new JsonArray(), ["expose"] = new JsonArray(), ["layout"] = new JsonObject { ["Umbraco.BlockList"] = new JsonArray() } };
                if (Page("Ehc.Contact.PageSeeded") is { } contact) AppendLink(groupLinks, "chat", "تواصل معنا", "Contact us", "نسعد بتواصلك", "We're glad to hear from you", contact);
                if (Page("Ehc.InfoPages.rights") is { } rights) AppendLink(groupLinks, "hand", "حقوق المرضى والمشاركة", "Patient rights & e-participation", "حقوقك وصوتك", "Your rights and your voice", rights);
                if (Page("Ehc.InfoPages.privacy") is { } privacy) AppendLink(groupLinks, "shield", "سياسة الخصوصية", "Privacy policy", "كيف نتعامل مع بياناتك", "How we handle your data", privacy);
                if (Page("Ehc.InfoPages.terms") is { } terms) AppendLink(groupLinks, "note", "الشروط والأحكام", "Terms and conditions", "شروط استخدام الموقع", "Terms for using the website", terms);
                if (groupLinks["contentData"]!.AsArray().Count == 0) continue;
                var column = JsonNode.Parse(group.Add("megaColumn", Text("heading", "تواصل وسياسات", "Contact & policies"), Shared("links", groupLinks.ToJsonString())).Build())!;
                Append(columns, column);
                changed++;
            }
            columnsValue!["value"] = columns.ToJsonString();
        }
        if (changed == 0) return;
        var hadDraft = nav.Edited;
        nav.SetValue("megaMenu", root.ToJsonString());
        ContentChanges.SaveKeepingDrafts(contents, nav, hadDraft, logger, $"Mega menu: {changed} links added or linked to the new pages");
    }

    private Guid TypeKey(string alias) => contentTypes.Get(alias)?.Key ?? throw new InvalidOperationException($"Element type {alias} is missing");

    private static string? Value(JsonNode block, string alias, string culture) =>
        block["values"]?.AsArray().FirstOrDefault(v => (string?)v?["alias"] == alias && (string?)v["culture"] == culture)?["value"]?.GetValue<string>();

    private void AppendLink(JsonObject list, string icon, string titleAr, string titleEn, string textAr, string textEn, Guid page)
    {
        var link = new BlockJson(TypeKey).Items().Add("megaLink", Pick("icon", icon), Text("title", titleAr, titleEn), Text("description", textAr, textEn),
            Shared("link", System.Text.Json.JsonSerializer.Serialize(new[] { new SeedLink(titleEn, Page: page).Json() }))).Build();
        Append(list, JsonNode.Parse(link)!);
    }

    /// <summary>Adds the blocks of a one-block list (content, expose, layout) to an existing block list value.</summary>
    private static void Append(JsonObject list, JsonNode single)
    {
        foreach (var name in new[] { "contentData", "expose" })
        {
            if (list[name] is not JsonArray target) list[name] = target = new JsonArray();
            foreach (var x in single[name]!.AsArray().ToList()) target.Add(x!.DeepClone());
        }
        if (list["layout"]?["Umbraco.BlockList"] is not JsonArray layout)
        {
            list["layout"] = new JsonObject { ["Umbraco.BlockList"] = layout = new JsonArray() };
        }
        foreach (var x in single["layout"]!["Umbraco.BlockList"]!.AsArray().ToList()) layout.Add(x!.DeepClone());
    }

    /// <summary>The first care navigator section on the home page gets the anchor "care-navigator" when it has none.</summary>
    private void AnchorNavigator(IContent home, Guid navigatorType)
    {
        if (home.GetValue<string>("blocks") is not { } raw || JsonNode.Parse(raw) is not JsonObject root) return;
        var block = root["contentData"]?.AsArray().FirstOrDefault(b => string.Equals((string?)b?["contentTypeKey"], navigatorType.ToString(), StringComparison.OrdinalIgnoreCase));
        if (block is null) return;
        var key = (string?)block["key"];
        var settingsKey = root["layout"]?["Umbraco.BlockList"]?.AsArray().FirstOrDefault(l => (string?)l?["contentKey"] == key)?["settingsKey"];
        var settings = root["settingsData"]?.AsArray().FirstOrDefault(s => (string?)s?["key"] == (string?)settingsKey);
        if (settings?["values"] is not JsonArray values) return;
        if (values.Any(v => (string?)v?["alias"] == "anchorId" && !string.IsNullOrWhiteSpace((string?)v["value"]))) return;
        values.Add(new JsonObject { ["alias"] = "anchorId", ["culture"] = null, ["segment"] = null, ["value"] = Anchor });
        var hadDraft = home.Edited;
        home.SetValue("blocks", root.ToJsonString());
        ContentChanges.SaveKeepingDrafts(contents, home, hadDraft, logger, "Care navigator anchor added");
    }
}
