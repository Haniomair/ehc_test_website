using EHC.Web.Site;

public class HealthToolsOverviewTests
{
    [Fact]
    public void Every_tool_has_a_known_kind_category_and_time()
    {
        Assert.All(HealthTools.All, t =>
        {
            Assert.Contains(t.Kind, new[] { "calculator", "test" });
            Assert.Contains(t.Category, HealthTools.Categories);
            Assert.InRange(t.Minutes, 1, 10);
        });
    }

    [Fact]
    public void Self_checks_are_the_questionnaires_and_the_vision_test() =>
        Assert.Equal(["visualAcuity", "prediabetes", "asthma"], HealthTools.All.Where(t => t.Kind == "test").Select(t => t.Key));

    [Theory]
    [InlineData("weight", "scal")]
    [InlineData("pregnancy", "baby")]
    [InlineData("screening", "shield")]
    public void Category_icons(string category, string icon) => Assert.Equal(icon, HealthTools.CategoryIcon(category));
}
