using System.ServiceModel;

namespace Exchange.Contracts;

/// <summary>
/// Final project (Labs 5–14) — service contract of the online currency
/// exchange office. Rate queries are public; account operations are
/// authenticated with username + password.
/// </summary>
[ServiceContract]
public interface IExchangeService
{
    // ---------- Exchange rates (no authorization required) ----------

    /// <summary>Current NBP table A average rate for one currency.</summary>
    [OperationContract]
    RateDto GetCurrentRate(string currencyCode);

    /// <summary>All current NBP table A rates.</summary>
    [OperationContract]
    List<RateDto> GetAllCurrentRates();

    /// <summary>Historical NBP table A rates for a currency in a date range (max 1 year).</summary>
    [OperationContract]
    List<RateDto> GetHistoricalRates(string currencyCode, DateTime startDate, DateTime endDate);

    /// <summary>Buy/sell prices offered by the exchange office (NBP mid ± margin).</summary>
    [OperationContract]
    OfficeRateDto GetOfficeRate(string currencyCode);

    // ---------- User accounts ----------

    [OperationContract]
    OperationResult RegisterUser(string username, string password);

    [OperationContract]
    OperationResult Login(string username, string password);

    /// <summary>Simulated bank transfer — tops up the user's PLN balance.</summary>
    [OperationContract]
    OperationResult DepositPln(string username, string password, decimal amountPln);

    // ---------- Currency exchange ----------

    /// <summary>Buys <paramref name="amount"/> units of foreign currency for PLN.</summary>
    [OperationContract]
    OperationResult BuyCurrency(string username, string password, string currencyCode, decimal amount);

    /// <summary>Sells <paramref name="amount"/> units of foreign currency back to PLN.</summary>
    [OperationContract]
    OperationResult SellCurrency(string username, string password, string currencyCode, decimal amount);

    // ---------- Account information ----------

    [OperationContract]
    List<BalanceDto> GetBalances(string username, string password);

    [OperationContract]
    List<TransactionDto> GetTransactionHistory(string username, string password);
}
