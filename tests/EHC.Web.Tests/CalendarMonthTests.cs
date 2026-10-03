using EHC.Web.Site;

public class CalendarMonthTests
{
    [Theory]
    [InlineData("Oct", 10)]
    [InlineData("October", 10)]
    [InlineData("أكتوبر", 10)]
    [InlineData("اكتوبر", 10)]
    [InlineData("Sept.", 9)]
    [InlineData("أبريل", 4)]
    [InlineData("يونيه", 6)]
    [InlineData(" may ", 5)]
    public void Month_labels_are_recognised(string label, int month) => Assert.Equal(month, CalendarMonth.Parse(label));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ramadan")]
    [InlineData("رمضان")]
    public void Other_labels_are_not_months(string? label) => Assert.Null(CalendarMonth.Parse(label));

    [Fact]
    public void Current_entry_follows_today()
    {
        var labels = new[] { "Sep", "Oct", "Nov" };
        Assert.Equal(1, CalendarMonth.CurrentIndex(labels, new DateOnly(2026, 10, 1)));
        Assert.Equal(-1, CalendarMonth.CurrentIndex(labels, new DateOnly(2027, 2, 1)));
    }
}
