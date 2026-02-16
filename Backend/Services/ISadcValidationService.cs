namespace Backend.Services;

public interface ISadcValidationService
{
    bool IsValidCountryCode(string? countryCode);
    bool IsValidCurrencyForCountry(string? countryCode, string? currencyCode);
    IReadOnlyList<string> GetAllowedCurrenciesForCountry(string? countryCode);
}
