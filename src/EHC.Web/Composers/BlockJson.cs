using System.Text.Json;

namespace EHC.Web.Composers;

/// <summary>
/// Builds a block list value (the JSON Umbraco stores) for seeders: one block per <see cref="Add"/>, every block
/// exposed in Arabic and English. Page-level lists get an empty shared settings element per block; nested lists
/// (cards, steps, FAQ items…) get none, as when an editor adds them.
/// </summary>
internal sealed class BlockJson(Func<string, Guid> typeKey, bool pageLevel = true)
{
    public const string Ar = "ar-SA", En = "en-US";

    private readonly List<object> _content = [], _settings = [], _expose = [], _layout = [];

    public BlockJson Add(string elementTypeAlias, params BlockField[] fields)
    {
        var key = Guid.NewGuid();
        _content.Add(new { contentTypeKey = typeKey(elementTypeAlias), key, values = fields.SelectMany(f => f.Values).ToArray() });
        _expose.Add(new { contentKey = key, culture = Ar, segment = (string?)null });
        _expose.Add(new { contentKey = key, culture = En, segment = (string?)null });
        if (pageLevel)
        {
            var settings = Guid.NewGuid();
            _settings.Add(new { contentTypeKey = typeKey("blockSettings"), key = settings, values = Array.Empty<object>() });
            _layout.Add(new { contentKey = key, settingsKey = settings });
        }
        else
        {
            _layout.Add(new { contentKey = key });
        }
        return this;
    }

    /// <summary>A list for a block's own item property (no settings).</summary>
    public BlockJson Items() => new(typeKey, pageLevel: false);

    public string Build() => JsonSerializer.Serialize(new
    {
        contentData = _content,
        settingsData = _settings,
        expose = _expose,
        layout = new Dictionary<string, object> { ["Umbraco.BlockList"] = _layout },
    });
}

/// <summary>A link for a link picker: an external address or a page of this site (optionally to a section: Anchor "#id").</summary>
internal sealed record SeedLink(string Name, string? Url = null, Guid? Page = null, bool NewWindow = false, string? Anchor = null)
{
    public object Json() => Page is { } key
        ? (object)new { name = Name, target = (string?)null, unique = key.ToString(), type = "document", udi = $"umb://document/{key:N}", url = (string?)null, queryString = Anchor, culture = (string?)null }
        : new { name = Name, target = NewWindow ? "_blank" : null, unique = (string?)null, type = "external", udi = (string?)null, url = Url, queryString = Anchor, culture = (string?)null };
}

/// <summary>One property value of a seeded block, per language or shared. Values the block editors store as JSON text are written as JSON text.</summary>
internal sealed record BlockField(IReadOnlyList<object> Values)
{
    private static object V(string alias, string? culture, object value) => new { alias, culture, segment = (string?)null, value };

    /// <summary>Arabic and English text.</summary>
    public static BlockField Text(string alias, string ar, string en) => new([V(alias, BlockJson.Ar, ar), V(alias, BlockJson.En, en)]);

    /// <summary>The same value in every language (numbers, codes, nested lists).</summary>
    public static BlockField Shared(string alias, object value) => new([V(alias, null, value)]);

    /// <summary>A dropdown choice.</summary>
    public static BlockField Pick(string alias, string option) => Shared(alias, JsonSerializer.Serialize(new[] { option }));

    /// <summary>Rich text (restricted editor HTML) in both languages.</summary>
    public static BlockField Rich(string alias, string arHtml, string enHtml) => Text(alias, RichJson(arHtml), RichJson(enHtml));

    /// <summary>One or more links per language.</summary>
    public static BlockField Links(string alias, SeedLink[] ar, SeedLink[] en) =>
        Text(alias, JsonSerializer.Serialize(ar.Select(l => l.Json())), JsonSerializer.Serialize(en.Select(l => l.Json())));

    public static BlockField Link(string alias, SeedLink ar, SeedLink en) => Links(alias, [ar], [en]);

    /// <summary>A nested block list (cards, steps, items).</summary>
    public static BlockField List(string alias, BlockJson items) => Shared(alias, items.Build());

    private static string RichJson(string html) => JsonSerializer.Serialize(new
    {
        markup = html,
        blocks = new { contentData = Array.Empty<object>(), settingsData = Array.Empty<object>(), expose = Array.Empty<object>(), layout = new { } },
    });
}
