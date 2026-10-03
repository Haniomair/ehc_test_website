using Umbraco.Cms.Infrastructure.Scoping;

namespace EHC.Web.Vitals;

public sealed class VitalsOptions
{
    /// <summary>Off switch for the collection script (the dashboard keeps working).</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Share of page views measured (0–1); lower it on busy days to keep the table small.</summary>
    public double SampleRate { get; set; } = 1;
    /// <summary>Values older than this are deleted daily. Google's own figures use the last 28 days.</summary>
    public int RetentionDays { get; set; } = 90;
}

/// <summary>Google's Core Web Vitals thresholds ("good" up to and including, "poor" above) and the accepted input range.</summary>
public static class VitalsMetrics
{
    public static readonly string[] Names = ["LCP", "INP", "CLS"];
    public static readonly string[] Devices = ["mobile", "desktop"];

    public static (double Good, double Poor, double Max) Limits(string metric) => metric switch
    {
        "LCP" => (2500, 4000, 60000),
        "INP" => (200, 500, 60000),
        "CLS" => (0.1, 0.25, 10),
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    public static bool Valid(string metric, double value) =>
        Names.Contains(metric) && double.IsFinite(value) && value >= 0 && value <= Limits(metric).Max;

    public static string Rating(string metric, double p75)
    {
        var (good, poor, _) = Limits(metric);
        return p75 <= good ? "good" : p75 <= poor ? "needs-improvement" : "poor";
    }
}

/// <summary>The 75th percentile (what Google reports), share of good values and sample count for one group.</summary>
public sealed record VitalStat(int Samples, double P75, int GoodPercent);

public sealed class PageSamples
{
    public Guid PageKey { get; set; }
    public int Samples { get; set; }
}

public interface IVitalsStore
{
    Task AddAsync(IReadOnlyList<VitalDto> values);
    VitalStat? Stat(DateTime sinceUtc, string metric, string device, Guid? pageKey = null);
    IReadOnlyList<PageSamples> BusiestPages(DateTime sinceUtc, string device, int take);
    int DeleteOlderThan(DateTime cutoffUtc);
}

public sealed class VitalsStore(IScopeProvider scopes) : IVitalsStore
{
    public async Task AddAsync(IReadOnlyList<VitalDto> values)
    {
        using var scope = scopes.CreateScope();
        foreach (var v in values) await scope.Database.InsertAsync(v);
        scope.Complete();
    }

    public VitalStat? Stat(DateTime sinceUtc, string metric, string device, Guid? pageKey = null)
    {
        var good = VitalsMetrics.Limits(metric).Good;
        using var scope = scopes.CreateScope(autoComplete: true);
        var filter = "createdUtc >= @0 AND metric = @1 AND device = @2" + (pageKey is null ? "" : " AND pageKey = @4");
        var counts = scope.Database.Fetch<CountRow>(
            $"SELECT COUNT(*) AS Total, SUM(CASE WHEN value <= @3 THEN 1 ELSE 0 END) AS Good FROM {VitalDto.Table} WHERE {filter}",
            sinceUtc, metric, device, good, pageKey ?? Guid.Empty).FirstOrDefault();
        if (counts is null || counts.Total == 0) return null;
        // nearest-rank 75th percentile: stream the sorted values and stop there (plain SQL, same on SQLite and SQL Server)
        var rank = (int)Math.Ceiling(0.75 * counts.Total) - 1;
        var p75 = scope.Database.Query<double>(
            $"SELECT value FROM {VitalDto.Table} WHERE {filter} ORDER BY value", sinceUtc, metric, device, good, pageKey ?? Guid.Empty)
            .Skip(rank).FirstOrDefault();
        return new VitalStat(counts.Total, p75, (int)Math.Round(100.0 * counts.Good / counts.Total));
    }

    public IReadOnlyList<PageSamples> BusiestPages(DateTime sinceUtc, string device, int take)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.Fetch<PageSamples>(
            $"SELECT pageKey AS PageKey, COUNT(*) AS Samples FROM {VitalDto.Table} WHERE createdUtc >= @0 AND device = @1 AND metric = 'LCP' " +
            "GROUP BY pageKey ORDER BY COUNT(*) DESC", sinceUtc, device).Take(Math.Clamp(take, 1, 100)).ToList();
    }

    public int DeleteOlderThan(DateTime cutoffUtc)
    {
        using var scope = scopes.CreateScope();
        var n = scope.Database.Execute($"DELETE FROM {VitalDto.Table} WHERE createdUtc < @0", cutoffUtc);
        scope.Complete();
        return n;
    }

    private sealed class CountRow
    {
        public int Total { get; set; }
        public int Good { get; set; }
    }
}
