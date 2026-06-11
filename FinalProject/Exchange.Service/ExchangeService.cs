using CoreWCF;
using Exchange.Contracts;
using Exchange.Service.Data;
using Exchange.Service.Nbp;
using Exchange.Service.Security;
using Microsoft.EntityFrameworkCore;

namespace Exchange.Service;

/// <summary>
/// Business logic of the currency exchange office.
/// Prices are based on the current NBP table A mid rate with a 2% margin:
/// the office sells currency at mid * 1.02 and buys it back at mid * 0.98.
/// </summary>
public class ExchangeService : IExchangeService
{
    /// <summary>Margin applied on top of the NBP mid rate (2%).</summary>
    private const decimal Margin = 0.02m;
    private const string Pln = "PLN";

    private readonly IDbContextFactory<ExchangeDbContext> _dbFactory;
    private readonly NbpApiClient _nbp;

    public ExchangeService(IDbContextFactory<ExchangeDbContext> dbFactory, NbpApiClient nbp)
    {
        _dbFactory = dbFactory;
        _nbp = nbp;
    }

    // ---------- Exchange rates (public) ----------

    public RateDto GetCurrentRate(string currencyCode) =>
        Guard(() => _nbp.GetCurrentRate(currencyCode));

    public List<RateDto> GetAllCurrentRates() =>
        Guard(() => _nbp.GetAllCurrentRates());

    public List<RateDto> GetHistoricalRates(string currencyCode, DateTime startDate, DateTime endDate) =>
        Guard(() => _nbp.GetHistoricalRates(currencyCode, startDate, endDate));

    public OfficeRateDto GetOfficeRate(string currencyCode) =>
        Guard(() =>
        {
            var rate = _nbp.GetCurrentRate(currencyCode);
            return new OfficeRateDto
            {
                Code = rate.Code,
                Currency = rate.Currency,
                NbpMid = rate.Mid,
                BuyPrice = BuyPrice(rate.Mid),
                SellPrice = SellPrice(rate.Mid),
                EffectiveDate = rate.EffectiveDate
            };
        });

    // ---------- User accounts ----------

    public OperationResult RegisterUser(string username, string password)
    {
        username = (username ?? "").Trim();
        if (username.Length is < 3 or > 64)
            return OperationResult.Fail("Username must be between 3 and 64 characters long.");
        if (string.IsNullOrEmpty(password) || password.Length < 4)
            return OperationResult.Fail("Password must be at least 4 characters long.");

        using var db = _dbFactory.CreateDbContext();
        if (db.Users.Any(u => u.Username == username))
            return OperationResult.Fail($"Username '{username}' is already taken.");

        var (hash, salt) = PasswordHasher.Hash(password);
        var user = new User
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt = DateTime.UtcNow
        };
        // every account starts with an (empty) PLN wallet
        user.Balances.Add(new Balance { CurrencyCode = Pln, Amount = 0m });

        db.Users.Add(user);
        db.SaveChanges();

