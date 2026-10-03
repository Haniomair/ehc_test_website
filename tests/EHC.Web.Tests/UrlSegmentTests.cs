using EHC.Web.Site;

public class UrlSegmentTests
{
    [Theory]
    [InlineData("مجمع الدمام الطبي يطلق مختبرًا لاضطرابات النوم", "مجمع الدمام الطبي يطلق مختبرا لاضطرابات النوم")]
    [InlineData("سمو أمير المنطقة الشرقية يكرّم التجمع", "سمو أمير المنطقة الشرقية يكرم التجمع")]
    [InlineData("صُم بصحة", "صم بصحة")]
    [InlineData("الرحمـــن", "الرحمن")] // tatweel
    public void Arabic_marks_are_removed(string name, string expected) => Assert.Equal(expected, ArabicUrlSegmentProvider.StripMarks(name));

    [Theory]
    [InlineData("Dammam Medical Complex")]
    [InlineData("مستشفى الولادة والأطفال بالدمام")]
    [InlineData("«حج بصحة 2026»")]
    public void Names_without_marks_are_unchanged(string name) => Assert.Equal(name, ArabicUrlSegmentProvider.StripMarks(name));
}
