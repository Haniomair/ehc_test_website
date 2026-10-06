namespace EHC.Web.Stats;

/// <summary>One page view as the real-time view sees it. Kept in memory for 30 minutes, never stored.</summary>
public sealed record LiveHit(DateTime Utc, string Visitor, bool NewVisit, Guid PageKey, string Culture, GeoPlace Place, string? Medium, string? Source, string Device);

/// <summary>A count for one value: visitors on the site now, or visits started in the last 30 minutes (sources).</summary>
public sealed record LiveItem(string Value, int Count, string? Label = null, string? Url = null);

/// <summary>A city with visitors now; Lat / Lon is the city's rounded position (null when the location file has none).</summary>
public sealed record LivePlace(string Country, string Region, string City, double? Lat, double? Lon, int Count);

public sealed record LiveSnapshot(
    int Active,
    IReadOnlyList<int> PerMinute,
    IReadOnlyList<LivePlace> Places,
    IReadOnlyList<LiveItem> Countries,
    IReadOnlyList<LiveItem> Pages,
    IReadOnlyList<LiveItem> Sources,
    IReadOnlyList<LiveItem> Devices);

/// <summary>
/// The last 30 minutes of page views, in memory only (cleared on restart; each server sees its own visitors). A visitor
/// is "on the site now" with a page view in the last 5 minutes, and is counted at the page they viewed last.
/// </summary>
public sealed class StatsLive(TimeProvider clock)
{
    public static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(5);
    public const int Minutes = 30;
    private const int MaxHits = 100_000;

    private readonly Queue<LiveHit> _hits = new();
    private readonly Lock _lock = new();

    public void Add(LiveHit hit)
    {
        lock (_lock)
        {
            _hits.Enqueue(hit);
            Prune(hit.Utc);
        }
    }

    private void Prune(DateTime nowUtc)
    {
        var cutoff = nowUtc.AddMinutes(-Minutes);
        while (_hits.Count > 0 && (_hits.Peek().Utc < cutoff || _hits.Count > MaxHits)) _hits.Dequeue();
    }

    public LiveSnapshot Snapshot(int top = 10)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        LiveHit[] hits;
        lock (_lock)
        {
            Prune(now);
            hits = _hits.ToArray();
        }

        var perMinute = new int[Minutes];
        foreach (var h in hits)
        {
            var ago = (int)(now - h.Utc).TotalMinutes;
            if (ago is >= 0 and < Minutes) perMinute[Minutes - 1 - ago]++;
        }

        var current = hits.Where(h => h.Utc >= now - ActiveWindow)
            .GroupBy(h => h.Visitor)
            .Select(g => g.MaxBy(h => h.Utc)!)
            .ToList();

        static List<LiveItem> Count(IEnumerable<string> values, int take) => values
            .GroupBy(v => v)
            .Select(g => new LiveItem(g.Key, g.Count()))
            .OrderByDescending(i => i.Count).ThenBy(i => i.Value, StringComparer.Ordinal)
            .Take(take).ToList();

        var places = current.Where(h => h.Place is { Country: not null, City: not null })
            .GroupBy(h => (h.Place.Country!, h.Place.Region ?? "", h.Place.City!))
            .Select(g =>
            {
                var located = g.Where(h => h.Place.Lat is not null && h.Place.Lon is not null).ToList();
                return new LivePlace(g.Key.Item1, g.Key.Item2, g.Key.Item3,
                    located.Count == 0 ? null : Math.Round(located.Average(h => h.Place.Lat!.Value), 2),
                    located.Count == 0 ? null : Math.Round(located.Average(h => h.Place.Lon!.Value), 2),
                    g.Count());
            })
            .OrderByDescending(p => p.Count).ThenBy(p => p.City, StringComparer.Ordinal)
            .ToList();

        return new LiveSnapshot(
            current.Count,
            perMinute,
            places,
            Count(current.Select(h => h.Place.Country ?? ""), 50),
            Count(current.Select(h => StatsStore.PageValue(h.PageKey, h.Culture)), top),
            Count(hits.Where(h => h.NewVisit).Select(h => $"{h.Medium}|{h.Source}"), top),
            Count(current.Select(h => h.Device), 5));
    }
}