        return OperationResult.Ok($"Account '{username}' has been created.");
    }

    public OperationResult Login(string username, string password)
    {
        using var db = _dbFactory.CreateDbContext();
        return Authenticate(db, username, password) is null
            ? OperationResult.Fail("Invalid username or password.")
            : OperationResult.Ok("Login successful.");
    }

    public OperationResult DepositPln(string username, string password, decimal amountPln)
    {
        if (amountPln <= 0)
            return OperationResult.Fail("Deposit amount must be greater than zero.");
        if (amountPln > 1_000_000m)
            return OperationResult.Fail("Single deposit is limited to 1 000 000 PLN.");

        amountPln = Math.Round(amountPln, 2);

        using var db = _dbFactory.CreateDbContext();
        var user = Authenticate(db, username, password);
        if (user is null)
            return OperationResult.Fail("Invalid username or password.");

        var plnBalance = GetOrCreateBalance(db, user.Id, Pln);
        plnBalance.Amount += amountPln;

        db.Transactions.Add(new Transaction
        {
            UserId = user.Id,
            Type = TransactionType.Deposit,
            CurrencyCode = Pln,
            Amount = amountPln,
            Rate = 1m,
            PlnValue = amountPln,
            Timestamp = DateTime.UtcNow
        });

        db.SaveChanges();
        return OperationResult.Ok($"Deposited {amountPln:0.00} PLN. Current balance: {plnBalance.Amount:0.00} PLN.");
    }

    // ---------- Currency exchange ----------

    public OperationResult BuyCurrency(string username, string password, string currencyCode, decimal amount)
    {
        var validation = ValidateExchangeInput(currencyCode, amount, out currencyCode);
        if (validation is not null)
            return validation;

        RateDto rate;
        try
        {
            rate = _nbp.GetCurrentRate(currencyCode);
        }
        catch (ArgumentException ex)
        {
            return OperationResult.Fail(ex.Message);
        }
        catch (Exception)
        {
            return OperationResult.Fail("The NBP API is currently unavailable. Please try again later.");
        }

        amount = Math.Round(amount, 2);
        var price = BuyPrice(rate.Mid);
        var costPln = Math.Round(amount * price, 2);

        using var db = _dbFactory.CreateDbContext();
        var user = Authenticate(db, username, password);
        if (user is null)
            return OperationResult.Fail("Invalid username or password.");

        var plnBalance = GetOrCreateBalance(db, user.Id, Pln);
        if (plnBalance.Amount < costPln)
            return OperationResult.Fail(
                $"Insufficient funds: buying {amount:0.00} {currencyCode} costs {costPln:0.00} PLN, " +
                $"but the account holds only {plnBalance.Amount:0.00} PLN.");

        var currencyBalance = GetOrCreateBalance(db, user.Id, currencyCode);
        plnBalance.Amount -= costPln;
        currencyBalance.Amount += amount;

        db.Transactions.Add(new Transaction
        {
            UserId = user.Id,
            Type = TransactionType.Buy,
            CurrencyCode = currencyCode,
            Amount = amount,
            Rate = price,
            PlnValue = costPln,
            Timestamp = DateTime.UtcNow
        });

        db.SaveChanges();
        return OperationResult.Ok(
            $"Bought {amount:0.00} {currencyCode} at {price:0.0000} PLN for {costPln:0.00} PLN. " +
            $"New balances: {currencyBalance.Amount:0.00} {currencyCode}, {plnBalance.Amount:0.00} PLN.");
    }

    public OperationResult SellCurrency(string username, string password, string currencyCode, decimal amount)
    {
        var validation = ValidateExchangeInput(currencyCode, amount, out currencyCode);
        if (validation is not null)
            return validation;

        RateDto rate;
        try
        {
            rate = _nbp.GetCurrentRate(currencyCode);
        }
        catch (ArgumentException ex)
        {
            return OperationResult.Fail(ex.Message);
        }
        catch (Exception)
        {
            return OperationResult.Fail("The NBP API is currently unavailable. Please try again later.");
        }

        amount = Math.Round(amount, 2);
        var price = SellPrice(rate.Mid);
        var proceedsPln = Math.Round(amount * price, 2);

        using var db = _dbFactory.CreateDbContext();
        var user = Authenticate(db, username, password);
        if (user is null)
            return OperationResult.Fail("Invalid username or password.");

        var currencyBalance = GetOrCreateBalance(db, user.Id, currencyCode);
        if (currencyBalance.Amount < amount)
            return OperationResult.Fail(
                $"Insufficient funds: the account holds only {currencyBalance.Amount:0.00} {currencyCode}.");

        var plnBalance = GetOrCreateBalance(db, user.Id, Pln);
        currencyBalance.Amount -= amount;
        plnBalance.Amount += proceedsPln;

        db.Transactions.Add(new Transaction
        {
            UserId = user.Id,
            Type = TransactionType.Sell,
            CurrencyCode = currencyCode,
            Amount = amount,
            Rate = price,
            PlnValue = proceedsPln,
            Timestamp = DateTime.UtcNow
        });

        db.SaveChanges();
        return OperationResult.Ok(
            $"Sold {amount:0.00} {currencyCode} at {price:0.0000} PLN for {proceedsPln:0.00} PLN. " +
            $"New balances: {currencyBalance.Amount:0.00} {currencyCode}, {plnBalance.Amount:0.00} PLN.");
    }

    // ---------- Account information ----------

    public List<BalanceDto> GetBalances(string username, string password)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = Authenticate(db, username, password)
                   ?? throw new FaultException("Invalid username or password.");

        return db.Balances
            .Where(b => b.UserId == user.Id)
            .OrderBy(b => b.CurrencyCode == Pln ? 0 : 1)
            .ThenBy(b => b.CurrencyCode)
            .Select(b => new BalanceDto { CurrencyCode = b.CurrencyCode, Amount = b.Amount })
            .ToList();
    }

    public List<TransactionDto> GetTransactionHistory(string username, string password)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = Authenticate(db, username, password)
                   ?? throw new FaultException("Invalid username or password.");

        return db.Transactions
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.Timestamp)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Type = t.Type.ToString(),
                CurrencyCode = t.CurrencyCode,
                Amount = t.Amount,
                Rate = t.Rate,
                PlnValue = t.PlnValue,
                Timestamp = t.Timestamp
            })
            .ToList();
    }

    // ---------- Helpers ----------

    private static decimal BuyPrice(decimal mid) => Math.Round(mid * (1m + Margin), 4);
    private static decimal SellPrice(decimal mid) => Math.Round(mid * (1m - Margin), 4);

    private static OperationResult? ValidateExchangeInput(string currencyCode, decimal amount, out string normalized)
    {
        normalized = (currencyCode ?? "").Trim().ToUpperInvariant();
        if (normalized.Length == 0)
            return OperationResult.Fail("Currency code must not be empty.");
        if (normalized == Pln)
            return OperationResult.Fail("PLN cannot be exchanged for itself.");
        if (amount <= 0)
            return OperationResult.Fail("Amount must be greater than zero.");
        return null;
    }

    private static User? Authenticate(ExchangeDbContext db, string username, string password)
    {
        username = (username ?? "").Trim();
        if (username.Length == 0 || string.IsNullOrEmpty(password))
            return null;

        var user = db.Users.SingleOrDefault(u => u.Username == username);
        if (user is null)
            return null;

        return PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt) ? user : null;
    }

    private static Balance GetOrCreateBalance(ExchangeDbContext db, int userId, string currencyCode)
    {
        var balance = db.Balances.SingleOrDefault(b => b.UserId == userId && b.CurrencyCode == currencyCode);
        if (balance is null)
        {
            balance = new Balance { UserId = userId, CurrencyCode = currencyCode, Amount = 0m };
            db.Balances.Add(balance);
        }
        return balance;
    }

    /// <summary>Translates internal exceptions into clean SOAP faults for rate queries.</summary>
    private static T Guard<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (ArgumentException ex)
        {
            throw new FaultException(ex.Message);
        }
        catch (Exception)
        {
            throw new FaultException("The NBP API is currently unavailable. Please try again later.");
        }
    }
}
