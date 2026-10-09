using EHC.Web.Blocks;

public class RichTextTocTests
{
    [Fact]
    public void Headings_get_ids_and_are_listed_in_order()
    {
        var r = RichTextToc.From("<h2>Who we are</h2><p>x</p><h2 class=\"a\">Data &amp; <b>cookies</b></h2><h3>Not listed</h3>");
        Assert.Equal(["Who we are", "Data & cookies"], r.Entries.Select(e => e.Text));
        Assert.Equal(["sec-who-we-are", "sec-data-cookies"], r.Entries.Select(e => e.Id));
        Assert.Contains("<h2 id=\"sec-who-we-are\">Who we are</h2>", r.Html);
        Assert.Contains("<h2 id=\"sec-data-cookies\" class=\"a\">Data &amp; <b>cookies</b></h2>", r.Html);
        Assert.Contains("<h3>Not listed</h3>", r.Html);
    }

    [Fact]
    public void Existing_ids_are_kept_and_repeats_made_unique()
    {
        var r = RichTextToc.From("<h2 id=\"sec-faq\">FAQ</h2><h2>FAQ</h2><h2>FAQ</h2><h2>   </h2>");
        Assert.Equal(["sec-faq", "sec-faq-2", "sec-faq-3"], r.Entries.Select(e => e.Id));
        Assert.Contains("<h2>   </h2>", r.Html);
    }

    [Fact]
    public void Arabic_headings_keep_their_letters()
    {
        var r = RichTextToc.From("<h2>من نحن</h2>");
        Assert.Equal("sec-من-نحن", r.Entries.Single().Id);
    }
}
