# Currency Exchange Office — System Documentation

Network Application Development — final project (Labs 5–14), including the
Lab 1 and Labs 2–4 exercises.

## 1. Technology overview

| Layer | Technology |
|---|---|
| Web Services | CoreWCF 1.5 (WCF for modern .NET), `BasicHttpBinding` (SOAP 1.1), WSDL metadata |
| Service clients | `System.ServiceModel` WCF client (`ChannelFactory<T>`) |
| External data | National Bank of Poland public API (http://api.nbp.pl/en.html), JSON |
| Database | SQLite via Entity Framework Core 8 (code-first, `EnsureCreated`) |
| Client application | WPF (.NET 8, Windows) |

All projects share service contracts through dedicated *Contracts* class
libraries. The contract interfaces are annotated with
`System.ServiceModel.ServiceContractAttribute`, which both the CoreWCF server
and the WCF client understand — so the server, the console clients and the WPF
client all compile against the same interface and DTO types, and communication
is plain SOAP over HTTP.

## 2. Components

### 2.1 Lab 1 — `Hello.*` (port 8081)

A minimal WCF service (`IHelloService` with `SayHello` and `GetServerTime`)
hosted in a console application, consumed by a console client through
`ChannelFactory<IHelloService>`. Demonstrates the contract → host → client
workflow used by all later projects.

### 2.2 Labs 2–4 — `Rates.*` (port 8082)

`IExchangeRateService.GetExchangeRate(string currencyCode)` returns the current
average exchange rate for the requested currency. The operation requires no
authorization. The service calls the NBP API endpoint:

```
GET https://api.nbp.pl/api/exchangerates/rates/a/{code}/?format=json
```

and maps the response to an `ExchangeRateInfo` DTO (code, currency name,
mid rate, effective date, NBP table number). Unknown currency codes and NBP
outages are reported to clients as SOAP faults with readable messages.

### 2.3 Final project — `Exchange.*` (port 8083)

```
+----------------+        SOAP/HTTP         +---------------------+   JSON/HTTPS   +---------+
| Exchange.Client| <----------------------> |  Exchange.Service   | <------------> | NBP API |
|     (WPF)      |  IExchangeService (WCF)  |  (CoreWCF + logic)  |                +---------+
+----------------+                          |         |           |
                                            |   EF Core (SQLite)  |
                                            |     exchange.db     |
                                            +---------------------+
```

#### Service operations (`IExchangeService`)

Public (no authorization), as required for rate access:

- `GetCurrentRate(code)` — current NBP table A mid rate for one currency.
- `GetAllCurrentRates()` — the whole current table A (~32 currencies).
- `GetHistoricalRates(code, from, to)` — historical quotations (max 367 days,
  an NBP API limit).
- `GetOfficeRate(code)` — the office's buy/sell prices for a currency.

Authenticated (username + password passed with each call):

- `RegisterUser`, `Login`
- `DepositPln` — simulated bank transfer topping up the PLN balance.
- `BuyCurrency`, `SellCurrency` — currency exchange against the PLN balance.
- `GetBalances`, `GetTransactionHistory`

Account operations return an `OperationResult { Success, Message }` so the
client can show validation errors (wrong password, insufficient funds, unknown
currency…) without dealing with SOAP faults.

#### Business rules

- Every account starts with a 0.00 PLN balance; foreign-currency wallets are
  created on first purchase.
- Office pricing is based on the live NBP table A mid rate with a **2% margin**:
  the office sells currency at `mid × 1.02` and buys at `mid × 0.98`.
- Buying checks the PLN balance, selling checks the foreign-currency balance;
  both record a transaction row with the rate applied and the PLN value.
- Amounts are rounded to 2 decimal places, rates to 4 (matching NBP precision).
- NBP responses are cached for 5 minutes to keep latency low and avoid
  hammering the public API.

#### Security

- Passwords are stored as PBKDF2 hashes (SHA-256, 100 000 iterations, random
  16-byte salt per user) and verified in constant time.
- Rate operations are anonymous; every account operation re-authenticates with
  the supplied credentials.

#### Database (SQLite, EF Core code-first)

Tables (see `database-schema.sql` for the full script):

- **Users** — `Id`, `Username` (unique), `PasswordHash`, `PasswordSalt`, `CreatedAt`
- **Balances** — `Id`, `UserId` → Users, `CurrencyCode`, `Amount`;
  unique index on (`UserId`, `CurrencyCode`)
- **Transactions** — `Id`, `UserId` → Users, `Type` (Deposit/Buy/Sell),
  `CurrencyCode`, `Amount`, `Rate`, `PlnValue`, `Timestamp`

The schema is created automatically on first service start
(`Database.EnsureCreated()`); the database file `exchange.db` lives next to
the service executable.

### 2.4 WPF client (`Exchange.Client`)

A single window with a login/registration screen and five tabs after signing
in:

1. **Exchange rates** — full NBP table A plus the office buy/sell prices for a
   selected currency.
2. **Historical rates** — quotations of a chosen currency in a date range.
3. **My account** — current balances and the simulated PLN transfer (top-up).
4. **Buy / sell currency** — exchange operations with a live price preview.
5. **Transaction history** — all of the user's operations.

All service calls run on background threads (`Task.Run` around a
`ChannelFactory` channel), so the UI never blocks; service-side validation
messages and connection problems are shown in the status bar.

## 3. Testing

`tools/SmokeTest` is an end-to-end console test run against a live service. It
covers all 11 service operations plus negative cases (unknown currency,
duplicate registration, wrong password, negative deposit, overdraft,
overselling) and verifies money conservation:
`deposit − purchase cost + sale proceeds = final PLN balance`.

Last full run: **18/18 PASS** (2026-06-10, live NBP data).

## 4. Possible extensions

- Sessions/tokens instead of per-call credentials (e.g. WS-Security).
- NBP table C (official bid/ask) instead of a synthetic margin.
- Rate charts in the client (historical data is already available).
- Administrative reporting (turnover per currency, office profit).
