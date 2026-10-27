# rebalanced

Rebalances an investment portfolio that is spread across several accounts with different tax treatment (taxable brokerage, Roth IRA, Roth 401(k), crypto wallets, private real estate). Given target allocations for the whole portfolio, it works out what to buy and sell in each account, keeping each account's total value fixed, so money never has to move between accounts.

The goal is tax-aware rebalancing: hold each asset in the account type where it is taxed least, and do the buying and selling where trades do not realize gains. The main entry point is a command-line tool that reads the "investment rebalance" spreadsheet and prints the trades to enter by hand at Vanguard.

## Quick start

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Download the spreadsheet as `.xlsx` (File, Download, Microsoft Excel), then:

```bash
dotnet run --project src/ReBalanced.Cli -- "investment rebalance.xlsx"
dotnet run --project src/ReBalanced.Cli -- "investment rebalance.xlsx" --crypto include --cash include
```

| Option | Effect | Default |
|---|---|---|
| `--crypto include\|ignore` | Rebalance crypto, or hold it as is and leave it out of the total | the sheet's `INCLUDE CRYPTO` |
| `--cash include\|ignore` | Count bank balances (net of credit cards) as cash to invest, or leave them out | the sheet's `INCLUDE BANKS` |
| `--cash-target <dollars>` | Cash to keep uninvested | the sheet's `CASH` target |
| `--band <fraction>` | How far an asset class may drift before the taxable account sells, e.g. `0.01` is 1 percentage point | `0` (exact targets, like the sheet) |
| `--live-prices` | Price holdings with Twelve Data instead of the prices saved in the sheet | off |
| `--us-share <f>` | US share of stocks | from the sheet (55%) |
| `--developed-share <f>` | Developed share of international stocks; the rest is emerging | from the sheet (75%) |
| `--bond-share <f>` | Bonds as a share of what is left after cash, real estate and crypto | from the sheet (6%) |
| `--min-real-estate <f>` | Real estate floor, topped up with a REIT | from the sheet (5%) |
| `--crypto-allocation <f>` | Crypto share; only used with crypto included | from the sheet (10%) |

The report's second line shows the ratios in use.

The output lists every account and whether it is traded or held, a bank transfer when `--cash include` finds money to invest, the trades per account as dollar amounts (Vanguard accepts dollar-based orders) with approximate share counts, and a table of target, current and resulting allocation per asset class.

## How the command-line tool works

**Import.** `SpreadsheetImporter` reads the values the sheet last calculated (Google Sheets functions such as `GOOGLEFINANCE` cannot be recalculated outside Google). Below the summary table, each section is classified by its title: the taxable account and the Roth IRA are traded; the 401(k), the crypto wallets and anything unrecognized, which is treated as real estate, are held as they are but count toward the total. Bank rows are summed into one cash balance. A ticker the importer does not know is an error; add it to `AssetClasses`.

**Targets.** `AllocationTargets` reproduces the sheet's formulas:

- Real estate counts at its current value. If it is under 5% of the total, a REIT allocation tops it up to 5%.
- Cash gets its dollar target. Crypto gets the crypto allocation (10%), split between bitcoin and ethereum by market cap, or nothing when crypto is ignored.
- Bonds get 6% of what remains, and stocks get the rest: 55% US (`VTI`), then 75/25 developed (`VEA`) and emerging (`VWO`).

These ratios are constants inside the sheet's formulas, not cells, so the importer works them back out from the calculated values: the US share from the `VTI`, `VEA` and `VWO` Target % cells, the bond share from "bond allocation", and the real estate floor from "reit allocation" (or from the number in its formula when real estate is above the floor and the cell is 0). Any ratio it cannot work out falls back to the value above with a warning, and every ratio can be overridden on the command line.

Holdings are grouped into asset classes, so the 401(k)'s `TMBRX` counts as bonds, and `GBTC`, the `BTC` mini trust and wallet bitcoin all count as bitcoin.

