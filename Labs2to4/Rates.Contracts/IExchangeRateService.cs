using System.Runtime.Serialization;
using System.ServiceModel;

namespace Rates.Contracts;

/// <summary>
/// Labs 2–4 — a WCF service returning current exchange rates from the
/// National Bank of Poland (NBP) public API. No authorization required.
/// </summary>
[ServiceContract]
public interface IExchangeRateService
{
    /// <summary>
    /// Returns the current average exchange rate (NBP table A) for the
    /// given ISO currency code, e.g. "USD", "EUR", "GBP".
    /// </summary>
    [OperationContract]
    ExchangeRateInfo GetExchangeRate(string currencyCode);
}

[DataContract]
public class ExchangeRateInfo
{
    /// <summary>ISO 4217 code, e.g. "USD".</summary>
    [DataMember]
    public string Code { get; set; } = "";

    /// <summary>Full currency name as returned by NBP.</summary>
    [DataMember]
    public string Currency { get; set; } = "";

    /// <summary>Average exchange rate in PLN (NBP "mid" rate).</summary>
    [DataMember]
    public decimal Rate { get; set; }

    /// <summary>Date the rate was published by NBP.</summary>
    [DataMember]
    public string EffectiveDate { get; set; } = "";

    /// <summary>NBP table number, e.g. "111/A/NBP/2026".</summary>
    [DataMember]
    public string TableNumber { get; set; } = "";
}
