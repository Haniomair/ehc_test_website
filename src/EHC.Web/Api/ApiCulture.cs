namespace EHC.Web.Api;

/// <summary>Maps the `culture` query value (ar, en, ar-SA, en-US) to a site culture; Arabic is the default.</summary>
public static class ApiCulture
{
    public static string From(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "en" or "en-us" => "en-US",
            _ => "ar-SA",
        };
}
