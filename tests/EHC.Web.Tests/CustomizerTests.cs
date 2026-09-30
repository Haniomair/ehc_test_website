using EHC.Web.Site;

public class CustomizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("panel=sideways&corners=soft")]                  // unknown value, default value: nothing left
    [InlineData("theme=not-a-guid&rotate=maybe&hide=XYZ")]
    public void Nothing_valid_means_no_state(string? cookie) => Assert.Null(Customizer.Parse(cookie));

    [Fact]
    public void Reads_allow_listed_values()
    {
        var s = Customizer.Parse("theme=1a3b2e4f-7a01-4b85-b0e7-91a9f153bddf&panel=navigator&rotate=0&side=start&frame=plain&align=center&corners=square&density=compact&hide=0a1b2c3d.ffffffff&order=ffffffff.0a1b2c3d")!;
        Assert.Equal(Guid.Parse("1a3b2e4f-7a01-4b85-b0e7-91a9f153bddf"), s.Theme);
        Assert.Equal("navigator", s.HeroPanel);
        Assert.False(s.HeroRotate);
        Assert.Equal(("start", "plain", "center"), (s.HeroSide, s.HeroFrame, s.HeroAlign));
        Assert.Equal("look-corners-square look-density-compact", s.HtmlClass);
        Assert.Equal(["0a1b2c3d", "ffffffff"], s.Hidden);
        Assert.Equal(["ffffffff", "0a1b2c3d"], s.Order);
    }

    [Theory]
    [InlineData("corners=square\" onload=\"x")]                   // attribute injection attempt
    [InlineData("corners=square;background:red")]
    public void Look_values_outside_the_list_are_dropped(string cookie)
        => Assert.Null(Customizer.Parse(cookie)?.HtmlClass);

    [Fact]
    public void Section_keys_must_be_short_hex()
    {
        var s = Customizer.Parse("hide=0a1b2c3d.<script>.0A1B2C3D.0a1b2c3d4")!;
        Assert.Equal(["0a1b2c3d"], s.Hidden);
    }

    [Fact]
    public void Oversized_cookie_is_ignored()
        => Assert.Null(Customizer.Parse("hide=" + string.Join(".", Enumerable.Repeat("0a1b2c3d", 300))));

    [Fact]
    public void Arrange_moves_listed_sections_and_keeps_the_rest_in_place()
    {
        Guid K(string prefix) => Guid.Parse(prefix + "-0000-0000-0000-000000000000");
        var items = new[] { K("aaaaaaaa"), K("bbbbbbbb"), K("cccccccc"), K("dddddddd") };
        var s = new CustomizerState { Order = ["dddddddd", "bbbbbbbb"] };
        var result = s.Arrange(items, g => g).Select(Customizer.ShortKey);
        Assert.Equal(["aaaaaaaa", "dddddddd", "cccccccc", "bbbbbbbb"], result);
    }

    [Theory]
    [InlineData("star", false)]
    [InlineData("hajj", true)]
    [InlineData("eid-adha", true)]
    [InlineData("none", true)]
    public void Only_the_star_pattern_spins(string key, bool upright)
    {
        var theme = EHC.Web.Themes.ResolvedTheme.Default with { PatternCss = EHC.Web.Themes.ThemeResolver.CustomizerPatterns[key] };
        Assert.Equal(upright, theme.InlineStyle!.Contains("--pattern-spin:none"));
    }

    [Fact]
    public void Pattern_choice_is_allow_listed()
    {
        Assert.Equal("eid-fitr", Customizer.Parse("pattern=eid-fitr")!.Pattern);
        Assert.Null(Customizer.Parse("pattern=url(x)"));
    }
}
