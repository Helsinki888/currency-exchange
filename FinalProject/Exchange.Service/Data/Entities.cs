namespace Exchange.Service.Data;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public List<Balance> Balances { get; set; } = new();
    public List<Transaction> Transactions { get; set; } = new();
}

public class Balance
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal Amount { get; set; }

    public User? User { get; set; }
}

public enum TransactionType
{
    Deposit,
    Buy,
    Sell
}

public class Transaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public TransactionType Type { get; set; }
    public string CurrencyCode { get; set; } = "";
    /// <summary>Amount of foreign currency (or PLN for deposits).</summary>
    public decimal Amount { get; set; }
    /// <summary>Exchange rate applied (1 for deposits).</summary>
    public decimal Rate { get; set; }
    /// <summary>Value of the operation in PLN.</summary>
    public decimal PlnValue { get; set; }
    public DateTime Timestamp { get; set; }

    public User? User { get; set; }
}
