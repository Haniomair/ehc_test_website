using System.Text.RegularExpressions;

namespace EHC.Web.Stats;

/// <summary>Where a visit came from: medium (direct / search / social / referral / campaign) and a source name.</summary>
public readonly record struct TrafficSource(string Medium, string Source);

/// <summary>
/// Turns the user-agent string and the referring page into coarse, non-identifying groups (device class, browser and
/// operating system family, traffic source). Only the group is kept, never the user-agent string or the referring URL.
/// </summary>
public static partial class StatsTraffic
{
    public const string Direct = "direct";
    public const string Search = "search";
    public const string Social = "social";
    public const string Referral = "referral";
    public const string Campaign = "campaign";

    public static readonly string[] Devices = ["mobile", "tablet", "desktop"];

    // automated clients (crawlers, link previews, uptime checks, scripted browsers); real browsers all send "Mozilla/"
    [GeneratedRegex(@"bot|crawl|spider|slurp|archiver|facebookexternalhit|preview|headless|lighthouse|pagespeed|gtmetrix|pingdom|uptime|monitor|curl|wget|python|java/|go-http|httpclient|okhttp|axios|node-fetch|undici|scrapy|phantomjs|selenium|puppeteer|playwright|mediapartners", RegexOptions.IgnoreCase)]
    private static partial Regex Bots();

    [GeneratedRegex(@"iPad|Tablet|PlayBook|Silk|Kindle", RegexOptions.IgnoreCase)]
    private static partial Regex Tablets();

    [GeneratedRegex(@"Mobi|iPhone|iPod|Android|Windows Phone", RegexOptions.IgnoreCase)]
    private static partial Regex Phones();

    [GeneratedRegex(@"FBAN|FBAV|Instagram|Snapchat|TikTok|musical_ly|Line/|; wv\)")]
    private static partial Regex InApp();

    public static bool IsBot(string? userAgent) =>
        string.IsNullOrWhiteSpace(userAgent) || !userAgent.Contains("Mozilla/", StringComparison.Ordinal) || Bots().IsMatch(userAgent);

    /// <param name="touch">The browser reported a touch screen (iPadOS presents itself as a Mac).</param>
    public static string Device(string userAgent, bool touch)
    {
        var android = userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase);
        if (Tablets().IsMatch(userAgent) || (android && !userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase))) return "tablet";
        if (touch && userAgent.Contains("Macintosh", StringComparison.Ordinal)) return "tablet";
        return Phones().IsMatch(userAgent) ? "mobile" : "desktop";
    }

    public static string Browser(string ua)
    {
        // order matters: most browsers also claim to be Chrome and Safari
        if (InApp().IsMatch(ua)) return "In-app browser";
        if (ua.Contains("Edg/") || ua.Contains("EdgA/") || ua.Contains("EdgiOS/")) return "Edge";
        if (ua.Contains("OPR/") || ua.Contains("Opera")) return "Opera";
        if (ua.Contains("SamsungBrowser/")) return "Samsung Internet";
        if (ua.Contains("Firefox/") || ua.Contains("FxiOS/")) return "Firefox";
        if (ua.Contains("CriOS/") || ua.Contains("Chrome/")) return "Chrome";
        if (ua.Contains("Safari/") && ua.Contains("Version/")) return "Safari";
        return "Other";
    }

    public static string OperatingSystem(string ua, bool touch)
    {
        if (ua.Contains("Windows")) return "Windows";
        if (ua.Contains("CrOS")) return "ChromeOS";
        if (ua.Contains("iPhone") || ua.Contains("iPad") || ua.Contains("iPod")) return "iOS";
        if (ua.Contains("Macintosh")) return touch ? "iOS" : "macOS";
        if (ua.Contains("Android")) return "Android";
        if (ua.Contains("Linux")) return "Linux";
        return "Other";
    }

    private static readonly (Regex Host, string Name, string Medium)[] Known =
    [
        (Host(@"google(\.[a-z]{2,3}){1,2}|googlequicksearchbox"), "Google", Search),
        (Host(@"bing\.com"), "Bing", Search),
        (Host(@"yahoo(\.[a-z]{2,3}){1,2}"), "Yahoo", Search),
        (Host(@"duckduckgo\.com"), "DuckDuckGo", Search),
        (Host(@"yandex(\.[a-z]{2,3}){1,2}|ya\.ru"), "Yandex", Search),
        (Host(@"baidu\.com"), "Baidu", Search),
        (Host(@"ecosia\.org"), "Ecosia", Search),
        (Host(@"search\.brave\.com"), "Brave Search", Search),
        (Host(@"facebook\.com|fb\.com|fb\.me"), "Facebook", Social),
        (Host(@"instagram\.com"), "Instagram", Social),
        (Host(@"t\.co|twitter\.com|x\.com"), "X", Social),
        (Host(@"linkedin\.com|lnkd\.in"), "LinkedIn", Social),
        (Host(@"youtube\.com|youtu\.be"), "YouTube", Social),
        (Host(@"tiktok\.com"), "TikTok", Social),
        (Host(@"snapchat\.com"), "Snapchat", Social),
        (Host(@"whatsapp\.com|wa\.me"), "WhatsApp", Social),
        (Host(@"t\.me|telegram\.org"), "Telegram", Social),
        (Host(@"reddit\.com"), "Reddit", Social),
        (Host(@"pinterest(\.[a-z]{2,3}){1,2}"), "Pinterest", Social),
    ];

    private static Regex Host(string domains) => new($@"(^|\.)({domains})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Classifies the start of a visit. <paramref name="campaign"/> is the page's utm_source (links in SMS, e-mail or
    /// posters); otherwise the referring site decides. Links from this site itself count as direct.
    /// </summary>
    public static TrafficSource Source(string? referrer, string? ownHost, string? campaign)
    {
        if (Clean(campaign, 100) is { Length: > 0 } tag) return new(Campaign, tag.ToLowerInvariant());
        if (!Uri.TryCreate(referrer, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return new(Direct, "");
        var host = Bare(uri.Host);
        if (ownHost is not null && host == Bare(ownHost)) return new(Direct, "");
        foreach (var (pattern, name, medium) in Known)
        {
            if (pattern.IsMatch(host)) return new(medium, name);
        }
        return new(Referral, host.Length > 100 ? host[..100] : host);
    }

    private static string Bare(string host)
    {
        host = host.ToLowerInvariant().TrimEnd('.');
        foreach (var prefix in new[] { "www.", "m.", "l.", "lm." })
        {
            if (host.StartsWith(prefix, StringComparison.Ordinal) && host.Length > prefix.Length + 3) return host[prefix.Length..];
        }
        return host;
    }

    /// <summary>A short label from the address bar (utm_campaign / utm_source): printable characters only, trimmed.</summary>
    public static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = new string(value.Where(c => !char.IsControl(c) && c != '|').ToArray()).Trim();
        if (text.Length == 0) return null;
        return text.Length > max ? text[..max] : text;
    }
}
