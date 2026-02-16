using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class SadcValidationServiceTests
{
    private readonly ISadcValidationService _sadc = new SadcValidationService();

    [Theory]
    [InlineData("ZA", true)]
    [InlineData("BW", true)]
    [InlineData("ZW", true)]
    [InlineData("XX", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidCountryCode_ReturnsExpected(string? code, bool expected)
    {
        Assert.Equal(expected, _sadc.IsValidCountryCode(code));
    }

    [Theory]
    [InlineData("ZA", "ZAR", true)]
    [InlineData("ZW", "ZWL", true)]
    [InlineData("ZW", "USD", true)]
    [InlineData("ZA", "USD", false)]
    [InlineData("BW", "BWP", true)]
    public void IsValidCurrencyForCountry_ReturnsExpected(string country, string currency, bool expected)
    {
        Assert.Equal(expected, _sadc.IsValidCurrencyForCountry(country, currency));
    }

    [Fact]
    public void GetAllowedCurrenciesForCountry_ZW_ReturnsZWLAndUSD()
    {
        var list = _sadc.GetAllowedCurrenciesForCountry("ZW");
        Assert.Contains("ZWL", list);
        Assert.Contains("USD", list);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void GetAllowedCurrenciesForCountry_ZA_ReturnsZAR()
    {
        var list = _sadc.GetAllowedCurrenciesForCountry("ZA");
        Assert.Single(list);
        Assert.Equal("ZAR", list[0]);
    }
}
