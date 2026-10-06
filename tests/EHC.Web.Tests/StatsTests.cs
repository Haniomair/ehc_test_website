using EHC.Web.Stats;

public class StatsTests
{
    private const string ChromeWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";
    private const string SafariIphone = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.5 Mobile/15E148 Safari/604.1";
    private const string SafariMac = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.5 Safari/605.1.15";
    private const string ChromeAndroidPhone = "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Mobile Safari/537.36";
    private const string ChromeAndroidTablet = "Mozilla/5.0 (Linux; Android 14; SM-X710) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";
    private const string SamsungPhone = "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/27.0 Chrome/125.0.0.0 Mobile Safari/537.36";
    private const string EdgeWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0";
    private const string FirefoxLinux = "Mozilla/5.0 (X11; Linux x86_64; rv:142.0) Gecko/20100101 Firefox/142.0";
    private const string InstagramIphone = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148 Instagram 350.0.0.0";

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/140.0.0.0 Safari/537.36")]
    [InlineData("facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)")]
    [InlineData("curl/8.9.1")]
    [InlineData("python-requests/2.32")]
    [InlineData("")]
    [InlineData(null)]
    public void Automated_clients_are_not_counted(string? ua) => Assert.True(StatsTraffic.IsBot(ua));

    [Theory]
    [InlineData(ChromeWindows)]
    [InlineData(SafariIphone)]
    [InlineData(SamsungPhone)]
    [InlineData(InstagramIphone)]
    public void Real_browsers_are_counted(string ua) => Assert.False(StatsTraffic.IsBot(ua));

    [Theory]
    [InlineData(ChromeWindows, false, "desktop")]
    [InlineData(SafariIphone, true, "mobile")]
    [InlineData(ChromeAndroidPhone, true, "mobile")]
    [InlineData(ChromeAndroidTablet, true, "tablet")]
    [InlineData(SafariMac, false, "desktop")]
    [InlineData(SafariMac, true, "tablet")]      // iPadOS presents itself as a Mac, but has a touch screen
    public void Device_class(string ua, bool touch, string device) => Assert.Equal(device, StatsTraffic.Device(ua, touch));

    [Theory]
    [InlineData(ChromeWindows, "Chrome")]
    [InlineData(EdgeWindows, "Edge")]
    [InlineData(SafariIphone, "Safari")]
    [InlineData(SamsungPhone, "Samsung Internet")]
    [InlineData(FirefoxLinux, "Firefox")]
    [InlineData(InstagramIphone, "In-app browser")]
    public void Browser_family(string ua, string browser) => Assert.Equal(browser, StatsTraffic.Browser(ua));

    [Theory]
    [InlineData(ChromeWindows, false, "Windows")]
    [InlineData(SafariIphone, true, "iOS")]
    [InlineData(SafariMac, false, "macOS")]
    [InlineData(SafariMac, true, "iOS")]
    [InlineData(ChromeAndroidPhone, true, "Android")]
    [InlineData(FirefoxLinux, false, "Linux")]
    public void Operating_system_family(string ua, bool touch, string os) => Assert.Equal(os, StatsTraffic.OperatingSystem(ua, touch));

    [Theory]
    [InlineData(null, null, "direct", "")]
    [InlineData("", null, "direct", "")]
    [InlineData("https://ehc.med.sa/ar/about/", null, "direct", "")]          // from this site after a break
    [InlineData("https://www.ehc.med.sa/", null, "direct", "")]
    [InlineData("https://www.google.com/", null, "search", "Google")]
    [InlineData("https://www.google.com.sa/", null, "search", "Google")]
    [InlineData("https://www.google.co.uk/", null, "search", "Google")]
    [InlineData("android-app://com.google.android.googlequicksearchbox/", null, "search", "Google")]
    [InlineData("https://www.bing.com/", null, "search", "Bing")]
    [InlineData("https://t.co/", null, "social", "X")]
    [InlineData("https://l.facebook.com/", null, "social", "Facebook")]
    [InlineData("https://www.moh.gov.sa/en/Pages/default.aspx", null, "referral", "moh.gov.sa")]
    [InlineData("https://www.google.com/", "SMS ", "campaign", "sms")]          // utm_source wins
    [InlineData(null, "poster|x", "campaign", "posterx")]                      // the separator never reaches a stored value
    [InlineData("not a url", null, "direct", "")]
    public void Traffic_source(string? referrer, string? campaign, string medium, string source)
    {
        var s = StatsTraffic.Source(referrer, "ehc.med.sa", campaign);
        Assert.Equal(medium, s.Medium);
        Assert.Equal(source, s.Source);
    }

    [Fact]
    public void Visitor_hash_is_stable_within_a_day_and_changes_with_the_salt()
    {
        var today = new byte[32];
        var tomorrow = Enumerable.Repeat((byte)1, 32).ToArray();
        var a = StatsVisitors.Hash(today, "203.0.113.7", ChromeWindows);
        Assert.Equal(16, a.Length);
        Assert.Equal(a, StatsVisitors.Hash(today, "203.0.113.7", ChromeWindows));
        Assert.NotEqual(a, StatsVisitors.Hash(today, "203.0.113.8", ChromeWindows));
        Assert.NotEqual(a, StatsVisitors.Hash(tomorrow, "203.0.113.7", ChromeWindows));
    }

