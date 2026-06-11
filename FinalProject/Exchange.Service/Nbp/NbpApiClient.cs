using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Exchange.Contracts;

namespace Exchange.Service.Nbp;

/// <summary>
/// Client for the National Bank of Poland public API (http://api.nbp.pl/en.html).
/// Current rates are cached for 5 minutes so the NBP API is not queried
/// on every service call.
/// </summary>
public class NbpApiClient
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.nbp.pl/api/"),
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, (RateDto Rate, DateTime FetchedAt)> _rateCache = new();
    private (List<RateDto> Rates, DateTime FetchedAt)? _tableCache;
    private readonly object _tableLock = new();

    /// <summary>Current table A mid rate for a single currency.</summary>
    public RateDto GetCurrentRate(string currencyCode)
    {
        currencyCode = Normalize(currencyCode);

        if (_rateCache.TryGetValue(currencyCode, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheTtl)
        {
            return cached.Rate;
        }

        var json = Get($"exchangerates/rates/a/{currencyCode}/?format=json",
            notFoundMessage: $"Currency code '{currencyCode}' was not found in NBP table A.");

        var doc = JsonSerializer.Deserialize<NbpRatesResponse>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Empty response from the NBP API.");

        var r = doc.Rates.First();
        var rate = new RateDto
        {
            Code = doc.Code,
            Currency = doc.Currency,
            Mid = r.Mid,
            EffectiveDate = ParseDate(r.EffectiveDate)
        };

        _rateCache[currencyCode] = (rate, DateTime.UtcNow);
        return rate;
    }

    /// <summary>All current table A rates.</summary>
    public List<RateDto> GetAllCurrentRates()
    {
        lock (_tableLock)
        {
            if (_tableCache is { } cached && DateTime.UtcNow - cached.FetchedAt < CacheTtl)
                return cached.Rates;
        }

        var json = Get("exchangerates/tables/a/?format=json",
            notFoundMessage: "NBP table A is not available.");

        var tables = JsonSerializer.Deserialize<List<NbpTableResponse>>(json, JsonOptions)
                     ?? throw new InvalidOperationException("Empty response from the NBP API.");

        var table = tables.First();
        var date = ParseDate(table.EffectiveDate);
        var rates = table.Rates
            .Select(r => new RateDto
            {
                Code = r.Code,
                Currency = r.Currency,
                Mid = r.Mid,
                EffectiveDate = date
            })
            .OrderBy(r => r.Code)
            .ToList();

        lock (_tableLock)
        {
            _tableCache = (rates, DateTime.UtcNow);
        }
        return rates;
    }

    /// <summary>Historical table A rates for a currency within a date range.</summary>
    public List<RateDto> GetHistoricalRates(string currencyCode, DateTime start, DateTime end)
    {
        currencyCode = Normalize(currencyCode);

        if (end < start)
            throw new ArgumentException("End date must not be earlier than start date.");
        if ((end - start).TotalDays > 367)
            throw new ArgumentException("The NBP API limits a single query to 367 days.");

        var from = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var json = Get($"exchangerates/rates/a/{currencyCode}/{from}/{to}/?format=json",
            notFoundMessage: $"No NBP data for '{currencyCode}' in the given date range.");

        var doc = JsonSerializer.Deserialize<NbpRatesResponse>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Empty response from the NBP API.");

        return doc.Rates
            .Select(r => new RateDto
            {
                Code = doc.Code,
                Currency = doc.Currency,
                Mid = r.Mid,
                EffectiveDate = ParseDate(r.EffectiveDate)
            })
            .ToList();
    }

    private static string Get(string url, string notFoundMessage)
    {
        using var response = Http.GetAsync(url).GetAwaiter().GetResult();
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new ArgumentException(notFoundMessage);
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private static string Normalize(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code must not be empty.");
        return currencyCode.Trim().ToUpperInvariant();
    }

    private static DateTime ParseDate(string date) =>
        DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // --- NBP API response shapes ---

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

    private sealed class NbpTableResponse
    {
        public string Table { get; set; } = "";
        public string No { get; set; } = "";
        public string EffectiveDate { get; set; } = "";
        public List<NbpTableRate> Rates { get; set; } = new();
    }

    private sealed class NbpTableRate
    {
        public string Currency { get; set; } = "";
        public string Code { get; set; } = "";
        public decimal Mid { get; set; }
    }
}
