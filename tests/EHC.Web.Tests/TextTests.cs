using EHC.Web.Blocks;

public class TextTests
{
    private static string Html(string? input)
    {
        using var w = new StringWriter();
        Text.Emphasis(input).WriteTo(w, System.Text.Encodings.Web.HtmlEncoder.Default);
        return w.ToString();
    }

    [Fact]
    public void Starred_words_become_em() => Assert.Equal("Care for <em>every stage</em> of life", Html("Care for *every stage* of life"));

    [Fact]
    public void Markup_is_encoded_before_highlighting() =>
        Assert.Equal("&lt;script&gt;x&lt;/script&gt; <em>&lt;b&gt;</em>", Html("<script>x</script> *<b>*"));

    [Fact]
    public void Empty_renders_nothing() => Assert.Equal("", Html("  "));

    [Fact]
    public void Plain_strips_asterisks() => Assert.Equal("Know your numbers", Text.Plain("Know your *numbers*"));
}
