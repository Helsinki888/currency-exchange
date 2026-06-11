# Currency Exchange — Network Application Development coursework

A set of .NET 8 projects covering the whole course: a simple WCF service (Lab 1),
an NBP exchange-rate Web Service (Labs 2–4) and the final project — an online
currency exchange office with a WCF service, SQLite database and WPF client
(Labs 5–14).

WCF services are implemented with **CoreWCF** — the official, Microsoft-supported
implementation of WCF for modern .NET. The programming model
(`[ServiceContract]`, `[OperationContract]`, `BasicHttpBinding`, WSDL metadata)
is identical to classic WCF, and the clients use the standard
`System.ServiceModel` WCF client libraries.

## Requirements

- .NET 8 SDK (Windows — the WPF client requires Windows)
- Internet access (the services call the public NBP API, http://api.nbp.pl/en.html)

## Solution layout

| Folder | Projects | Purpose |
|---|---|---|
| `Lab1/` | `Hello.Contracts`, `Hello.Service`, `Hello.Client` | Lab 1 — simple WCF service + console client |
| `Labs2to4/` | `Rates.Contracts`, `Rates.Service`, `Rates.Client` | Labs 2–4 — NBP exchange-rate Web Service |
| `FinalProject/` | `Exchange.Contracts`, `Exchange.Service`, `Exchange.Client` | Labs 5–14 — exchange office service, DB + WPF client |
| `tools/SmokeTest/` | `SmokeTest` | End-to-end test of the final service |
| `docs/` | — | Documentation and database schema script |

## How to run

Build everything once:

```
dotnet build CurrencyExchange.sln
```

### Lab 1

```
dotnet run --project Lab1/Hello.Service     # terminal 1 — http://localhost:8081
dotnet run --project Lab1/Hello.Client      # terminal 2
```

### Labs 2–4

```
dotnet run --project Labs2to4/Rates.Service # terminal 1 — http://localhost:8082
dotnet run --project Labs2to4/Rates.Client  # terminal 2 (interactive)
dotnet run --project Labs2to4/Rates.Client -- USD   # or one-shot
```

### Final project (Labs 5–14)

```
dotnet run --project FinalProject/Exchange.Service  # terminal 1 — http://localhost:8083
dotnet run --project FinalProject/Exchange.Client   # terminal 2 — WPF client
```

The service creates `exchange.db` (SQLite) next to its executable on first run.
In the client: create an account, sign in, top up PLN with a simulated
transfer, then buy and sell currencies at the office rates (NBP mid ± 2%).

### Smoke test

With `Exchange.Service` running:

```
dotnet run --project tools/SmokeTest
```

Exercises every service operation (rates, history, accounts, deposits,
buy/sell, balances, transaction history) and verifies money conservation.

## WSDL endpoints

- http://localhost:8081/HelloService.svc?wsdl
- http://localhost:8082/ExchangeRateService.svc?wsdl
- http://localhost:8083/ExchangeService.svc?wsdl

See [docs/Documentation.md](docs/Documentation.md) for the architecture
description and [docs/database-schema.sql](docs/database-schema.sql) for the
database schema.
