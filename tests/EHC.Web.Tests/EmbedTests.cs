using EHC.Web.Site;

public class EmbedTests
{
    private static readonly string[] Frappe = ["forms.ehc.example"];

    [Theory]
    [InlineData("https://forms.office.com/Pages/ResponsePage.aspx?id=AbC-12_xyz&x=1", "https://forms.office.com/Pages/ResponsePage.aspx?id=AbC-12_xyz&embed=true")]
    [InlineData("https://forms.office.com/r/Ab12Cd34", "https://forms.office.com/r/Ab12Cd34?embed=true")]
    [InlineData("https://forms.cloud.microsoft/r/Ab12Cd34?origin=lprLink", "https://forms.cloud.microsoft/r/Ab12Cd34?embed=true")]
    public void Microsoft_forms_links_are_rebuilt(string input, string expected) => Assert.Equal(expected, Embed.Src(Embed.MicrosoftForms, input, Frappe));

    [Theory]
    [InlineData("https://app.powerbi.com/view?r=eyJrIjoi123=", "https://app.powerbi.com/view?r=eyJrIjoi123=")]
    [InlineData("https://app.powerbi.com/view?r=eyJrIjoi123&pageName=ReportSection1", "https://app.powerbi.com/view?r=eyJrIjoi123&pageName=ReportSection1")]
    public void Public_power_bi_reports_are_allowed(string input, string expected) => Assert.Equal(expected, Embed.Src(Embed.PowerBi, input, Frappe));

    [Theory]
    [InlineData("https://forms.ehc.example/patient-feedback", "https://forms.ehc.example/patient-feedback")]
    [InlineData("https://FORMS.ehc.example/volunteer/new?x=<script>", "https://forms.ehc.example/volunteer/new")]
    public void Frappe_web_forms_on_configured_hosts_are_allowed(string input, string expected) => Assert.Equal(expected, Embed.Src(Embed.Frappe, input, Frappe));

    [Theory]
    [InlineData(Embed.MicrosoftForms, "http://forms.office.com/r/Ab12Cd34")]                  // not https
    [InlineData(Embed.MicrosoftForms, "https://forms.office.com.evil.example/r/Ab12Cd34")]    // look-alike host
    [InlineData(Embed.MicrosoftForms, "https://user@forms.office.com/r/Ab12Cd34")]            // credentials
    [InlineData(Embed.MicrosoftForms, "https://forms.office.com/Pages/DesignPage.aspx")]      // editor, not a form
    [InlineData(Embed.MicrosoftForms, "https://app.powerbi.com/view?r=abcd")]                 // wrong provider
    [InlineData(Embed.PowerBi, "https://app.powerbi.com/reportEmbed?reportId=1")]            // secure embed (needs sign-in)
    [InlineData(Embed.PowerBi, "https://app.powerbi.com/view?r=abc\"><script>")]
    [InlineData(Embed.Frappe, "https://forms.other.example/patient-feedback")]               // host not configured
    [InlineData(Embed.Frappe, "https://forms.ehc.example/app/patient")]                       // Frappe desk
    [InlineData(Embed.Frappe, "https://forms.ehc.example/api/resource/User")]                 // Frappe API
    [InlineData(Embed.Frappe, "https://forms.ehc.example/")]
    [InlineData("other", "https://forms.office.com/r/Ab12Cd34")]
    [InlineData(Embed.MicrosoftForms, "javascript:alert(1)")]
    public void Everything_else_is_rejected(string provider, string input) => Assert.Null(Embed.Src(provider, input, Frappe));

    [Fact]
    public void Frame_sources_include_configured_frappe_hosts_only_when_valid()
    {
        var sources = Embed.FrameSources(new EmbedOptions { FrappeHosts = ["forms.ehc.example", "bad host", "*.evil.example"] }).ToList();
        Assert.Contains("https://forms.ehc.example", sources);
        Assert.Contains("https://app.powerbi.com", sources);
        Assert.DoesNotContain(sources, s => s.Contains("evil") || s.Contains("bad"));
        Assert.Equal(5, sources.Count);
    }
}
