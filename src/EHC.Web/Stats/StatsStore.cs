using System.Globalization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace EHC.Web.Stats;

public sealed class StatsOptions
{
    /// <summary>Off switch for counting (the dashboard keeps showing what was collected).</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Count only visitors who accepted optional cookies. Off by default: counting uses no cookies or stored ids.</summary>
    public bool RequireConsent { get; set; }
    /// <summary>Single page views are deleted after this many days; the daily totals are kept.</summary>
    public int RawRetentionDays { get; set; } = 30;
    /// <summary>Days start and end at midnight in this time zone.</summary>
    public string TimeZone { get; set; } = "Asia/Riyadh";
    /// <summary>Countries (ISO codes) for which region and city are recorded; everyone else is counted by country only.</summary>
    public string[] CityCountries { get; set; } = ["SA"];
    /// <summary>Folder (relative to the site root) holding the IP-to-location file (*.mmdb).</summary>
    public string GeoFolder { get; set; } = "umbraco/Data/Geo";
    /// <summary>Download the free DB-IP "IP to City Lite" file each month. Off: place a .mmdb file in GeoFolder by hand.</summary>
    public bool GeoAutoUpdate { get; set; } = true;
    /// <summary>{0} is the month (yyyy-MM).</summary>
    public string GeoDownloadUrl { get; set; } = "https://download.db-ip.com/free/dbip-city-lite-{0:yyyy-MM}.mmdb.gz";
}

/// <summary>Calendar days in the site's time zone (Saudi Arabia has no daylight saving; the fallback is UTC+3).</summary>
public sealed class StatsCalendar(TimeZoneInfo zone, TimeProvider clock)
{
    public static TimeZoneInfo Zone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");
        }
    }

    public DateOnly Today => DayOf(clock.GetUtcNow().UtcDateTime);

    public DateOnly DayOf(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));

    public DateTime StartUtc(DateOnly day) => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), zone);
}

/// <summary>Totals for one dimension value. Value "" means unknown (or, for "total", the whole site).</summary>
public sealed record StatsRow(string Dimension, string Value, int Views, int Visits, int Visitors);

public sealed record StatsDayRow(DateOnly Day, int Views, int Visits, int Visitors);

public interface IStatsStore
{
    void Add(IReadOnlyCollection<StatsHitDto> hits);
    /// <summary>Totals per dimension straight from the page views in [from, to).</summary>
    IReadOnlyList<StatsRow> Aggregate(DateTime fromUtc, DateTime toUtc, bool totalOnly = false);
    /// <summary>Replaces the stored totals of one day and marks every day up to it as rolled up.</summary>
    void SaveDay(DateOnly day, IReadOnlyList<StatsRow> rows);
    /// <summary>Stored daily totals summed over [from, to] (visitors are summed per day).</summary>
    IReadOnlyList<StatsRow> DailySums(DateOnly from, DateOnly to, bool totalOnly = false);
    IReadOnlyList<StatsDayRow> DailyTotals(DateOnly from, DateOnly to);
    DateOnly? RolledUpTo();
    DateTime? FirstHitUtc();
    int DeleteHitsBefore(DateTime cutoffUtc);
}

public sealed class StatsStore(IScopeProvider scopes, IKeyValueService keyValues) : IStatsStore
{
    public const string Total = "total";
    public const string Page = "page";
    private const string RolledUpKey = "Ehc.Stats.RolledUpTo";

    /// <summary>
    /// Dimension name → hit columns making up its value (joined with "|") and an optional filter. Traffic sources are
    /// counted on the first page of each visit only.
    /// </summary>
    public static readonly (string Name, string[] Columns, string? Where)[] Dimensions =
    [
        ("culture", ["culture"], null),
        ("country", ["country"], null),
        ("region", ["country", "region"], "region IS NOT NULL"),
        ("city", ["country", "region", "city"], "city IS NOT NULL"),
        ("device", ["device"], null),
        ("browser", ["browser"], null),
        ("os", ["os"], null),
        ("medium", ["medium"], "newVisit = 1"),
        ("source", ["medium", "source"], "newVisit = 1"),
        ("campaign", ["source", "campaign"], "newVisit = 1 AND campaign IS NOT NULL"),
    ];

    private const string Counts =
        "COUNT(*) AS Views, COALESCE(SUM(CASE WHEN newVisit = 1 THEN 1 ELSE 0 END), 0) AS Visits, COUNT(DISTINCT visitor) AS Visitors";

    public void Add(IReadOnlyCollection<StatsHitDto> hits)
    {
        if (hits.Count == 0) return;
        using var scope = scopes.CreateScope();
        scope.Database.InsertBulk(hits);
        scope.Complete();
    }

