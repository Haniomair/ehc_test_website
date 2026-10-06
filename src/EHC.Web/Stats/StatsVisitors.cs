using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Umbraco.Cms.Core.Services;

namespace EHC.Web.Stats;

/// <summary>
/// Cookieless visitor counting. The id is a hash of the IP address and user-agent string with a random salt that is
/// replaced at midnight; the old salt is overwritten, so yesterday's ids can no longer be recomputed and a visitor
/// cannot be followed from one day to the next. The IP address and user-agent string themselves are never stored.
/// A visit ends after 30 minutes without a page view (kept in memory only).
/// </summary>
public sealed class StatsVisitors(IKeyValueService keyValues, StatsCalendar calendar) : IDisposable
{
    public static readonly TimeSpan VisitTimeout = TimeSpan.FromMinutes(30);
    private const string SaltKey = "Ehc.Stats.Salt";

    private readonly Lock _lock = new();
    private readonly MemoryCache _seen = new(new MemoryCacheOptions());
    private DateOnly _day;
    private byte[] _salt = [];

    public (string Visitor, bool NewVisit) Identify(string ip, string userAgent)
    {
        var id = Hash(Salt(), ip, userAgent);
        var newVisit = !_seen.TryGetValue(id, out _);
        _seen.Set(id, true, new MemoryCacheEntryOptions { SlidingExpiration = VisitTimeout });
        return (id, newVisit);
    }

    public static string Hash(byte[] salt, string ip, string userAgent)
    {
        var input = Encoding.UTF8.GetBytes(ip + "\n" + userAgent);
        var hash = HMACSHA256.HashData(salt, input);
        return Convert.ToHexStringLower(hash, 0, 8);
    }

    /// <summary>Today's salt, shared through the database so a restart during the day keeps the same ids.</summary>
    private byte[] Salt()
    {
        var today = calendar.Today;
        lock (_lock)
        {
            if (_day == today) return _salt;
            var day = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var stored = keyValues.GetValue(SaltKey)?.Split('|');
            if (stored is [var d, var s] && d == day)
            {
                _salt = Convert.FromBase64String(s);
            }
            else
            {
                _salt = RandomNumberGenerator.GetBytes(32);
                keyValues.SetValue(SaltKey, day + "|" + Convert.ToBase64String(_salt));
            }
            _day = today;
            return _salt;
        }
    }

    public void Dispose() => _seen.Dispose();
}
