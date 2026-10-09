using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Umbraco.Cms.Infrastructure.Scoping;

namespace EHC.Web.Contact;

public sealed class ContactOptions
{
    /// <summary>Messages older than this are deleted daily.</summary>
    public int RetentionMonths { get; set; } = 12;
}

public sealed class ContactFilter
{
    public DateTime SinceUtc { get; init; }
    public string? Service { get; init; }
    public string? Status { get; init; }
}

public sealed class ContactCount
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
}

public interface IContactStore
{
    /// <summary>Stores the message with a new unique reference and returns that reference.</summary>
    Task<string> AddAsync(ContactMessageDto message);
    IReadOnlyList<ContactMessageDto> List(ContactFilter filter, int take);
    IReadOnlyList<ContactCount> Counts(DateTime sinceUtc);
    ContactMessageDto? Get(int id);
    bool SetStatus(int id, string status, DateTime nowUtc);
    int DeleteOlderThan(DateTime cutoffUtc);
}

public sealed class ContactStore(IScopeProvider scopes) : IContactStore
{
    public async Task<string> AddAsync(ContactMessageDto message)
    {
        using var scope = scopes.CreateScope();
        for (var attempt = 0; ; attempt++)
        {
            message.Reference = ContactText.NewReference(message.CreatedUtc);
            var taken = scope.Database.ExecuteScalar<int>($"SELECT COUNT(*) FROM {ContactMessageDto.Table} WHERE reference = @0", message.Reference);
            if (taken == 0 || attempt == 4) break;
        }
        await scope.Database.InsertAsync(message);
        scope.Complete();
        return message.Reference;
    }

    public IReadOnlyList<ContactMessageDto> List(ContactFilter filter, int take)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        var sql = scope.SqlContext.Sql().SelectAll().From<ContactMessageDto>().Where<ContactMessageDto>(x => x.CreatedUtc >= filter.SinceUtc);
        if (filter.Service is { } service) sql = sql.Where<ContactMessageDto>(x => x.Service == service);
        if (filter.Status is { } status) sql = sql.Where<ContactMessageDto>(x => x.Status == status);
        sql = sql.OrderByDescending<ContactMessageDto>(x => x.CreatedUtc);
        return scope.Database.SkipTake<ContactMessageDto>(0, Math.Clamp(take, 1, 5000), sql);
    }

    public IReadOnlyList<ContactCount> Counts(DateTime sinceUtc)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.Fetch<ContactCount>(
            $"SELECT status AS Status, COUNT(*) AS Count FROM {ContactMessageDto.Table} WHERE createdUtc >= @0 GROUP BY status", sinceUtc);
    }

    public ContactMessageDto? Get(int id)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.SingleOrDefaultById<ContactMessageDto>(id);
    }

    public bool SetStatus(int id, string status, DateTime nowUtc)
    {
        using var scope = scopes.CreateScope();
        var n = scope.Database.Execute($"UPDATE {ContactMessageDto.Table} SET status = @0, updatedUtc = @1 WHERE id = @2", status, nowUtc, id);
        scope.Complete();
        return n > 0;
    }

    public int DeleteOlderThan(DateTime cutoffUtc)
    {
        using var scope = scopes.CreateScope();
        var n = scope.Database.Execute($"DELETE FROM {ContactMessageDto.Table} WHERE createdUtc < @0", cutoffUtc);
        scope.Complete();
        return n;
    }
}

/// <summary>A choice in "What is your message about?". Code = the service code of the EHC helpdesk (Frappe).</summary>
public sealed record ContactService(string Key, string Code, string Icon);

/// <summary>Input rules for the contact form: fixed services and statuses, cleaned and length-checked text fields.</summary>
public static partial class ContactText
{
    public const int MinName = 2, MaxName = 100;
    public const int MaxEmail = 254;
    public const int MinMessage = 10, MaxMessage = 2000;

    public const string StatusNew = "new";
    public static readonly string[] Statuses = [StatusNew, "progress", "closed"];

