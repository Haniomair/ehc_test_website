using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Site;

/// <summary>
/// E-services: pages (eService) under the E-services folder, each a short guide with a Start link to an external
/// system (Sehhaty, Etimad, staff systems…) and the audiences it is for. Audience names come from the dictionary
/// (EHC.EServices.Audience.{key}); the order below is the order of the tabs and filters (patients first).
/// </summary>
public static class EServices
{
    /// <param name="Value">the stored checkbox value (EHC - E-service audience)</param>
    public sealed record Audience(string Key, string Value, string Icon);

    public static readonly IReadOnlyList<Audience> Audiences =
    [
        new("patients", "Patients", "heart"),
        new("staff", "Staff", "brief"),
        new("suppliers", "Suppliers", "doc"),
        new("trainees", "Trainees", "book"),
        new("volunteers", "Volunteers", "hand"),
    ];

    /// <summary>Stored checkbox values → audience keys, in the fixed order; unknown values are ignored.</summary>
    public static IReadOnlyList<string> Keys(IEnumerable<string>? values)
    {
        var set = (values ?? []).Select(v => v.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Audiences.Where(a => set.Contains(a.Value) || set.Contains(a.Key)).Select(a => a.Key).ToList();
    }

    public static IReadOnlyList<string> Of(IPublishedContent service) => Keys(service.Value<IEnumerable<string>>("audiences"));

    public static Audience? Find(string? key) => Audiences.FirstOrDefault(a => a.Key == key);

    /// <summary>The site's E-services folder (under the home page of <paramref name="page"/>).</summary>
    public static IPublishedContent? Folder(IPublishedContent page) =>
        page.AncestorOrSelf("home")?.Children().FirstOrDefault(c => c.ContentType.Alias == "eServicesFolder");

    /// <summary>Published e-services in the folder's order.</summary>
    public static List<IPublishedContent> All(IPublishedContent page) =>
        Folder(page)?.Children().Where(c => c.ContentType.Alias == "eService").ToList() ?? [];
}
