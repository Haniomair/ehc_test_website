using EHC.Web.Site;

public class HealthToolsTests
{
    [Fact]
    public void Every_tool_has_a_category_and_questionnaires_exist_for_the_self_checks()
    {
        Assert.All(HealthTools.All, t => Assert.Contains(t.Category, HealthTools.Categories));
        Assert.Equal(HealthTools.All.Count, HealthTools.All.Select(t => t.Key).Distinct().Count());
        Assert.NotNull(HealthTools.QuestionnaireFor("prediabetes"));
        Assert.NotNull(HealthTools.QuestionnaireFor("asthma"));
        Assert.Null(HealthTools.QuestionnaireFor("bmi"));
        Assert.Null(HealthTools.Find("unknown"));
        Assert.Equal("bmi", HealthTools.Find("BMI")!.Key);
    }

    [Theory]
    [InlineData(24.9, 0)]
    [InlineData(25, 1)]
    [InlineData(29.9, 1)]
    [InlineData(30, 2)]
    [InlineData(39.9, 2)]
    [InlineData(40, 3)]
    public void Prediabetes_weight_points_follow_bmi(double bmi, int points) =>
        Assert.Equal(points, HealthTools.WeightScore(bmi));

    [Theory]
    [InlineData(4, "low")]
    [InlineData(5, "high")]
    [InlineData(10, "high")]
    public void Prediabetes_high_risk_from_five(int total, string band) =>
        Assert.Equal(band, HealthTools.BandFor(HealthTools.Prediabetes, total).Key);

    [Theory]
    [InlineData(5, "poor")]
    [InlineData(15, "poor")]
    [InlineData(16, "fair")]
    [InlineData(19, "fair")]
    [InlineData(20, "good")]
    [InlineData(25, "good")]
    public void Asthma_control_bands(int total, string band) =>
        Assert.Equal(band, HealthTools.BandFor(HealthTools.Asthma, total).Key);

    [Fact]
    public void Questionnaire_maximum_matches_the_highest_answers()
    {
        // asthma: five questions of 1–5
        Assert.Equal(HealthTools.Asthma.Max, HealthTools.Asthma.Questions.Sum(q => q.Options.Max(o => o.Score)));
        // prediabetes: best case for each sex (the gestational question is for women only) + 3 weight points
        foreach (var sex in new[] { "male", "female" })
        {
            var max = HealthTools.Prediabetes.Questions
                .Where(q => q.OnlyIf is null || q.OnlyIf.Value.Option == sex)
                .Sum(q => q.Key == "sex" ? q.Options.First(o => o.Key == sex).Score : q.Options.Max(o => o.Score)) + 3;
            Assert.Equal(HealthTools.Prediabetes.Max, max);
        }
    }
}

public class AzIndexTests
{
    [Theory]
    [InlineData("الأطفال", "ا")]
    [InlineData("أمراض القلب", "ا")]
    [InlineData("إرادة", "ا")]
    [InlineData("الجراحة العامة", "ج")]
    [InlineData("طب العيون", "ط")]
    public void Arabic_letter_ignores_the_article_and_hamza(string name, string letter) =>
        Assert.Equal(letter, AzIndex.LetterOf(name, arabic: true));

    [Theory]
    [InlineData("مستشفى الجبيل العام", "ج")]
    [InlineData("مركز صحي غرناطة", "غ")]
    [InlineData("Granada Health Center", "G")]
    [InlineData("Al-Khafji General Hospital", "K")]
    public void Facility_words_are_skipped(string name, string letter) =>
        Assert.Equal(letter, AzIndex.LetterOf(name, name[0] > 'z', AzIndex.FacilityWords));

    [Fact]
    public void Groups_follow_the_alphabet_with_other_scripts_and_symbols_last()
    {
        var groups = AzIndex.Build(
        [
            new("طب العيون", "/a", null), new("الأطفال", "/b", null), new("Dental", "/c", null), new("3D imaging", "/d", null), new("باطنية", "/e", null),
        ], arabic: true);
        Assert.Equal(["ا", "ب", "ط", "D", "#"], groups.Select(g => g.Letter));
    }
}

public class HealthArticlesTests
{
    [Fact]
    public void Reading_time_counts_words_not_markup()
    {
        Assert.Equal(1, HealthArticles.ReadingMinutes(null));
        Assert.Equal(1, HealthArticles.ReadingMinutes("<p>short</p>"));
        var words = string.Join(" ", Enumerable.Repeat("كلمة", 900));
        Assert.Equal(5, HealthArticles.ReadingMinutes($"<p class=\"x\">{words}</p>"));
    }

    [Fact]
    public void Every_topic_has_an_icon() =>
        Assert.All(HealthArticles.Topics, t => Assert.NotEqual("book", HealthArticles.IconFor(t)));
}
