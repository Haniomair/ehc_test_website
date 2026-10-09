using System.Text.Json;
using System.Text.Json.Nodes;
using EHC.Web.Composers;

public class NavigatorSymptomsTests
{
    private static readonly string[] Levels = ["emergency", "urgent", "primary", "virtual", "self"];

    private static List<(string Ar, string En, string Level)> Starter()
    {
        var root = JsonNode.Parse(NavigatorSymptomsSeeder.Symptoms(alias => Guid.NewGuid()))!;
        return root["contentData"]!.AsArray().Select(b =>
        {
            var values = b!["values"]!.AsArray();
            string Value(string alias, string? culture) => (string)values.First(v => (string?)v!["alias"] == alias && (string?)v["culture"] == culture)!["value"]!;
            var level = JsonSerializer.Deserialize<string[]>(Value("level", null))![0];
            return (Value("label", "ar-SA"), Value("label", "en-US"), level);
        }).ToList();
    }

    [Fact]
    public void Every_symptom_has_a_known_care_level_and_both_languages()
    {
        var symptoms = Starter();
        Assert.NotEmpty(symptoms);
        Assert.All(symptoms, s =>
        {
            Assert.Contains(s.Level, Levels);
            Assert.False(string.IsNullOrWhiteSpace(s.Ar));
            Assert.False(string.IsNullOrWhiteSpace(s.En));
        });
        Assert.Equal(symptoms.Count, symptoms.Select(s => s.En).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(symptoms.Count, symptoms.Select(s => s.Ar).Distinct().Count());
    }

    [Theory]
    [InlineData("Chest pain")]
    [InlineData("Severe difficulty breathing")]
    [InlineData("Signs of a stroke")]
    [InlineData("Severe bleeding")]
    [InlineData("Thoughts of harming yourself")]
    public void Red_flag_symptoms_always_go_to_emergency(string symptom) =>
        Assert.Equal("emergency", Starter().Single(s => s.En == symptom).Level);

    [Fact]
    public void Every_care_level_is_used()
    {
        var used = Starter().Select(s => s.Level).ToHashSet();
        Assert.All(Levels, l => Assert.Contains(l, used));
    }
}
