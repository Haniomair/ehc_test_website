using EHC.Web.Composers;
using EHC.Web.Site;

public class HealthLibraryTests
{
    private static readonly HealthLibraryContent.Item[] Items = HealthLibraryContent.Items;

    [Theory]
    [InlineData("السُّكَّري", "السكري")]
    [InlineData("أنيميا  الفول", "انيميا الفول")]
    [InlineData("حصوة", "حصوه")]
    [InlineData("Breast CANCER ", "breast cancer")]
    public void Search_text_is_normalised(string input, string expected) => Assert.Equal(expected, LibraryCatalog.Normalize(input));

    [Fact]
    public void Lines_skip_blanks_and_bullets() =>
        Assert.Equal(["One", "Two", "Three"], LibraryCatalog.Lines("One\n\n- Two\r\n • Three \n"));

    [Fact]
    public void Section_text_becomes_encoded_paragraphs_and_lists()
    {
        Assert.Equal("<p>Intro &amp; more</p><ul><li>a</li><li>b &lt;c&gt;</li></ul><p>End</p>", HealthLibraryContent.Html("Intro & more\n- a\n- b <c>\nEnd"));
    }

    [Fact]
    public void Slugs_are_unique_and_links_point_to_existing_pages()
    {
        var slugs = Items.Select(i => i.Slug).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
        foreach (var item in Items)
        {
            foreach (var target in item.Related ?? [])
            {
                var other = Items.SingleOrDefault(i => i.Slug == target);
                Assert.True(other is not null, $"{item.Slug} links to missing {target}");
                Assert.NotEqual(item.IsTest, other!.IsTest);   // conditions link to tests and the other way round
            }
        }
    }

    [Fact]
    public void Every_page_has_valid_systems_sections_and_both_languages()
    {
        foreach (var item in Items)
        {
            Assert.NotEmpty(item.Systems);
            Assert.All(item.Systems, s => Assert.NotNull(LibraryCatalog.System(s)));
            Assert.All(item.Audiences ?? [], a => Assert.Contains(a, LibraryCatalog.Audiences));
            var allowed = item.IsTest ? LibraryCatalog.TestSections : LibraryCatalog.ConditionSections;
            Assert.All(item.Sections.Keys, k => Assert.Contains(k, allowed));
            Assert.Contains(item.IsTest ? "about" : "overview", item.Sections.Keys);
            foreach (var t in new[] { item.Name, item.Aka, item.Intro }.Concat(item.Sections.Values))
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Ar), item.Slug);
                Assert.False(string.IsNullOrWhiteSpace(t.En), item.Slug);
            }
            Assert.All(item.Sources ?? [], s => Assert.StartsWith("https://", s.UrlEn));
            if (item.IsTest) Assert.True(item.Emergency is null && item.Urgent is null && item.Primary is null, item.Slug);
        }
    }

    [Fact]
    public void Help_tiers_have_the_same_number_of_items_in_both_languages()
    {
        foreach (var item in Items)
        {
            foreach (var tier in new[] { item.Emergency, item.Urgent, item.Primary }.OfType<HealthLibraryContent.T>())
            {
                Assert.Equal(LibraryCatalog.Lines(tier.Ar).Count, LibraryCatalog.Lines(tier.En).Count);
            }
            foreach (var (key, text) in item.Sections)
            {
                Assert.True(text.Ar.Split('\n').Count(l => l.StartsWith("- ")) == text.En.Split('\n').Count(l => l.StartsWith("- ")), $"{item.Slug}.{key}: list lengths differ");
            }
        }
    }

    [Fact]
    public void Body_map_points_are_inside_the_figure()
    {
        foreach (var s in LibraryCatalog.Systems.Where(s => s.OnMap))
        {
            Assert.InRange(s.X!.Value, 0, 100);
            Assert.InRange(s.Y!.Value, 0, 100);
        }
        Assert.Equal(LibraryCatalog.Systems.Count, LibraryCatalog.Systems.Select(s => s.Key).Distinct().Count());
    }
}
