using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EHC.Web.Contact;

public class ContactTextTests
{
    [Theory]
    [InlineData("0551234567", "0551234567")]
    [InlineData("055 123 4567", "0551234567")]
    [InlineData("551234567", "0551234567")]
    [InlineData("+966 55 123 4567", "0551234567")]
    [InlineData("00966551234567", "0551234567")]
    [InlineData("966551234567", "0551234567")]
    [InlineData("٠٥٥١٢٣٤٥٦٧", "0551234567")]
    [InlineData("013-812-3456", "0138123456")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    [InlineData("(+20) 100-123-4567", "+201001234567")]
    [InlineData("05512+34567", null)]
    [InlineData("12345", null)]
    [InlineData("0991234567", null)]
    [InlineData("055123456", null)]
    [InlineData("+0123456789", null)]
    [InlineData("call 0551234567", null)]
    [InlineData("", null)]
    public void Phones_are_normalised(string input, string? expected) => Assert.Equal(expected, ContactText.Phone(input));

    [Theory]
    [InlineData("  Name@Example.com ", "name@example.com")]
    [InlineData("first.last+tag@ehc.med.sa", "first.last+tag@ehc.med.sa")]
    [InlineData("no-at-sign.example.com", null)]
    [InlineData("a@b.c", null)]
    [InlineData("two@@example.com", null)]
    [InlineData("John <john@example.com>", null)]
    public void Emails_are_checked(string input, string? expected) => Assert.Equal(expected, ContactText.Email(input));

    [Theory]
    [InlineData("  محمد   أحمد ", "محمد أحمد")]
    [InlineData("Sara\tAli", "Sara Ali")]
    [InlineData("A", null)]
    [InlineData("12345", null)]
    [InlineData(null, null)]
    public void Names_are_cleaned(string? input, string? expected) => Assert.Equal(expected, ContactText.Name(input));

    [Fact]
    public void Long_names_are_rejected() => Assert.Null(ContactText.Name(new string('a', ContactText.MaxName + 1)));

    [Fact]
    public void Messages_keep_line_breaks_but_not_runs_of_blank_lines()
    {
        Assert.Equal("Line one\n\nLine two\nthree", ContactText.Message("  Line   one\r\n\r\n\r\n \nLine two\nthree  "));
    }

    [Theory]
    [InlineData("too short")]
    [InlineData("          ")]
    [InlineData(null)]
    public void Short_messages_are_rejected(string? input) => Assert.Null(ContactText.Message(input));

    [Fact]
    public void Long_messages_are_rejected() => Assert.Null(ContactText.Message(new string('a', ContactText.MaxMessage + 1)));

    [Theory]
    [InlineData("complaint", "complaint")]
    [InlineData("volunteering", "volunteering")]
    [InlineData("Complaint", null)]
    [InlineData("GEN-SER005", null)]
    [InlineData(null, null)]
    public void Services_come_from_the_fixed_list(string? input, string? expected) => Assert.Equal(expected, ContactText.Service(input));

    [Fact]
    public void Services_keep_the_helpdesk_codes_of_the_current_site()
    {
        Assert.Equal(["GEN-SER005", "GEN-SER004", "GEN-SER003", "GEN-SER008", "GEN-SER007", "GEN-SER002"], ContactText.Services.Select(s => s.Code));
    }

    [Theory]
    [InlineData("closed", "closed")]
    [InlineData("deleted", null)]
    public void Statuses_come_from_the_fixed_list(string input, string? expected) => Assert.Equal(expected, ContactText.Status(input));

    [Fact]
    public void References_carry_the_date_and_an_unambiguous_code()
    {
        var reference = ContactText.NewReference(new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc));
        Assert.Matches(new Regex("^EHC-261009-[ACDEFGHJKMNPQRTUVWXY34679]{5}$"), reference);
        Assert.True(reference.Length <= 20);
    }

    [Theory]
    [InlineData("https://www.ehc.med.sa/ar/%D8%AA%D9%88%D8%A7%D8%B5%D9%84-%D9%85%D8%B9%D9%86%D8%A7/", true)]
    [InlineData("https://www.ehc.med.sa/ar/تواصل-معنا/", true)]
    [InlineData("https://www.ehc.med.sa/contact-us/", true)]
    [InlineData("https://www.ehc.med.sa/ar/privacy-policy/", false)]
    [InlineData("https://example.com/contact-us/", false)]
    [InlineData("/ar/تواصل-معنا/", false)]
    public void Old_contact_page_links_are_recognised(string url, bool expected) => Assert.Equal(expected, ContactPageSeeder.IsOldContactPage(url));

    [Fact]
    public void Old_contact_links_in_nested_blocks_point_to_the_new_page()
    {
        var key = Guid.NewGuid();
        var root = JsonNode.Parse("""
            {"contentData":[{"values":[{"alias":"link","value":[{"name":"شاركنا رأيك","target":"_blank","unique":null,"type":null,"udi":null,
              "url":"https://www.ehc.med.sa/ar/تواصل-معنا/","queryString":null,"culture":null}]},
              {"alias":"other","value":[{"name":"x","url":"https://www.ehc.med.sa/ar/privacy-policy/"}]}]}]}
            """);
        Assert.Equal(1, ContactPageSeeder.Repoint(root, key));
        var link = root!["contentData"]![0]!["values"]![0]!["value"]![0]!;
        Assert.Equal(key.ToString(), (string?)link["unique"]);
        Assert.Equal("document", (string?)link["type"]);
        Assert.Null(link["url"]);
        Assert.Equal("شاركنا رأيك", (string?)link["name"]);
        Assert.Equal("https://www.ehc.med.sa/ar/privacy-policy/", (string?)root["contentData"]![0]!["values"]![1]!["value"]![0]!["url"]);
    }

    [Fact]
    public void Old_contact_links_inside_json_strings_are_rewritten_as_strings()
    {
        // how the database keeps nested blocks: JSON text inside JSON text
        var link = new JsonArray(new JsonObject { ["name"] = "شاركنا رأيك", ["target"] = "_blank", ["url"] = "https://www.ehc.med.sa/ar/تواصل-معنا/" });
        var block = new JsonObject { ["values"] = new JsonArray(new JsonObject { ["alias"] = "link", ["value"] = link.ToJsonString() }) };
        var root = new JsonObject { ["contentData"] = new JsonArray(new JsonObject { ["values"] = new JsonArray(new JsonObject { ["alias"] = "columns", ["value"] = block.ToJsonString() }) }) };
        var key = Guid.NewGuid();

        Assert.Equal(1, ContactPageSeeder.Repoint(root, key));
        var outer = root["contentData"]![0]!["values"]![0]!["value"]!.GetValue<string>();
        var inner = JsonNode.Parse(outer)!["values"]![0]!["value"]!.GetValue<string>();
        var rewritten = JsonNode.Parse(inner)![0]!;
        Assert.Equal(key.ToString(), (string?)rewritten["unique"]);
        Assert.Null(rewritten["url"]);
    }
}
