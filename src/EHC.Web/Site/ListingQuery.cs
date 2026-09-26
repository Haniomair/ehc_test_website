using Microsoft.AspNetCore.Http;

namespace EHC.Web.Site;

/// <summary>Builds listing URLs that keep the current filters, changing one key (null removes it; page resets).</summary>
public static class ListingQuery
{
    public static string With(HttpRequest request, string key, string? value)
    {
        var q = request.Query
            .Where(kv => kv.Key != key && kv.Key != "page" && !string.IsNullOrEmpty(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        if (!string.IsNullOrEmpty(value)) q[key] = value;
        return request.Path + (q.Count == 0 ? "" : QueryString.Create(q.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value))).ToUriComponent());
    }

    public static string Page(HttpRequest request, int page)
    {
        var q = request.Query.Where(kv => kv.Key != "page" && !string.IsNullOrEmpty(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        if (page > 1) q["page"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return request.Path + (q.Count == 0 ? "" : QueryString.Create(q.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value))).ToUriComponent());
    }

    public static int CurrentPage(HttpRequest request) =>
        int.TryParse(request.Query["page"], out var p) && p > 0 ? Math.Min(p, 500) : 1;

    public static Guid? Key(HttpRequest request, string key) =>
        Guid.TryParse(request.Query[key], out var g) ? g : null;
}
