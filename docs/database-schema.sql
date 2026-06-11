-- Currency Exchange Office — SQLite database schema
-- The schema is normally created automatically by EF Core (Database.EnsureCreated()).
-- This script documents the resulting structure and can recreate it manually.

CREATE TABLE "Users" (
    "Id"           INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
    "Username"     TEXT    NOT NULL,           -- max 64 chars, unique
    "PasswordHash" TEXT    NOT NULL,           -- PBKDF2-SHA256, Base64
    "PasswordSalt" TEXT    NOT NULL,           -- 16-byte random salt, Base64
    "CreatedAt"    TEXT    NOT NULL            -- UTC timestamp
);

CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");

CREATE TABLE "Balances" (
    "Id"           INTEGER NOT NULL CONSTRAINT "PK_Balances" PRIMARY KEY AUTOINCREMENT,
    "UserId"       INTEGER NOT NULL,
    "CurrencyCode" TEXT    NOT NULL,           -- ISO 4217, e.g. 'PLN', 'USD'
    "Amount"       TEXT    NOT NULL,           -- decimal stored as text by EF Core
    CONSTRAINT "FK_Balances_Users_UserId"
        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- one wallet per user per currency
CREATE UNIQUE INDEX "IX_Balances_UserId_CurrencyCode" ON "Balances" ("UserId", "CurrencyCode");

CREATE TABLE "Transactions" (
    "Id"           INTEGER NOT NULL CONSTRAINT "PK_Transactions" PRIMARY KEY AUTOINCREMENT,
    "UserId"       INTEGER NOT NULL,
    "Type"         TEXT    NOT NULL,           -- 'Deposit' | 'Buy' | 'Sell'
    "CurrencyCode" TEXT    NOT NULL,           -- currency traded ('PLN' for deposits)
    "Amount"       TEXT    NOT NULL,           -- amount of currency (PLN for deposits)
    "Rate"         TEXT    NOT NULL,           -- exchange rate applied (1 for deposits)
    "PlnValue"     TEXT    NOT NULL,           -- operation value in PLN
    "Timestamp"    TEXT    NOT NULL,           -- UTC timestamp
    CONSTRAINT "FK_Transactions_Users_UserId"
        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Transactions_UserId" ON "Transactions" ("UserId");