    public IReadOnlyList<StatsRow> Aggregate(DateTime fromUtc, DateTime toUtc, bool totalOnly = false)
    {
        const string range = "createdUtc >= @0 AND createdUtc < @1";
        var rows = new List<StatsRow>();
        using var scope = scopes.CreateScope(autoComplete: true);
        var db = scope.Database;

        var total = db.Fetch<CountRow>($"SELECT {Counts} FROM {StatsHitDto.Table} WHERE {range}", fromUtc, toUtc).FirstOrDefault();
        rows.Add(new StatsRow(Total, "", total?.Views ?? 0, total?.Visits ?? 0, total?.Visitors ?? 0));
        if (totalOnly || total is null || total.Views == 0) return rows;

        // page key and language together: the Arabic and English versions of a page are reported separately
        rows.AddRange(db.Fetch<PageRow>(
                $"SELECT pageKey AS PageKey, culture AS Culture, {Counts} FROM {StatsHitDto.Table} WHERE {range} GROUP BY pageKey, culture", fromUtc, toUtc)
            .Select(r => new StatsRow(Page, PageValue(r.PageKey, r.Culture), r.Views, r.Visits, r.Visitors)));

        foreach (var (name, columns, where) in Dimensions)
        {
            var select = string.Join(", ", columns.Select((c, i) => $"{c} AS C{i}"));
            var group = string.Join(", ", columns);
            var sql = $"SELECT {select}, {Counts} FROM {StatsHitDto.Table} WHERE {range}{(where is null ? "" : " AND " + where)} GROUP BY {group}";
            rows.AddRange(db.Fetch<GroupRow>(sql, fromUtc, toUtc)
                .Select(r => new StatsRow(name, string.Join('|', new[] { r.C0, r.C1, r.C2 }.Take(columns.Length).Select(v => v ?? "")), r.Views, r.Visits, r.Visitors)));
        }
        return rows;
    }

    public static string PageValue(Guid key, string culture) => $"{key:D}|{culture}";

    public void SaveDay(DateOnly day, IReadOnlyList<StatsRow> rows)
    {
        var date = day.ToDateTime(TimeOnly.MinValue);
        using var scope = scopes.CreateScope();
        scope.Database.Execute($"DELETE FROM {StatsDailyDto.Table} WHERE day = @0", date);
        scope.Database.InsertBulk(rows.Select(r => new StatsDailyDto
        {
            Day = date,
            Dimension = r.Dimension,
            Value = r.Value.Length > 200 ? r.Value[..200] : r.Value,
            Views = r.Views,
            Visits = r.Visits,
            Visitors = r.Visitors,
        }));
        keyValues.SetValue(RolledUpKey, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        scope.Complete();
    }

    public IReadOnlyList<StatsRow> DailySums(DateOnly from, DateOnly to, bool totalOnly = false)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.Fetch<SumRow>(
                "SELECT dimension AS Dimension, value AS Value, SUM(views) AS Views, SUM(visits) AS Visits, SUM(visitors) AS Visitors " +
                $"FROM {StatsDailyDto.Table} WHERE day >= @0 AND day <= @1{(totalOnly ? " AND dimension = @2" : "")} GROUP BY dimension, value",
                from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue), Total)
            .Select(r => new StatsRow(r.Dimension, r.Value, r.Views, r.Visits, r.Visitors))
            .ToList();
    }

    public IReadOnlyList<StatsDayRow> DailyTotals(DateOnly from, DateOnly to)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.Fetch<StatsDailyDto>(
                $"SELECT * FROM {StatsDailyDto.Table} WHERE dimension = @0 AND day >= @1 AND day <= @2",
                Total, from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue))
            .Select(r => new StatsDayRow(DateOnly.FromDateTime(r.Day), r.Views, r.Visits, r.Visitors))
            .ToList();
    }

    public DateOnly? RolledUpTo() =>
        DateOnly.TryParseExact(keyValues.GetValue(RolledUpKey), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    public DateTime? FirstHitUtc()
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        // streamed: stops after the first (oldest) row
        return scope.Database.Query<StatsHitDto>($"SELECT * FROM {StatsHitDto.Table} ORDER BY createdUtc").FirstOrDefault()?.CreatedUtc;
    }

    public int DeleteHitsBefore(DateTime cutoffUtc)
    {
        using var scope = scopes.CreateScope();
        var n = scope.Database.Execute($"DELETE FROM {StatsHitDto.Table} WHERE createdUtc < @0", cutoffUtc);
        scope.Complete();
        return n;
    }

    private class CountRow
    {
        public int Views { get; set; }
        public int Visits { get; set; }
        public int Visitors { get; set; }
    }

    private sealed class PageRow : CountRow
    {
        public Guid PageKey { get; set; }
        public string Culture { get; set; } = "";
    }

    private sealed class GroupRow : CountRow
    {
        public string? C0 { get; set; }
        public string? C1 { get; set; }
        public string? C2 { get; set; }
    }

    private sealed class SumRow : CountRow
    {
        public string Dimension { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
