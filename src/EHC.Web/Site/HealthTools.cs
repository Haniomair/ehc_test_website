namespace EHC.Web.Site;

/// <summary>
/// The interactive health tools (calculators and self-assessments). Editors pick a tool by key on a health tool page
/// or in the "Health tool" section; texts come from the dictionary (EHC.Tools.*) so the clinical team can edit them.
/// All calculations run in the browser (components/health-tools.js, components/vision-test.js); nothing is sent or stored.
/// </summary>
public static class HealthTools
{
    public sealed record Tool(string Key, string Icon, string Category);

    public static readonly IReadOnlyList<Tool> All =
    [
        new("bmi", "scal", "weight"),
        new("idealWeight", "target", "weight"),
        new("calories", "flame", "weight"),
        new("ovulation", "cal", "pregnancy"),
        new("dueDate", "baby", "pregnancy"),
        new("visualAcuity", "eye", "screening"),
        new("prediabetes", "drop", "screening"),
        new("asthma", "lungs", "screening"),
    ];

    public static readonly IReadOnlyList<string> Categories = ["weight", "pregnancy", "screening"];

    private static readonly Dictionary<string, Tool> ByKey = All.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);

    public static Tool? Find(string? key) => key is null ? null : ByKey.GetValueOrDefault(key);

    // ------------------------------------------------------------------ self-assessments

    /// <summary>One answer: dictionary key suffix and the points it scores.</summary>
    public sealed record Option(string Key, int Score);

    /// <summary>A question; <see cref="OnlyIf"/> shows it only when another question has a given answer (e.g. women only).</summary>
    public sealed record Question(string Key, IReadOnlyList<Option> Options, (string Question, string Option)? OnlyIf = null);

    /// <summary>Result band: applies when the total is at least <see cref="Min"/> (bands are checked from the top).</summary>
    public sealed record Band(int Min, string Key, string Tone);

    public sealed record Questionnaire(string Key, IReadOnlyList<Question> Questions, IReadOnlyList<Band> Bands, int Max, bool WeightQuestion);

    private static Question YesNo(string key, int yes, (string, string)? onlyIf = null) => new(key, [new("yes", yes), new("no", 0)], onlyIf);

    /// <summary>
    /// Prediabetes risk test (adapted from the ADA / CDC test): age, sex, gestational diabetes, family history,
    /// blood pressure, activity and a weight score from height and weight. 5 or more = high risk.
    /// </summary>
    public static readonly Questionnaire Prediabetes = new("prediabetes",
    [
        new("age", [new("under40", 0), new("40to49", 1), new("50to59", 2), new("60plus", 3)]),
        new("sex", [new("male", 1), new("female", 0)]),
        YesNo("gestational", 1, ("sex", "female")),
        YesNo("family", 1),
        YesNo("pressure", 1),
        new("active", [new("yes", 0), new("no", 1)]),
    ],
    [new(5, "high", "warn"), new(0, "low", "good")],
    Max: 10, WeightQuestion: true);

    /// <summary>
    /// Asthma Control Test: five questions scored 1–5 (total 5–25). 20+ well controlled, 16–19 not well controlled,
    /// 15 or less very poorly controlled.
    /// </summary>
    public static readonly Questionnaire Asthma = new("asthma",
    [
        new("q1", Scale(5)), new("q2", Scale(5)), new("q3", Scale(5)), new("q4", Scale(5)), new("q5", Scale(5)),
    ],
    [new(20, "good", "good"), new(16, "fair", "warn"), new(0, "poor", "bad")],
    Max: 25, WeightQuestion: false);

    private static Option[] Scale(int n) => Enumerable.Range(1, n).Select(i => new Option("a" + i, i)).ToArray();

    public static Questionnaire? QuestionnaireFor(string key) => key switch
    {
        "prediabetes" => Prediabetes,
        "asthma" => Asthma,
        _ => null,
    };

    /// <summary>Weight points for the prediabetes test from BMI: under 25 = 0, 25–29.9 = 1, 30–39.9 = 2, 40+ = 3.</summary>
    public static int WeightScore(double bmi) => bmi switch { < 25 => 0, < 30 => 1, < 40 => 2, _ => 3 };

    /// <summary>Total → band (the first band, from the top, whose minimum is reached).</summary>
    public static Band BandFor(Questionnaire q, int total) => q.Bands.OrderByDescending(b => b.Min).First(b => total >= b.Min);
}

/// <summary>A number input on a tool form (Views/Partials/tools/_Field.cshtml).</summary>
public sealed record ToolField(string Id, string Name, string Label, string? Unit, double Min, double Max, double Step = 1, string? Placeholder = null);

/// <summary>The slider BMI calculator (Views/Partials/tools/_BmiSlider.cshtml): used by the "Know your numbers" section and the BMI tool page.</summary>
public sealed record BmiSlider(string Id, bool ShowTitle);
