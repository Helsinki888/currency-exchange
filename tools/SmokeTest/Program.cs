using System.ServiceModel;
using Exchange.Contracts;

// End-to-end smoke test of the Exchange.Service WCF service.
// Requires the service to be running on http://localhost:8083.

var factory = new ChannelFactory<IExchangeService>(
    new BasicHttpBinding { MaxReceivedMessageSize = 4 * 1024 * 1024 },
    new EndpointAddress("http://localhost:8083/ExchangeService.svc"));

var s = factory.CreateChannel();
var user = "smoketest_" + DateTime.Now.ToString("HHmmss");
var pass = "test1234";
var failed = false;

void Check(string step, bool ok, string detail)
{
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {step}: {detail}");
    if (!ok) failed = true;
}

// 1. Public rate queries
var usd = s.GetCurrentRate("USD");
Check("GetCurrentRate", usd.Code == "USD" && usd.Mid > 0, $"USD mid = {usd.Mid} PLN ({usd.EffectiveDate:yyyy-MM-dd})");

var all = s.GetAllCurrentRates();
Check("GetAllCurrentRates", all.Count > 10, $"{all.Count} currencies in table A");

var hist = s.GetHistoricalRates("EUR", DateTime.Today.AddDays(-30), DateTime.Today);
Check("GetHistoricalRates", hist.Count > 5, $"{hist.Count} EUR quotations in the last 30 days");

var office = s.GetOfficeRate("EUR");
Check("GetOfficeRate", office.BuyPrice > office.NbpMid && office.SellPrice < office.NbpMid,
    $"EUR: we sell {office.BuyPrice}, we buy {office.SellPrice}, mid {office.NbpMid}");

// Invalid currency must produce a clean fault
try
{
    s.GetCurrentRate("XXX");
    Check("Invalid currency fault", false, "no fault thrown");
}
catch (FaultException ex)
{
    Check("Invalid currency fault", true, ex.Message);
}

// 2. Accounts
var reg = s.RegisterUser(user, pass);
Check("RegisterUser", reg.Success, reg.Message);

var dupe = s.RegisterUser(user, pass);
Check("Duplicate username rejected", !dupe.Success, dupe.Message);

var badLogin = s.Login(user, "wrong-password");
Check("Wrong password rejected", !badLogin.Success, badLogin.Message);

var login = s.Login(user, pass);
Check("Login", login.Success, login.Message);

// 3. Deposit
var dep = s.DepositPln(user, pass, 10_000m);
Check("DepositPln", dep.Success, dep.Message);

var negDep = s.DepositPln(user, pass, -5m);
Check("Negative deposit rejected", !negDep.Success, negDep.Message);

// 4. Buy / sell
var buy = s.BuyCurrency(user, pass, "USD", 100m);
Check("BuyCurrency", buy.Success, buy.Message);

var tooMuch = s.BuyCurrency(user, pass, "USD", 1_000_000m);
Check("Overdraft rejected", !tooMuch.Success, tooMuch.Message);

var sell = s.SellCurrency(user, pass, "USD", 40m);
Check("SellCurrency", sell.Success, sell.Message);

var oversell = s.SellCurrency(user, pass, "USD", 10_000m);
Check("Overselling rejected", !oversell.Success, oversell.Message);

// 5. Balances + history
var balances = s.GetBalances(user, pass);
var pln = balances.Single(b => b.CurrencyCode == "PLN").Amount;
var usdBal = balances.Single(b => b.CurrencyCode == "USD").Amount;
Check("GetBalances", usdBal == 60m && pln is > 0 and < 10_000m,
    string.Join(", ", balances.Select(b => $"{b.Amount:0.00} {b.CurrencyCode}")));

var tx = s.GetTransactionHistory(user, pass);
Check("GetTransactionHistory", tx.Count == 3,
    $"{tx.Count} transactions: " + string.Join("; ", tx.Select(t => $"{t.Type} {t.Amount:0.00} {t.CurrencyCode} @ {t.Rate}")));

// Money conservation: deposit - buy cost + sell proceeds == PLN balance
var expectedPln = 10_000m - tx.Single(t => t.Type == "Buy").PlnValue + tx.Single(t => t.Type == "Sell").PlnValue;
Check("PLN balance consistent", pln == expectedPln, $"balance {pln:0.00} == expected {expectedPln:0.00}");

((IClientChannel)s).Close();
factory.Close();

Console.WriteLine();
Console.WriteLine(failed ? "SMOKE TEST FAILED" : "ALL SMOKE TESTS PASSED");
Environment.Exit(failed ? 1 : 0);
