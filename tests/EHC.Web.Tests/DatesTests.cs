using System.Globalization;
using EHC.Web.Site;

public class DatesTests
{
    private static readonly UmAlQuraCalendar Cal = new();

    [Theory]
    [InlineData("2026-09-26")]
    [InlineData("2027-03-10")]
    [InlineData("2030-01-01")]
    public void Hijri_matches_umm_al_qura(string iso)
    {
        var date = DateOnly.Parse(iso, CultureInfo.InvariantCulture);
        var dt = date.ToDateTime(TimeOnly.MinValue);
        var ar = Dates.Hijri(date, SiteCultures.Create("ar-SA"))!;
        var en = Dates.Hijri(date, SiteCultures.Create("en-US"))!;
        Assert.StartsWith(Cal.GetDayOfMonth(dt) + " ", ar);
        Assert.EndsWith(Cal.GetYear(dt) + "هـ", ar);
        Assert.EndsWith(Cal.GetYear(dt) + " AH", en);
    }

    [Fact]
    public void Ramadan_is_month_nine()
    {
        // 1 Ramadan 1448 in Umm al-Qura, converted by .NET itself
        var first = DateOnly.FromDateTime(Cal.ToDateTime(1448, 9, 1, 0, 0, 0, 0));
        Assert.Equal("1 رمضان 1448هـ", Dates.Hijri(first, SiteCultures.Create("ar-SA")));
        Assert.Equal("1 Ramadan 1448 AH", Dates.Hijri(first, SiteCultures.Create("en-US")));
    }

    [Fact]
    public void Out_of_range_dates_have_no_hijri() =>
        Assert.Null(Dates.Hijri(new DateOnly(1800, 1, 1), SiteCultures.Create("ar-SA")));
}
