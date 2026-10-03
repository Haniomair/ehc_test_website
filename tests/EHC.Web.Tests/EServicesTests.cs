using EHC.Web.Site;

public class EServicesTests
{
    [Fact]
    public void Stored_values_become_keys_in_the_fixed_order()
    {
        Assert.Equal(["patients", "staff"], EServices.Keys(["Staff", "Patients", "Martians"]));
        Assert.Equal(["trainees"], EServices.Keys([" trainees "]));
        Assert.Empty(EServices.Keys(null));
    }

    [Fact]
    public void Patients_come_first() => Assert.Equal("patients", EServices.Audiences[0].Key);

    [Fact]
    public void Every_audience_matches_the_editor_options_and_has_a_name()
    {
        var root = Root();
        var config = File.ReadAllText(Path.Combine(root, "src", "EHC.Web", "uSync", "v17", "DataTypes", "EHCEServiceAudience.config"));
        var dictionary = Directory.GetFiles(Path.Combine(root, "src", "EHC.Web", "uSync", "v17", "Dictionary"), "*.config").Select(File.ReadAllText).ToList();
        foreach (var a in EServices.Audiences)
        {
            Assert.Contains($"\"{a.Value}\"", config);
            Assert.Contains(dictionary, d => d.Contains($"Alias=\"EHC.EServices.Audience.{a.Key}\""));
        }
    }

    static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "EHC.slnx"))) return dir.FullName;
        Assert.Fail("repository root not found");
        return "";
    }
}
