using System.Globalization;
using EHC.Web.Site;

public class CultureTests
{
    private const string DbTimestamp = "2026-09-26 09:21:26.4126671";

    [Fact]
    public void Default_ar_SA_cannot_parse_gregorian_timestamps()
        => Assert.ThrowsAny<FormatException>(() => Convert.ToDateTime(DbTimestamp, CultureInfo.GetCultureInfo("ar-SA")));

    [Fact]
    public void Site_ar_SA_parses_gregorian_timestamps()
    {
        var culture = SiteCultures.Create("ar-SA");
        Assert.IsType<GregorianCalendar>(culture.DateTimeFormat.Calendar);
        Assert.Equal(new DateTime(2026, 9, 26, 9, 21, 26, 412, 667), Convert.ToDateTime(DbTimestamp, culture), TimeSpan.FromMilliseconds(1));
        Assert.True(culture.TextInfo.IsRightToLeft);
    }
}
