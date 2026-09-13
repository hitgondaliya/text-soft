using System.Net.Mail;
using System.Text.RegularExpressions;
using PhoneNumbers;

namespace FontSoftware.App.Services;

/// <summary>
/// Validation for the registration form. The mobile number is checked with
/// libphonenumber against the chosen country — the same library the server uses —
/// so "98250 00000" for India passes and "12345" does not.
/// </summary>
public static class RegistrationValidator
{
    private static readonly PhoneNumberUtil Phones = PhoneNumberUtil.GetInstance();

    public static string? NameError(string? name)
    {
        var n = (name ?? "").Trim();
        if (n.Length < 2) return "Enter your full name.";
        if (n.Length > 80) return "That name is too long.";
        if (!Regex.IsMatch(n, @"\p{L}")) return "Enter your name in letters.";
        return null;
    }

    public static string? EmailError(string? email)
    {
        var e = (email ?? "").Trim();
        if (e.Length == 0) return "Enter your email address.";
        if (e.Length > 120 || !e.Contains('@') || !e.Contains('.')) return "Enter a valid email address.";
        try
        {
            var addr = new MailAddress(e);
            if (!string.Equals(addr.Address, e, StringComparison.OrdinalIgnoreCase)) return "Enter a valid email address.";
        }
        catch
        {
            return "Enter a valid email address.";
        }
        return null;
    }

    /// <summary>Null when valid; sets <paramref name="national"/> to the digits to send.</summary>
    public static string? PhoneError(string? countryCode, string? phone, out string national)
    {
        national = "";
        var digits = Regex.Replace(phone ?? "", @"[^\d]", "");
        if (digits.Length == 0) return "Enter your mobile number.";
        if (digits.Length < 4 || digits.Length > 15) return "That mobile number is not valid.";
        var region = (countryCode ?? CountryCatalog.DefaultCode).ToUpperInvariant();
        try
        {
            var parsed = Phones.Parse(digits, region);
            if (!Phones.IsValidNumberForRegion(parsed, region))
                return $"That is not a valid number for {CountryCatalog.Find(region)?.Name ?? region}.";
            var type = Phones.GetNumberType(parsed);
            if (type is not (PhoneNumberType.MOBILE or PhoneNumberType.FIXED_LINE_OR_MOBILE or PhoneNumberType.FIXED_LINE))
                return "Enter a mobile number.";
            national = parsed.NationalNumber.ToString();
            return null;
        }
        catch (NumberParseException)
        {
            return "That mobile number is not valid.";
        }
    }
}
