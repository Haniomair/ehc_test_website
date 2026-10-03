using EHC.Web.Vitals;

public class VitalsTests
{
    [Theory]
    [InlineData("LCP", 2500, "good")]
    [InlineData("LCP", 2501, "needs-improvement")]
    [InlineData("LCP", 4000, "needs-improvement")]
    [InlineData("LCP", 4001, "poor")]
    [InlineData("INP", 200, "good")]
    [InlineData("INP", 500, "needs-improvement")]
    [InlineData("INP", 501, "poor")]
    [InlineData("CLS", 0.1, "good")]
    [InlineData("CLS", 0.25, "needs-improvement")]
    [InlineData("CLS", 0.26, "poor")]
    public void Ratings_follow_Googles_thresholds(string metric, double p75, string rating) => Assert.Equal(rating, VitalsMetrics.Rating(metric, p75));

    [Theory]
    [InlineData("LCP", 1800, true)]
    [InlineData("CLS", 0, true)]
    [InlineData("LCP", -1, false)]
    [InlineData("LCP", 120000, false)]   // a tab left open in the background, not a real paint
    [InlineData("CLS", 50, false)]
    [InlineData("FID", 10, false)]       // retired metric
    [InlineData("TTFB", 300, false)]     // not collected
    public void Only_known_metrics_in_range_are_accepted(string metric, double value, bool ok) => Assert.Equal(ok, VitalsMetrics.Valid(metric, value));

    [Fact]
    public void Not_a_number_is_rejected() => Assert.False(VitalsMetrics.Valid("INP", double.NaN));
}
