using EHC.Web.Themes;

public class ThemeResolverTests
{
    private static readonly DateTime Start = new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("2026-09-19T23:59:59Z", false)]
    [InlineData("2026-09-20T00:00:00Z", true)]   // start inclusive
    [InlineData("2026-09-23T12:00:00Z", true)]
    [InlineData("2026-09-25T00:00:00Z", false)]  // end exclusive
    public void Schedule_window(string now, bool expected)
        => Assert.Equal(expected, ThemeResolver.IsLive(Start, End, DateTime.Parse(now).ToUniversalTime()));

    [Theory]
    [InlineData("#007A3D", true)]
    [InlineData("#abc", false)]
    [InlineData("red", false)]
    [InlineData("#000000;background:url(x)", false)]   // CSS injection attempt
    public void Hex_validation(string value, bool ok)
        => Assert.Equal(ok, ThemeResolver.HexColor().IsMatch(value));
}

public class ContrastTests
{
    [Theory]
    [InlineData("#FFFFFF", "#000000", 21.0)]
    [InlineData("#FFFFFF", "#FFFFFF", 1.0)]
    public void Ratio_matches_wcag(string a, string b, double expected)
        => Assert.Equal(expected, Contrast.Ratio(a, b), 2);

    [Fact]
    public void Default_brand_blue_passes_as_brand500_but_not_as_brand600()
    {
        // #2490CC is the EHC default brand-500: 3.5:1 with white (see CHANGELOG), fine for large UI, not for button text.
        Assert.Empty(Contrast.Check(new Dictionary<string, string?> { ["brand500"] = "#2490CC" }));
        Assert.Single(Contrast.Check(new Dictionary<string, string?> { ["brand600"] = "#2490CC" }));
    }

    [Fact]
    public void Empty_and_invalid_overrides_are_ignored()
        => Assert.Empty(Contrast.Check(new Dictionary<string, string?> { ["brand600"] = "", ["deep900"] = null, ["accent400"] = "red" }));

    [Fact]
    public void Pale_deep900_fails_both_rules()
        => Assert.Equal(2, Contrast.Check(new Dictionary<string, string?> { ["deep900"] = "#B0C4DE" }).Count);
}
