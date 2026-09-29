using EHC.Web.Feedback;

public class FeedbackTextTests
{
    [Theory]
    [InlineData("Call me on 0551234567 please", "Call me on […] please")]
    [InlineData("رقم الهوية ١٠٢٣٤٥٦٧٨٩ والملف 55-66-77 والغرفة 12-34", "رقم الهوية […] والملف […] والغرفة 12-34")]
    [InlineData("MRN 123 456 789", "MRN […]")]
    [InlineData("mail me: someone@example.com", "mail me: […]")]
    [InlineData("  lots\n\n of   space  ", "lots of space")]
    [InlineData("Opening hours for 2026 are wrong", "Opening hours for 2026 are wrong")]
    public void Comments_are_cleaned(string input, string expected) => Assert.Equal(expected, FeedbackText.Comment(input));

    [Fact]
    public void Comments_are_shortened()
    {
        var c = FeedbackText.Comment(new string('a', 400))!;
        Assert.Equal(FeedbackText.MaxComment + 1, c.Length);
        Assert.EndsWith("…", c);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Empty_comments_are_null(string? input) => Assert.Null(FeedbackText.Comment(input));

    [Theory]
    [InlineData("outdated", "outdated")]
    [InlineData("OUTDATED", null)]
    [InlineData("<script>", null)]
    [InlineData(null, null)]
    public void Only_known_reasons_are_kept(string? input, string? expected) => Assert.Equal(expected, FeedbackText.Reason(input));
}
