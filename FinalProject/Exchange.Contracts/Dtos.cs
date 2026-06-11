using System.Runtime.Serialization;

namespace Exchange.Contracts;

[DataContract]
public class RateDto
{
    [DataMember] public string Code { get; set; } = "";
    [DataMember] public string Currency { get; set; } = "";
    /// <summary>NBP average ("mid") rate in PLN.</summary>
    [DataMember] public decimal Mid { get; set; }
    [DataMember] public DateTime EffectiveDate { get; set; }
}

[DataContract]
public class OfficeRateDto
{
    [DataMember] public string Code { get; set; } = "";
    [DataMember] public string Currency { get; set; } = "";
    /// <summary>NBP average rate the prices are based on.</summary>
    [DataMember] public decimal NbpMid { get; set; }
    /// <summary>Price at which the office SELLS the currency to the user (user buys).</summary>
    [DataMember] public decimal BuyPrice { get; set; }
    /// <summary>Price at which the office BUYS the currency from the user (user sells).</summary>
    [DataMember] public decimal SellPrice { get; set; }
    [DataMember] public DateTime EffectiveDate { get; set; }
}

[DataContract]
public class OperationResult
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public string Message { get; set; } = "";

    public static OperationResult Ok(string message = "OK") =>
        new() { Success = true, Message = message };

    public static OperationResult Fail(string message) =>
        new() { Success = false, Message = message };
}

[DataContract]
public class BalanceDto
{
    [DataMember] public string CurrencyCode { get; set; } = "";
    [DataMember] public decimal Amount { get; set; }
}

[DataContract]
public class TransactionDto
{
    [DataMember] public int Id { get; set; }
    /// <summary>Deposit, Buy or Sell.</summary>
    [DataMember] public string Type { get; set; } = "";
    [DataMember] public string CurrencyCode { get; set; } = "";
    /// <summary>Amount of foreign currency (or PLN for deposits).</summary>
    [DataMember] public decimal Amount { get; set; }
    /// <summary>Exchange rate applied (1 for deposits).</summary>
    [DataMember] public decimal Rate { get; set; }
    /// <summary>Value of the operation in PLN.</summary>
    [DataMember] public decimal PlnValue { get; set; }
    [DataMember] public DateTime Timestamp { get; set; }
}
