using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using EHC.Web.Site;

namespace EHC.Web.Contact;

/// <summary>
/// Once: creates and publishes a "Contact us" page under the home page with the contact form block (texts and contact
/// details from the contact page on ehc.med.sa, October 2026), then points navigation links that still go to that old
/// page to the new one. Navigation is republished only when it has no unpublished edits; otherwise the change is left as
/// a draft. Waits (and tries again on the next start) until the contactFormBlock element type has been imported.
/// </summary>
public sealed class ContactPageSeeder(IContentService contents, IContentTypeService contentTypes, IKeyValueService keyValues, IRuntimeState runtime, ILogger<ContactPageSeeder> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private const string PageKey = "Ehc.Contact.PageSeeded";          // value: the new page's key
    private const string NavigationKey = "Ehc.Contact.NavigationLinked";
    private const string Ar = "ar-SA", En = "en-US";
    private const string PrivacyUrl = "https://www.ehc.med.sa/ar/privacy-policy/";
    private static readonly Guid BlockSettingsType = new("41e32f64-114d-4ddd-bb62-a0050c955eec");

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        try { Run(); }
        catch (Exception e) { logger.LogError(e, "Creating the contact page failed"); }
    }

    private void Run()
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        if (keyValues.GetValue(PageKey) is null) CreatePage();
        if (keyValues.GetValue(NavigationKey) is null && Guid.TryParse(keyValues.GetValue(PageKey), out var page))
        {
            RepointNavigation(page);
            keyValues.SetValue(NavigationKey, "1");
        }
    }

    private void CreatePage()
    {
        var homeType = contentTypes.Get("home");
        var formType = contentTypes.Get("contactFormBlock");
        if (homeType is null || formType is null || contentTypes.Get("landingPage") is null) return;

        // the site has one home page; the first one gets the contact page
        foreach (var home in contents.GetRootContent().Where(c => c.ContentTypeId == homeType.Id && !c.Trashed).Take(1))
        {
            var page = contents.Create("تواصل معنا", home.Key, "landingPage");
            page.SetCultureName("تواصل معنا", Ar);
            page.SetCultureName("Contact us", En);
            page.SetValue("intro", "نحن هنا لخدمتك", Ar);
            page.SetValue("intro", "We are here to help you", En);
            page.SetValue("blocks", Blocks(formType.Key));
            contents.Save(page);
            var published = contents.Publish(page, [Ar, En]);
            if (!published.Success) logger.LogWarning("Contact page saved as a draft, publishing failed: {Status}", published.Result);
            logger.LogInformation("Created the contact page under {Home}", home.Name);
            keyValues.SetValue(PageKey, page.Key.ToString());
        }
    }

    private static string Blocks(Guid formType)
    {
        Guid content = Guid.NewGuid(), settings = Guid.NewGuid();
        object Value(string alias, string? culture, object value) => new { alias, culture, segment = (string?)null, value };
        var values = new List<object>
        {
            Value("eyebrow", Ar, "شاركنا رأيك"),
            Value("eyebrow", En, "Share your feedback"),
            Value("heading", Ar, "نستقبل رسالتك *باهتمام*"),
            Value("heading", En, "Every message *matters to us*"),
            Value("intro", Ar, "يسرّنا تواصلك معنا، ونسعد بخدمتك والإجابة على أي استفسار. من فضلك، شاركنا معلوماتك، وسيتواصل معك فريقنا في أقرب وقت ممكن."),
            Value("intro", En, "We are glad to hear from you and happy to answer any question. Please share your details and our team will contact you as soon as possible."),
            Value("phone", null, "920022231"),
            Value("email", null, "info@ehc.med.sa"),
            Value("privacyLink", Ar, ExternalLink("سياسة الخصوصية", PrivacyUrl)),
            Value("privacyLink", En, ExternalLink("Privacy policy", PrivacyUrl)),
        };
        return JsonSerializer.Serialize(new
        {
            contentData = new[] { new { contentTypeKey = formType, key = content, values } },
            settingsData = new[] { new { contentTypeKey = BlockSettingsType, key = settings, values = Array.Empty<object>() } },
            expose = new[] { new { contentKey = content, culture = Ar, segment = (string?)null }, new { contentKey = content, culture = En, segment = (string?)null } },
            layout = new Dictionary<string, object> { ["Umbraco.BlockList"] = new[] { new { contentKey = content, settingsKey = settings } } },
        });
    }

    // link pickers inside blocks store their value as JSON text
    private static string ExternalLink(string name, string url) => JsonSerializer.Serialize(new[]
    {
        new { name, target = "_blank", unique = (string?)null, type = "external", udi = (string?)null, url, queryString = (string?)null, culture = (string?)null },
    });

    /// <summary>Navigation links to the old contact page (ehc.med.sa/ar/تواصل-معنا/ or /contact-us/) become links to the new page.</summary>
    private void RepointNavigation(Guid pageKey)
    {
        var navType = contentTypes.Get("navigation");
        if (navType is null) return;
        foreach (var nav in contents.GetPagedOfTypes([navType.Id], 0, 10, out _, null, null))
        {
            LinkRewriter.Apply(contents, nav, url => IsOldContactPage(url) ? pageKey : null, logger);
        }
    }

    internal static int Repoint(JsonNode? node, Guid pageKey) => LinkRewriter.Rewrite(node, url => IsOldContactPage(url) ? pageKey : null);

    internal static bool IsOldContactPage(string? url) => LinkRewriter.OldSitePath(url) is "ar/تواصل-معنا" or "تواصل-معنا" or "contact-us" or "en/contact-us";
}