    [Theory]
    [InlineData("Riyadh (Al Raed)", "Riyadh")]
    [InlineData("Dammam", "Dammam")]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void Only_the_city_is_kept(string? name, string? city) => Assert.Equal(city, StatsGeo.CityOnly(name));

    [Fact]
    public void Days_follow_Saudi_time()
    {
        var calendar = new StatsCalendar(StatsCalendar.Zone("Asia/Riyadh"), TimeProvider.System);
        Assert.Equal(new DateOnly(2026, 10, 6), calendar.DayOf(new DateTime(2026, 10, 5, 21, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 10, 5), calendar.DayOf(new DateTime(2026, 10, 5, 20, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 10, 5, 21, 0, 0), calendar.StartUtc(new DateOnly(2026, 10, 6)));
    }

    [Fact]
    public void Unknown_time_zone_falls_back_to_utc_plus_3() =>
        Assert.Equal(TimeSpan.FromHours(3), StatsCalendar.Zone("Nowhere/Nothing").BaseUtcOffset);

    [Fact]
    public void Report_range_defaults_to_30_days_and_never_passes_today()
    {
        var today = new DateOnly(2026, 10, 6);
        Assert.Equal((new DateOnly(2026, 9, 7), today), StatsManagementController.Range(null, null, today));
        Assert.Equal((today, today), StatsManagementController.Range(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), today));
        var (from, to) = StatsManagementController.Range(new DateOnly(2020, 1, 1), today, today);
        Assert.Equal(StatsManagementController.MaxDays, to.DayNumber - from.DayNumber + 1);
    }

    [Fact]
    public void Report_adds_live_days_to_stored_days_and_fills_gaps_with_zero()
    {
        var clock = new FixedClock(new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc));   // 12:00 in Riyadh
        var calendar = new StatsCalendar(StatsCalendar.Zone("Asia/Riyadh"), clock);
        var store = new FakeStore(calendar)
        {
            Rolled = new DateOnly(2026, 10, 4),
            Stored =
            {
                [new DateOnly(2026, 10, 3)] = [new("total", "", 10, 4, 3), new("country", "SA", 10, 4, 3)],
                [new DateOnly(2026, 10, 4)] = [new("total", "", 6, 2, 2), new("country", "SA", 5, 2, 2), new("country", "", 1, 0, 1)],
            },
            Live =
            {
                [new DateOnly(2026, 10, 6)] = [new("total", "", 3, 1, 1), new("country", "SA", 3, 1, 1)],
            },
        };

        var range = new StatsReports(store, calendar).Load(new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 9));

        Assert.Equal(new StatsRow("total", "", 19, 7, 6), range.Total);
        Assert.Equal(new StatsRow("country", "SA", 18, 7, 6), range.Rows.Single(r => r is { Dimension: "country", Value: "SA" }));
        Assert.Equal(5, range.Days.Count);   // 2–6 October: never past today
        Assert.Equal([0, 10, 6, 0, 3], range.Days.Select(d => d.Views));
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private sealed class FakeStore(StatsCalendar calendar) : IStatsStore
    {
        public DateOnly? Rolled { get; init; }
        public Dictionary<DateOnly, List<StatsRow>> Stored { get; } = [];
        public Dictionary<DateOnly, List<StatsRow>> Live { get; } = [];

        public void Add(IReadOnlyCollection<StatsHitDto> hits) { }

        public IReadOnlyList<StatsRow> Aggregate(DateTime fromUtc, DateTime toUtc, bool totalOnly = false)
        {
            var rows = Live.GetValueOrDefault(calendar.DayOf(fromUtc)) ?? [new("total", "", 0, 0, 0)];
            return totalOnly ? rows.Where(r => r.Dimension == "total").ToList() : rows;
        }

        public void SaveDay(DateOnly day, IReadOnlyList<StatsRow> rows) { }

        public IReadOnlyList<StatsRow> DailySums(DateOnly from, DateOnly to, bool totalOnly = false) =>
            Stored.Where(d => d.Key >= from && d.Key <= to).SelectMany(d => d.Value)
                .Where(r => !totalOnly || r.Dimension == "total")
                .GroupBy(r => (r.Dimension, r.Value))
                .Select(g => new StatsRow(g.Key.Dimension, g.Key.Value, g.Sum(r => r.Views), g.Sum(r => r.Visits), g.Sum(r => r.Visitors)))
                .ToList();

        public IReadOnlyList<StatsDayRow> DailyTotals(DateOnly from, DateOnly to) =>
            Stored.Where(d => d.Key >= from && d.Key <= to)
                .Select(d => d.Value.Single(r => r.Dimension == "total") is var t ? new StatsDayRow(d.Key, t.Views, t.Visits, t.Visitors) : null!)
                .ToList();

        public DateOnly? RolledUpTo() => Rolled;
        public DateTime? FirstHitUtc() => calendar.StartUtc(new DateOnly(2026, 10, 5));
        public int DeleteHitsBefore(DateTime cutoffUtc) => 0;
    }
}
