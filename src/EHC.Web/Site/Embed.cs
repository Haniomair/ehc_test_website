using System.Text.RegularExpressions;

namespace EHC.Web.Site;

/// <summary>Embed section settings (appsettings "Ehc:Embed").</summary>
public sealed class EmbedOptions
{
    /// <summary>Host names of EHC's Frappe sites whose web forms may be embedded, e.g. "forms.ehc.med.sa".
    /// Each host is also added to the Content-Security-Policy frame-src. Empty = Frappe embeds are off.</summary>
    public string[] FrappeHosts { get; set; } = [];
}

/// <summary>
/// Allow-list for the Embed section. An editor's link is never used as-is: it is parsed, checked against the provider's
/// hosts and URL shape, and rebuilt from the validated parts, so only known form / report addresses can become a frame.
/// </summary>
public static partial class Embed
{
    public const string MicrosoftForms = "microsoftForms";
    public const string PowerBi = "powerBi";
    public const string Frappe = "frappe";

    public static readonly string[] MicrosoftFormsHosts = ["forms.office.com", "forms.microsoft.com", "forms.cloud.microsoft"];
    public const string PowerBiHost = "app.powerbi.com";

    // Frappe desk / API / file routes are never embeddable, only public web form routes
    private static readonly HashSet<string> FrappeBlocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "app", "api", "desk", "login", "logout", "private", "files", "backups", "method", "update-password", "me", "print", "printview",
    };

    /// <summary>frame-src values for the Content-Security-Policy.</summary>
    public static IEnumerable<string> FrameSources(EmbedOptions options) =>
        MicrosoftFormsHosts.Append(PowerBiHost).Concat(CleanHosts(options.FrappeHosts)).Select(h => "https://" + h);

    public static IEnumerable<string> CleanHosts(IEnumerable<string>? hosts) =>
        (hosts ?? []).Select(h => h.Trim().ToLowerInvariant()).Where(h => HostName().IsMatch(h)).Distinct();

    /// <summary>The frame address for a provider + editor link, or null when the link is not allowed.</summary>
    public static string? Src(string? provider, string? url, IEnumerable<string>? frappeHosts)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0) return null;
        var host = uri.IdnHost.ToLowerInvariant();
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var path = uri.AbsolutePath;

        switch (provider)
        {
            case MicrosoftForms when MicrosoftFormsHosts.Contains(host):
            {
                if (path.Equals("/Pages/ResponsePage.aspx", StringComparison.OrdinalIgnoreCase))
                {
                    var id = query["id"];
                    return id is not null && Token().IsMatch(id) ? $"https://{host}/Pages/ResponsePage.aspx?id={id}&embed=true" : null;
                }
                var m = FormsShortLink().Match(path);
                return m.Success ? $"https://{host}/{m.Groups[1].Value}/{m.Groups[2].Value}?embed=true" : null;
            }
            case PowerBi when host == PowerBiHost && path.Equals("/view", StringComparison.OrdinalIgnoreCase):
            {
                // "Publish to web" reports only (public by design); secure embeds need a sign-in and are not supported
                var r = query["r"];
                if (r is null || !Token().IsMatch(r)) return null;
                var pageName = query["pageName"];
                return $"https://{PowerBiHost}/view?r={r}" + (pageName is not null && Token().IsMatch(pageName) ? $"&pageName={pageName}" : "");
            }
            case Frappe when CleanHosts(frappeHosts).Contains(host):
            {
                var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length is 0 or > 4 || FrappeBlocked.Contains(segments[0]) || !segments.All(s => Slug().IsMatch(s))) return null;
                return $"https://{host}/{string.Join('/', segments)}";
            }
            default:
                return null;
        }
    }

    /// <summary>Visitor-facing provider name for the notice ("provided by …").</summary>
    public static string ProviderName(string? provider, string? src) => provider switch
    {
        MicrosoftForms => "Microsoft Forms",
        PowerBi => "Microsoft Power BI",
        _ => Uri.TryCreate(src, UriKind.Absolute, out var u) ? u.Host : "",
    };

    [GeneratedRegex("^[A-Za-z0-9_=-]{4,400}$")]
    private static partial Regex Token();

    [GeneratedRegex("^/(r|e)/([A-Za-z0-9]{4,40})/?$")]
    private static partial Regex FormsShortLink();

    [GeneratedRegex("^[a-z0-9-]{1,60}$")]
    private static partial Regex Slug();

    [GeneratedRegex(@"^(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$")]
    private static partial Regex HostName();
}