**Placement.** Each asset class may only be held in its tax-efficient home, following the Bogleheads [tax-efficient fund placement](https://www.bogleheads.org/wiki/Tax-efficient_fund_placement) guidance (`TaxEfficientPlacement`):

| Asset class | Home | Why |
|---|---|---|
| Bonds, REIT | Roth | Least tax-efficient: interest and REIT dividends are taxed as ordinary income |
| Bitcoin, Ethereum | Roth | Not covered by Bogleheads; held in the Roth for its expected growth |
| Developed and emerging stocks | Taxable | The foreign tax credit only works in a taxable account |
| US stocks | Either | Tax-efficient; fills whatever space is left |
| Cash | Either, leaning taxable | Keeps the Roth invested |

Placement wins over avoiding taxable sales: a holding outside its home is sold, and keeping international in the taxable account can mean selling taxable `VTI`. If a home account type is missing from the portfolio, that class may go in any traded account, with a warning.

**Trades.** `TaxAwareRebalancer` is a linear program in dollars, solved with [Google OR-Tools](https://developers.google.com/optimization) (GLOP). For each traded account it chooses the final dollar amount of every ticker it holds, plus each allowed class's preferred ticker. Each account's total stays the same. The objective, cheapest first:

1. Drift beyond the band is fixed almost at any cost. With the default band of 0 that means every class lands on its target.
2. Selling in the taxable account costs more than drift inside the band is worth, so with a band above 0 it rebalances inside the band only with cash and Roth trades. Apart from sales that placement forces, it never sells in the taxable account when a Roth trade can do the same job.
3. Cash and US stocks, which have two homes, lean to the first one listed above.
4. New money goes to each class's preferred, lower-fee ticker (`BTC` over `GBTC`, `ETH` over `ETHE`).

## Other components

The repository also has an ASP.NET Core API and the original share-based solver, `RebalanceService`. It solves for whole or fractional share counts with OR-Tools' SCIP solver, widening a tolerance band until the targets fit, and uses the ticker lists in `AccountRules` for asset location. It prices assets through Twelve Data with a one-hour LiteDB cache. The API has no controllers yet.

| Project | Contents |
|---|---|
| `src/ReBalanced.Cli` | The command-line tool: spreadsheet import, options, report |
| `src/ReBalanced.Domain` | `Portfolio`, `Account`, `Holding`, `Asset`, `AssetClass`, `TaxEfficientPlacement`, `AccountRules` |
| `src/ReBalanced.Application` | `TaxAwareRebalancer`, `AllocationTargets`, `RebalanceService` and supporting services |
| `src/ReBalanced.Infrastructure` | Twelve Data price client, LiteDB price cache and portfolio repository |
| `src/ReBalanced.API` | ASP.NET Core host: dependency wiring, Swagger, logging |
| `tests/` | xUnit tests for each project |
| `scripts/rebalance.linq` | The original LINQPad prototype |

## Twelve Data key

`--live-prices` and the API need a Twelve Data key. Put it in `src/ReBalanced.API/appsettings.Development.json`, which is gitignored and shared with the command-line tool:

```json
{ "TwelveData": { "ApiKey": "..." } }
```

or set the environment variable `TwelveData__ApiKey`. The API reads the file only when `ASPNETCORE_ENVIRONMENT=Development`, and will not start without a key.

The key is sent in the `Authorization` header, never in the URL. Each symbol in a request costs one credit, and the free plan allows 8 credits a minute. The spreadsheet has about 12 priced symbols, so `--live-prices` on the free plan can be rejected; any symbol without a live price falls back to the sheet's price with a warning.

## Tests

```bash
dotnet test --filter "Category!=Integration"
```

Tests tagged `Category=Integration` call the live Twelve Data API. To run them, put the key in `tests/ReBalanced.Infrastructure.Tests/appsettings.development.json` (also gitignored) and run `dotnet test --filter "Category=Integration"`.

## Known limitations

- **No cost basis.** The sheet has no per-lot cost basis, so the tool cannot choose which lots to sell or harvest losses. Every taxable sell is treated as equally costly; check each lot before selling.
- **The spreadsheet layout is assumed.** The importer finds cells by their labels (`Total Investment`, `crypto allocation`, `Market Cap`, `INCLUDE CRYPTO`, `INCLUDE BANKS`) and sections by their titles. Renaming them breaks the import.
- **Saved portfolios lose their accounts.** LiteDB cannot rebuild `Portfolio.Accounts` or an account's holdings. The round-trip test is skipped until this is fixed.
- **Overlapping equivalent targets in `RebalanceService`.** If both an asset and its equivalent are allocated (say `bitcoin` and `GBTC`), each target is applied to the same holding separately. `TaxAwareRebalancer` does not have this problem because it works on asset classes.
