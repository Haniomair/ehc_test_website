using EHC.Web.Blocks;

public class BlockHelperTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=10", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ&list=x", "dQw4w9WgXcQ")]
    [InlineData(" dQw4w9WgXcQ ", "dQw4w9WgXcQ")]
    public void YouTube_links_give_the_id(string input, string id) => Assert.Equal(id, Video.YouTubeId(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://www.youtube.com/watch?v=abc\"onload")]
    [InlineData("https://www.youtube.com/channel/UCabcdefghijk")]
    public void Anything_else_is_rejected(string? input) => Assert.Null(Video.YouTubeId(input));

    [Fact]
    public void Table_splits_pipes_and_pads_rows()
    {
        var t = TableData.Parse("Day | Time | Clinic\nSunday | 8:00\n\nMonday|9:00|Eye", firstRowIsHeader: true);
        Assert.Equal(["Day", "Time", "Clinic"], t.Header);
        Assert.Equal(2, t.Rows.Count);
        Assert.Equal(["Sunday", "8:00", ""], t.Rows[0]);
    }

    [Fact]
    public void Table_accepts_tabs_from_spreadsheets()
    {
        var t = TableData.Parse("a\tb|c\r\nd\te", firstRowIsHeader: false);
        Assert.Empty(t.Header);
        Assert.Equal(["a", "b|c"], t.Rows[0]);
    }

    [Fact]
    public void Empty_table_is_empty() => Assert.True(TableData.Parse("  \n ", true).IsEmpty);
}
