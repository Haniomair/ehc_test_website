using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace EHC.Web.Composers;

/// <summary>
/// Creates the E-services folder under the home page with the e-services listed on the current site (ehc.med.sa,
/// October 2026), once, while there is no E-services folder. Everything is saved as an unpublished draft for the
/// content team to check (texts, links, audiences) and publish.
/// </summary>
public sealed class EServicesSeeder(IContentService contents, IContentTypeService contentTypes, IRuntimeState runtime, ILogger<EServicesSeeder> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private const string Ar = "ar-SA", En = "en-US";
    private const string SehhatyAr = "https://www.moh.gov.sa/eservices/sehhaty/pages/default.aspx";
    private const string SehhatyEn = "https://www.moh.gov.sa/en/eservices/sehhaty/pages/default.aspx";

    private sealed record Seed(string NameAr, string NameEn, string SummaryAr, string SummaryEn, string Audience, string UrlAr, string UrlEn,
        string ProviderAr, string ProviderEn, bool SignIn, string Icon);

    private static readonly Seed[] Seeds =
    [
        new("المواعيد والزيارات", "Appointments and visits", "اطّلع على مواعيدك وزياراتك.", "View your appointments and visits.", "Patients", SehhatyAr, SehhatyEn, "صحتي (وزارة الصحة)", "Sehhaty (Ministry of Health)", true, "cal"),
        new("حجز موعد", "Book an appointment", "احجز مواعيدك وتابعها.", "Book and follow your appointments.", "Patients", SehhatyAr, SehhatyEn, "صحتي (وزارة الصحة)", "Sehhaty (Ministry of Health)", true, "clock"),
        new("التقارير الطبية", "Medical reports", "اطلب تقاريرك الطبية واطّلع عليها.", "Request and view your medical reports.", "Patients", SehhatyAr, SehhatyEn, "صحتي (وزارة الصحة)", "Sehhaty (Ministry of Health)", true, "note"),
        new("العلامات الحيوية", "Vital signs", "اعرض علاماتك الحيوية وتابعها.", "View and follow your vital signs.", "Patients", SehhatyAr, SehhatyEn, "صحتي (وزارة الصحة)", "Sehhaty (Ministry of Health)", true, "heart"),
        new("البريد الإلكتروني", "E-mail", "البريد الإلكتروني الرسمي للموظفين.", "Official employee e-mail.", "Staff", "https://webmail.moh.gov.sa/owa", "https://webmail.moh.gov.sa/owa", "وزارة الصحة", "Ministry of Health", true, "mail"),
        new("موارد", "Mawared", "الخدمة الذاتية للموظفين.", "Employee self-service.", "Staff", "https://erp.moh.gov.sa/", "https://erp.moh.gov.sa/", "وزارة الصحة", "Ministry of Health", true, "brief"),
        new("نظام إدارة قائمة الأدوية", "Formulary management system", "إدارة أدوية القائمة الدوائية ومتابعتها.", "Manage and monitor formulary medications.", "Staff", "https://tb.ehc.med.sa/login?redirect-to=%2Fapp%2Fformulary-management", "https://tb.ehc.med.sa/login?redirect-to=%2Fapp%2Fformulary-management", "تجمع الشرقية الصحي", "Eastern Health Cluster", true, "lab"),
        new("منصة اعتماد", "Etimad portal", "منصة الخدمات المالية الإلكترونية للموردين.", "Financial e-services platform for suppliers.", "Suppliers", "https://portal.etimad.sa/", "https://portal.etimad.sa/", "اعتماد", "Etimad", true, "doc"),
        new("التدريب", "Training", "التدريب لطلاب الجامعات.", "Training for university students.", "Trainees", "https://tb.ehc.med.sa/admission", "https://tb.ehc.med.sa/admission", "تجمع الشرقية الصحي", "Eastern Health Cluster", false, "book"),
        new("التطوع الصحي", "Health volunteering", "منصة التطوع الصحي.", "Health volunteering platform.", "Volunteers", "https://volunteer.srca.org.sa/", "https://volunteer.srca.org.sa/", "هيئة الهلال الأحمر السعودي", "Saudi Red Crescent Authority", false, "hand"),
    ];

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        try { Run(); }
        catch (Exception e) { logger.LogError(e, "Seeding e-services failed"); }
    }

    private void Run()
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        var homeType = contentTypes.Get("home");
        var folderType = contentTypes.Get("eServicesFolder");
        if (homeType is null || folderType is null || contentTypes.Get("eService") is null) return;
        if (contents.GetPagedOfTypes([folderType.Id], 0, 1, out var existing, null, null).Any() || existing > 0) return;

        foreach (var home in contents.GetRootContent().Where(c => c.ContentTypeId == homeType.Id && !c.Trashed))
        {
            var folder = contents.Create("الخدمات الإلكترونية", home.Key, "eServicesFolder");
            folder.SetCultureName("الخدمات الإلكترونية", Ar);
            folder.SetCultureName("E-services", En);
            contents.Save(folder);
            foreach (var s in Seeds)
            {
                var page = contents.Create(s.NameAr, folder.Key, "eService");
                page.SetCultureName(s.NameAr, Ar);
                page.SetCultureName(s.NameEn, En);
                page.SetValue("summary", s.SummaryAr, Ar);
                page.SetValue("summary", s.SummaryEn, En);
                page.SetValue("provider", s.ProviderAr, Ar);
                page.SetValue("provider", s.ProviderEn, En);
                page.SetValue("startLink", Link(s.ProviderAr, s.UrlAr), Ar);
                page.SetValue("startLink", Link(s.ProviderEn, s.UrlEn), En);
                page.SetValue("audiences", JsonSerializer.Serialize(new[] { s.Audience }));
                page.SetValue("icon", JsonSerializer.Serialize(new[] { s.Icon }));
                page.SetValue("requiresSignIn", s.SignIn);
                contents.Save(page);
            }
            logger.LogInformation("Seeded the E-services folder with {Count} e-services as drafts under {Home}", Seeds.Length, home.Name);
        }
    }

    private static string Link(string name, string url) => JsonSerializer.Serialize(new[]
    {
        new { name, target = "_blank", unique = (string?)null, type = (string?)null, udi = (string?)null, url, queryString = (string?)null, culture = (string?)null },
    });
}
