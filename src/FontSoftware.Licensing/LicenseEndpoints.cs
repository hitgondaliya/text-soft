namespace FontSoftware.Licensing;

/// <summary>
/// Where the licence server lives. A compile-time constant on purpose: it used to
/// sit in the user-editable settings.json, which let anyone point the app at a
/// server of their own. The signature check would still reject that server's
/// tokens, but there is no reason to hand out the knob at all.
///
/// scripts\set-prod-config.ps1 stamps the production HTTPS URL in here before a
/// release build. Debug builds may override it with the TEXTSOFT_API_URL
/// environment variable so a developer can test against a local panel.
/// </summary>
public static class LicenseEndpoints
{
    // The live licence server. scripts\set-prod-config.ps1 stamps this.
    public const string BaseUrl = "https://admin.text-soft.com";

    public static string Resolve()
    {
#if DEBUG
        var o = Environment.GetEnvironmentVariable("TEXTSOFT_API_URL");
        if (!string.IsNullOrWhiteSpace(o)) return o.Trim();
#endif
        return BaseUrl;
    }
}

