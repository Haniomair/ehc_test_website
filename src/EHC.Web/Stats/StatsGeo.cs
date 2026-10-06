using System.IO.Compression;
using System.Net;
using MaxMind.Db;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace EHC.Web.Stats;

/// <summary>
/// Country (ISO code) for everyone; region, city and the city's position only for the countries in
/// Ehc:Stats:CityCountries. The position is rounded to 0.1° (about 10 km) and used only by the in-memory real-time map.
/// </summary>
public sealed record GeoPlace(string? Country, string? Region, string? City, double? Lat = null, double? Lon = null)
{
    public static readonly GeoPlace Unknown = new(null, null, null);
}

public interface IStatsGeo
{
    GeoPlace Lookup(IPAddress? address);
    /// <summary>Build date of the loaded file; null when no file is loaded (everyone is then counted as "unknown").</summary>
    DateTime? Built { get; }
    string Folder { get; }
    void Reload();
}

/// <summary>
/// IP-to-location lookups against a local .mmdb file (DB-IP "IP to City Lite", CC BY 4.0), read in-process: addresses
/// are never sent anywhere. The newest *.mmdb in the folder is used; older ones are deleted after a reload.
/// </summary>
public sealed class StatsGeo : IStatsGeo, IDisposable
{
    private readonly HashSet<string> _cityCountries;
    private readonly ILogger<StatsGeo> _logger;
    private volatile Reader? _reader;

    public StatsGeo(IOptions<StatsOptions> options, IHostEnvironment env, ILogger<StatsGeo> logger)
    {
        _logger = logger;
        _cityCountries = new HashSet<string>(options.Value.CityCountries, StringComparer.OrdinalIgnoreCase);
        Folder = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.GeoFolder));
        Reload();
    }

    public string Folder { get; }

    public DateTime? Built => _reader?.Metadata.BuildDate;

    public void Reload()
    {
        var file = Directory.Exists(Folder)
            ? Directory.GetFiles(Folder, "*.mmdb").OrderByDescending(f => f, StringComparer.Ordinal).FirstOrDefault()
            : null;
        if (file is null) return;
        try
        {
            var old = Interlocked.Exchange(ref _reader, new Reader(file, FileAccessMode.MemoryMapped));
            // lookups in progress may still use the old file for a moment
            if (old is not null) _ = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => { old.Dispose(); DeleteOthers(file); });
            _logger.LogInformation("Visitor statistics: location file {File} loaded", Path.GetFileName(file));
        }
        catch (Exception e) when (e is IOException or InvalidDatabaseException)
        {
            _logger.LogWarning(e, "Visitor statistics: could not open location file {File}", file);
        }
    }

    private void DeleteOthers(string keep)
    {
        foreach (var f in Directory.GetFiles(Folder, "*.mmdb").Where(f => f != keep))
        {
            try { File.Delete(f); } catch (IOException) { /* still in use: next time */ }
        }
    }

    public GeoPlace Lookup(IPAddress? address)
    {
        var reader = _reader;
        if (reader is null || address is null) return GeoPlace.Unknown;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        try
        {
            var data = reader.Find<Dictionary<string, object>>(address);
            var country = Text(data, "country", "iso_code");
            if (country is null || country.Length != 2) return GeoPlace.Unknown;
            if (!_cityCountries.Contains(country)) return new(country, null, null);
            var region = data!.TryGetValue("subdivisions", out var s) && s is IList<object> { Count: > 0 } list && list[0] is IDictionary<string, object> first
                ? Name(first) : null;
            var city = data.TryGetValue("city", out var c) && c is IDictionary<string, object> cd ? Name(cd) : null;
            var (lat, lon) = data.TryGetValue("location", out var l) && l is IDictionary<string, object> loc
                && loc.TryGetValue("latitude", out var la) && la is double latitude && loc.TryGetValue("longitude", out var lo) && lo is double longitude
                ? (Math.Round(latitude, 1), Math.Round(longitude, 1)) : ((double?)null, (double?)null);
            return new(country, Clip(region), Clip(CityOnly(city)), lat, lon);
        }
        catch (Exception e) when (e is InvalidDatabaseException or ObjectDisposedException or ArgumentException)
        {
            return GeoPlace.Unknown;
        }
    }

    /// <summary>The file names some cities with a district ("Riyadh (Al Raed)"); only the city is kept.</summary>
    public static string? CityOnly(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var cut = name.IndexOf(" (", StringComparison.Ordinal);
        return (cut > 0 ? name[..cut] : name).Trim();
    }

    private static string? Clip(string? s) => s is null ? null : s.Length > 60 ? s[..60] : s;

    private static string? Name(IDictionary<string, object> node) =>
        node.TryGetValue("names", out var n) && n is IDictionary<string, object> names && names.TryGetValue("en", out var en) ? en as string : null;

    private static string? Text(IDictionary<string, object>? data, string node, string field) =>
        data is not null && data.TryGetValue(node, out var n) && n is IDictionary<string, object> d && d.TryGetValue(field, out var v) ? v as string : null;

    public void Dispose() => _reader?.Dispose();
}

/// <summary>
/// Keeps the location file current: once a day, if the loaded file is not from this month (or there is none), downloads
/// DB-IP's free monthly file, checks that it opens, and switches to it. Runs on every server (each has its own copy).
/// </summary>
public sealed class StatsGeoUpdateJob(
    IStatsGeo geo,
    IHttpClientFactory http,
    IOptions<StatsOptions> options,
    TimeProvider clock,
    ILogger<StatsGeoUpdateJob> logger) : IRecurringBackgroundJob
{
    public const string HttpClientName = "ehc-geo";

    public TimeSpan Period => TimeSpan.FromDays(1);
    public TimeSpan Delay => TimeSpan.FromMinutes(1);
    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher, ServerRole.Subscriber];

    public event EventHandler PeriodChanged { add { } remove { } }

    public async Task RunJobAsync()
    {
        var o = options.Value;
        if (!o.Enabled || !o.GeoAutoUpdate) return;
        var now = clock.GetUtcNow().UtcDateTime;
        var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (geo.Built is { } built && built >= thisMonth) return;

        // a new month's file appears in its first days; until then last month's will do (only if there is no file yet)
        var months = geo.Built is null ? new[] { thisMonth, thisMonth.AddMonths(-1) } : [thisMonth];
        foreach (var month in months)
        {
            if (await TryDownload(string.Format(System.Globalization.CultureInfo.InvariantCulture, o.GeoDownloadUrl, month), month)) return;
        }
    }

    private async Task<bool> TryDownload(string url, DateTime month)
    {
        Directory.CreateDirectory(geo.Folder);
        var target = Path.Combine(geo.Folder, $"dbip-city-lite-{month:yyyy-MM}.mmdb");
        var temp = target + ".download";
        try
        {
            using var client = http.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            response.EnsureSuccessStatusCode();
            await using (var body = await response.Content.ReadAsStreamAsync())
            await using (var gzip = new GZipStream(body, CompressionMode.Decompress))
            await using (var file = File.Create(temp))
            {
                await gzip.CopyToAsync(file);
            }
            using (var check = new Reader(temp, FileAccessMode.MemoryMapped))
            {
                if (check.Find<Dictionary<string, object>>(IPAddress.Parse("8.8.8.8")) is null) throw new InvalidDatabaseException("test lookup failed");
            }
            File.Move(temp, target, overwrite: true);
            geo.Reload();
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDatabaseException or TaskCanceledException or InvalidDataException)
        {
            logger.LogWarning(e, "Visitor statistics: could not update the location file from {Url}", url);
            return false;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
