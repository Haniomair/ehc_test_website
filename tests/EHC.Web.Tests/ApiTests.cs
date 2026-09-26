using EHC.Web.Api;
using Microsoft.Extensions.Options;

public class ApiTests
{
    [Fact]
    public async Task Er_stub_returns_no_numbers_by_default()
    {
        // docs/06: never fake numbers in production — without a real feed the block shows "coming soon"
        var provider = new StubErWaitProvider(Options.Create(new ErWaitOptions()), TimeProvider.System);
        var result = await provider.GetAsync(CancellationToken.None);
        Assert.False(result.Available);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Er_demo_data_is_flagged_illustrative()
    {
        var provider = new StubErWaitProvider(Options.Create(new ErWaitOptions { UseDemoData = true }), TimeProvider.System);
        var result = await provider.GetAsync(CancellationToken.None);
        Assert.True(result.Available);
        Assert.True(result.Illustrative);
        Assert.All(result.Items, i => Assert.InRange(i.Minutes, 0, 120));
    }

    [Theory]
    [InlineData(10, "quiet")]
    [InlineData(29, "quiet")]
    [InlineData(30, "moderate")]
    [InlineData(55, "busy")]
    public void Er_levels(int minutes, string level) => Assert.Equal(level, ErLevel.For(minutes));

    [Theory]
    [InlineData(null, "ar-SA")]
    [InlineData("en", "en-US")]
    [InlineData("EN-us", "en-US")]
    [InlineData("fr", "ar-SA")]
    public void Culture_mapping(string? input, string expected) => Assert.Equal(expected, ApiCulture.From(input));
}
