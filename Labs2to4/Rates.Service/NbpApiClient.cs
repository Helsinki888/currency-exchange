using System.Text.Json;
using Rates.Contracts;

namespace Rates.Service;

/// <summary>
/// Thin client for the National Bank of Poland public API
/// (https://api.nbp.pl/ — see http://api.nbp.pl/en.html).
/// </summary>
public class NbpApiClient
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.nbp.pl/api/"),
        Timeout = TimeSpan.FromSeconds(15)
    };

    public ExchangeRateInfo GetCurrentRate(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code must not be empty.");

        currencyCode = currencyCode.Trim().ToUpperInvariant();

        // Example: https://api.nbp.pl/api/exchangerates/rates/a/usd/?format=json
        var url = $"exchangerates/rates/a/{currencyCode}/?format=json";

        using var response = Http.GetAsync(url).GetAwaiter().GetResult();
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new ArgumentException($"Currency code '{currencyCode}' was not found in NBP table A.");

        response.EnsureSuccessStatusCode();

        var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var doc = JsonSerializer.Deserialize<NbpRatesResponse>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Empty response from the NBP API.");

        var rate = doc.Rates.First();
        return new ExchangeRateInfo
        {
            Code = doc.Code,
            Currency = doc.Currency,
            Rate = rate.Mid,
            EffectiveDate = rate.EffectiveDate,
            TableNumber = rate.No
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // --- NBP API response shape ---

    private sealed class NbpRatesResponse
    {
        public string Table { get; set; } = "";
        public string Currency { get; set; } = "";
        public string Code { get; set; } = "";
        public List<NbpRate> Rates { get; set; } = new();
    }

    private sealed class NbpRate
    {
        public string No { get; set; } = "";
        public string EffectiveDate { get; set; } = "";
        public decimal Mid { get; set; }
    }
}
