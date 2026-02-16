namespace Backend.Services;

public class SadcValidationService : ISadcValidationService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CountryToCurrencies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["AO"] = ["AOA"],
        ["BW"] = ["BWP"],
        ["KM"] = ["KMF"],
        ["CD"] = ["CDF"],
        ["SZ"] = ["SZL"],
        ["LS"] = ["LSL"],
        ["MG"] = ["MGA"],
        ["MW"] = ["MWK"],
        ["MU"] = ["MUR"],
        ["MZ"] = ["MZN"],
        ["NA"] = ["NAD"],
        ["SC"] = ["SCR"],
        ["ZA"] = ["ZAR"],
        ["TZ"] = ["TZS"],
        ["ZM"] = ["ZMW"],
        ["ZW"] = ["ZWL", "USD"]
    };

    public bool IsValidCountryCode(string? countryCode) =>
        !string.IsNullOrWhiteSpace(countryCode) && CountryToCurrencies.ContainsKey(countryCode.Trim());

    public bool IsValidCurrencyForCountry(string? countryCode, string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(currencyCode))
            return false;
        var currencies = GetAllowedCurrenciesForCountry(countryCode);
        return currencies.Contains(currencyCode.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetAllowedCurrenciesForCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || !CountryToCurrencies.TryGetValue(countryCode.Trim(), out var list))
            return Array.Empty<string>();
        return list;
    }
}
