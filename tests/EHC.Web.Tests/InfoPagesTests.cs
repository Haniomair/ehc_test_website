using System.Text.Json.Nodes;
using EHC.Web.Composers;
using EHC.Web.Site;

public class InfoPagesTests
{
    private static readonly string[] Ids = ["privacy", "terms", "guide", "projects", "researchers", "academic", "rights"];

    private static InfoPagesContent Content(Guid? contact = null) => new(
        new InfoPagesContent.Pages(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), contact ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
        alias => new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(alias))),
        new InfoPagesContent.Retention(12, 12, 30, 90));

    [Theory]
    [InlineData("https://www.ehc.med.sa/ar/%D9%84%D9%84%D8%A8%D8%A7%D8%AD%D8%AB%D9%8A%D9%86/", "ar/للباحثين")]
    [InlineData("https://www.ehc.med.sa/Privacy-Policy/", "privacy-policy")]
    [InlineData("https://tb.ehc.med.sa/admission", "admission")]
    [InlineData("https://example.com/privacy-policy/", null)]
    [InlineData("/ar/privacy-policy/", null)]
    public void Old_site_paths_are_decoded(string url, string? expected) => Assert.Equal(expected, LinkRewriter.OldSitePath(url));

    [Theory]
    [MemberData(nameof(PageIds))]
    public void Every_page_is_a_valid_block_list_exposed_in_both_languages(string id)
    {
        var root = JsonNode.Parse(Content().Blocks(id))!;
        var blocks = root["contentData"]!.AsArray();
        Assert.NotEmpty(blocks);
        Assert.Equal(blocks.Count, root["layout"]!["Umbraco.BlockList"]!.AsArray().Count);
        Assert.Equal(blocks.Count, root["settingsData"]!.AsArray().Count);
        Assert.Equal(blocks.Count * 2, root["expose"]!.AsArray().Count);
        foreach (var block in blocks)
        {
            foreach (var value in block!["values"]!.AsArray())
            {
                var culture = (string?)value!["culture"];
                Assert.True(culture is null or "ar-SA" or "en-US");
                // nested lists, links, rich text and dropdowns are stored as JSON text, as the editors save them
                Assert.True(value["value"] is JsonValue, $"{id}: {(string?)value["alias"]} is not stored as text");
            }
        }
    }

    [Theory]
    [MemberData(nameof(PageIds))]
    public void Pages_have_text_in_both_languages_and_no_links_to_the_old_site(string id)
    {
        var json = Content().Blocks(id);
        Assert.DoesNotContain("coming-soon", json);
        Assert.DoesNotContain("www.ehc.med.sa/", json);
        var root = JsonNode.Parse(json)!;
        var cultures = root["contentData"]!.AsArray().SelectMany(b => b!["values"]!.AsArray()).Select(v => (string?)v!["culture"]).Where(c => c is not null).ToList();
        Assert.Equal(cultures.Count(c => c == "ar-SA"), cultures.Count(c => c == "en-US"));
    }

    [Fact]
    public void Privacy_draft_is_marked_and_uses_the_configured_retention()
    {
        var json = Content().Blocks("privacy");
        Assert.Contains("Draft for review", json);
        Assert.Contains("deleted after 30 days", json);
        Assert.Contains("##", json);
    }

    [Fact]
    public void Contact_bands_are_left_out_without_a_contact_page()
    {
        var withContact = JsonNode.Parse(Content().Blocks("guide"))!["contentData"]!.AsArray().Count;
        var content = new InfoPagesContent(
            new InfoPagesContent.Pages(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid()),
            alias => Guid.NewGuid(), new InfoPagesContent.Retention(12, 12, 30, 90));
        Assert.Equal(withContact - 1, JsonNode.Parse(content.Blocks("guide"))!["contentData"]!.AsArray().Count);
    }

    [Theory]
    [InlineData("https://www.moh.gov.sa/awarenessplateform/patientsrights/pages/default.aspx", "moh.gov.sa/awarenessplateform/patientsrights/pages/default.aspx")]
    [InlineData("https://www.ehc.med.sa/ar/%D8%A7%D9%84%D8%B4%D8%B1%D9%88%D8%B7-%D9%88%D8%A7%D9%84%D8%A3%D8%AD%D9%83%D8%A7%D9%85/", "ehc.med.sa/ar/الشروط-والأحكام")]
    [InlineData("https://tb.ehc.med.sa/admission", "tb.ehc.med.sa/admission")]
    [InlineData("tel:997", null)]
    [InlineData("/ar/page/", null)]
    public void Addresses_are_compared_by_host_and_decoded_path(string url, string? expected) => Assert.Equal(expected, InfoPagesSeeder.HostPath(url));

    [Fact]
    public void Links_to_other_sites_are_rewritten_when_their_host_is_named()
    {
        var key = Guid.NewGuid();
        var root = JsonNode.Parse("""[{"name":"Patient rights","url":"https://www.moh.gov.sa/awarenessplateform/patientsrights/pages/default.aspx"}]""");
        Guid? Target(string url) => InfoPagesSeeder.HostPath(url) == "moh.gov.sa/awarenessplateform/patientsrights/pages/default.aspx" ? key : null;
        Assert.Equal(1, LinkRewriter.Rewrite(root, Target, "moh.gov.sa"));
        Assert.Equal(key.ToString(), (string?)root![0]!["unique"]);
    }

    [Fact]
    public void Rights_page_links_the_official_documents_and_the_ways_to_take_part()
    {
        var json = Content().Blocks("rights");
        Assert.Contains("moh.gov.sa/awarenessplateform/Patientsrights/Documents", json);
        Assert.Contains("open.data.gov.sa", json);
        Assert.Contains("e-participation", json);
    }

    public static TheoryData<string> PageIds() => [.. Ids];
}
