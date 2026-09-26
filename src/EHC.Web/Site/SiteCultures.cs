using System.Globalization;

namespace EHC.Web.Site;

/// <summary>
/// .NET's ar-SA culture uses the Umm al-Qura (Hijri) calendar by default. Umbraco sets the request culture to ar-SA on
/// Arabic pages, and the data layer (NPoco) parses stored timestamps such as "2026-09-26 09:21:26" with the current
/// culture, so year 2026 fails as a Hijri year (FormatException). The site therefore uses ar-SA with the Gregorian
/// calendar; Arabic names, digits and direction are unchanged. Show Hijri dates explicitly with UmAlQuraCalendar.
/// </summary>
public static class SiteCultures
{
    public static CultureInfo Create(string name)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(name).Clone();
        if (culture.DateTimeFormat.Calendar is not GregorianCalendar)
        {
            var gregorian = culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault() ?? new GregorianCalendar();
            culture.DateTimeFormat.Calendar = gregorian;
        }
        return CultureInfo.ReadOnly(culture);
    }
}