    // same choices and helpdesk codes as the form on ehc.med.sa
    public static readonly IReadOnlyList<ContactService> Services =
    [
        new("complaint", "GEN-SER005", "flag"),
        new("question", "GEN-SER004", "chat"),
        new("suggestion", "GEN-SER003", "bulb"),
        new("volunteering", "GEN-SER008", "hand"),
        new("research", "GEN-SER007", "lab"),
        new("other", "GEN-SER002", "note"),
    ];

    public static string? Service(string? value) => Services.FirstOrDefault(s => s.Key == value)?.Key;

    public static string? Status(string? value) => value is not null && Statuses.Contains(value) ? value : null;

    /// <summary>Single line, control characters removed, spaces collapsed; null when too short / long or without a letter.</summary>
    public static string? Name(string? value)
    {
        var s = Line(value);
        return s is null || s.Length < MinName || s.Length > MaxName || !s.Any(char.IsLetter) ? null : s;
    }

    public static string? Email(string? value)
    {
        var s = Line(value)?.ToLowerInvariant();
        if (s is null || s.Length > MaxEmail || !EmailShape().IsMatch(s)) return null;
        return MailAddress.TryCreate(s, out var parsed) && parsed.Address == s ? s : null;
    }

    /// <summary>
    /// Saudi numbers in any common form (05x…, 5x…, +966 / 00966 / 966 5x…, landlines 01x…) become 0XXXXXXXXX; other
    /// countries must start with + or 00 and become +digits. Arabic-Indic digits, spaces, dashes, dots and brackets are
    /// accepted. Null when it is not a phone number.
    /// </summary>
    public static string? Phone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 30) return null;
        var sb = new StringBuilder();
        foreach (var ch in value.Trim())
        {
            if (char.IsDigit(ch)) sb.Append((char)('0' + (int)char.GetNumericValue(ch)));
            else if (ch == '+' && sb.Length == 0) sb.Append('+');
            else if (ch is not (' ' or '-' or '.' or '(' or ')' or ' ')) return null;
        }
        var s = sb.ToString();
        if (s.StartsWith("00")) s = "+" + s[2..];
        if (s.StartsWith("+966")) s = "0" + s[4..];
        else if (s.StartsWith("966") && s.Length == 12) s = "0" + s[3..];
        else if (s.Length == 9 && s[0] == '5') s = "0" + s;
        if (s.StartsWith('+')) return s.Length is >= 9 and <= 16 && s[1] != '0' ? s : null;
        return SaudiNumber().IsMatch(s) ? s : null;
    }

    /// <summary>Message text: control characters become spaces (line breaks kept), at most one blank line in a row.</summary>
    public static string? Message(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = new string(value.Replace("\r\n", "\n").Select(ch => ch != '\n' && char.IsControl(ch) ? ' ' : ch).ToArray());
        s = BlankLines().Replace(Spaces().Replace(s, " "), "\n\n").Trim();
        return s.Length < MinMessage || s.Length > MaxMessage ? null : s;
    }

    /// <summary>Reference shown to the visitor, e.g. EHC-261009-K7MQ4: date + five characters that are never confused when read aloud.</summary>
    public static string NewReference(DateTime createdUtc)
    {
        const string alphabet = "ACDEFGHJKMNPQRTUVWXY34679";
        Span<char> code = stackalloc char[5];
        for (var i = 0; i < code.Length; i++) code[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return $"EHC-{createdUtc:yyMMdd}-{code}";
    }

    private static string? Line(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = Spaces().Replace(new string(value.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray()), " ").Trim();
        return s.Length == 0 ? null : s;
    }

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n[ \n]*\n")]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$")]
    private static partial Regex EmailShape();

    // mobile 05XXXXXXXX or landline 01X XXXXXXX (both 10 digits)
    [GeneratedRegex(@"^0(5\d|1[1-7])\d{7}$")]
    private static partial Regex SaudiNumber();
}
