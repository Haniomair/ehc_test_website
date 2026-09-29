using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Html;

namespace EHC.Web.Site;

/// <summary>
/// Dates are shown in the Gregorian calendar with the Umm al-Qura (Hijri) date alongside, e.g.
/// "26 سبتمبر 2026 · 14 ربيع الآخر 1448هـ" / "26 September 2026 · 14 Rabi al-Thani 1448 AH".
/// The request culture uses the Gregorian calendar (see SiteCultures), so Hijri is formatted explicitly here.
/// </summary>
public static class Dates
{
    private static readonly UmAlQuraCalendar UmAlQura = new();
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private static readonly string[] MonthsAr =
    [
        "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
        "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة",
    ];

    private static readonly string[] MonthsEn =
    [
        "Muharram", "Safar", "Rabi al-Awwal", "Rabi al-Thani", "Jumada al-Ula", "Jumada al-Akhirah",
        "Rajab", "Shaban", "Ramadan", "Shawwal", "Dhu al-Qadah", "Dhu al-Hijjah",
    ];

    /// <summary>Hijri month names in the site's spelling (used by the date tools in the browser).</summary>
    public static IReadOnlyList<string> HijriMonths(CultureInfo culture) => culture.TwoLetterISOLanguageName == "ar" ? MonthsAr : MonthsEn;

    /// <summary>Hijri (Umm al-Qura) date as text, or null outside the calendar's supported range.</summary>
    public static string? Hijri(DateOnly date, CultureInfo culture, bool withYear = true)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        if (dt < UmAlQura.MinSupportedDateTime || dt > UmAlQura.MaxSupportedDateTime) return null;

        var day = UmAlQura.GetDayOfMonth(dt);
        var month = UmAlQura.GetMonth(dt);
        var year = UmAlQura.GetYear(dt);
        var arabic = culture.TwoLetterISOLanguageName == "ar";
        var name = (arabic ? MonthsAr : MonthsEn)[month - 1];
        if (!withYear) return $"{day} {name}";
        return arabic ? $"{day} {name} {year}هـ" : $"{day} {name} {year} AH";
    }

    public static string Gregorian(DateOnly date, CultureInfo culture, bool withYear = true) =>
        date.ToString(withYear ? "d MMMM yyyy" : "d MMMM", culture);

    /// <summary>&lt;time datetime="yyyy-MM-dd"&gt;Gregorian&lt;/time&gt; followed by the Hijri date (HTML-encoded).</summary>
    public static IHtmlContent Html(DateOnly date, bool withYear = true, string? hijriClass = null)
    {
        var culture = CultureInfo.CurrentCulture;
        var enc = Encoder;
        var iso = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var html = $"<time datetime=\"{iso}\">{enc.Encode(Gregorian(date, culture, withYear))}</time>";
        var hijri = Hijri(date, culture, withYear);
        if (hijri is not null)
            html += hijriClass is null ? $"<span> · {enc.Encode(hijri)}</span>" : $"<span class=\"{enc.Encode(hijriClass)}\"> · {enc.Encode(hijri)}</span>";
        return new HtmlString(html);
    }

    public static IHtmlContent Html(DateOnly? date, bool withYear = true, string? hijriClass = null) =>
        date is { } d ? Html(d, withYear, hijriClass) : HtmlString.Empty;

    /// <summary>Saudi Arabia is UTC+3 all year (no daylight saving).</summary>
    public static readonly TimeSpan SaudiOffset = TimeSpan.FromHours(3);

    /// <summary>Today's date in Saudi Arabia.</summary>
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SaudiOffset).DateTime);
}
