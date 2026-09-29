using EHC.Web.Blocks;

public class ComponentCatalogTests
{
    private static string ComponentsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "EHC.Web"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "EHC.Web", "Views", "Partials", "blocklist", "Components");
    }

    [Fact]
    public void Every_section_view_has_a_catalogue_entry()
    {
        var missing = Directory.GetFiles(ComponentsDir(), "*.cshtml")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(a => ComponentCatalog.Find(a!) is null)
            .ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Catalogue_entries_have_a_view_and_a_known_category()
    {
        var views = Directory.GetFiles(ComponentsDir(), "*.cshtml").Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in ComponentCatalog.All)
        {
            Assert.Contains(c.Alias, views);
            Assert.Contains(ComponentCatalog.Categories, x => x.Key == c.CategoryKey);
            Assert.False(string.IsNullOrWhiteSpace(c.NameAr + c.NameEn + c.DescriptionAr + c.DescriptionEn));
        }
        Assert.Equal(ComponentCatalog.All.Count, ComponentCatalog.All.Select(c => c.Alias).Distinct().Count());
    }
}
