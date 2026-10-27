using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Cli.Tests;

public class JsonImporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

    public void Dispose()
    {
        File.Delete(_path);
    }

    private ImportedPortfolio Import(string json)
    {
        File.WriteAllText(_path, json);
        return JsonImporter.Import(_path);
    }

    private const string Balances = """
        {
          "settings": { "includeCrypto": true, "cashTarget": 500, "ratios": { "usShare": 0.6 } },
          "accounts": [
            { "name": "Taxable", "kind": "taxable", "holdings": [
              { "ticker": "VTI", "shares": 10, "price": 100 }, { "ticker": "CASH", "value": 50 } ] },
            { "name": "Roth IRA", "kind": "rothIra", "holdings": [ { "ticker": "GBTC", "shares": 5, "price": 40 } ] },
            { "name": "401(k)", "kind": "retirement401k", "holdings": [ { "ticker": "TMBRX", "shares": 100, "price": 8 } ] },
            { "name": "Banks", "kind": "banks", "holdings": [ { "name": "Checking", "value": 300 }, { "name": "Card", "value": -100 } ] },
            { "name": "Duplex", "kind": "realEstate", "holdings": [ { "name": "Unit A", "value": 20000 } ] }
          ]
        }
        """;

    [Fact]
    public void ImportReadsAccountsAsTradableOrHeld()
    {
        var json = Balances;

        var portfolio = Import(json);

        Assert.Equal(new[] {("Taxable", true), ("Roth IRA", true), ("401(k)", false), ("Duplex", false)},
            portfolio.Sections.Select(x => (x.Account.Name, x.Account.Tradable)));
    }

    [Fact]
    public void ImportSumsBanksIntoBankCash()
    {
        var json = Balances;

        var portfolio = Import(json);

        Assert.Equal(200M, portfolio.BankCash);
    }

    [Fact]
    public void ImportReadsSettingsAndFillsMissingRatiosWithDefaults()
    {
        var json = Balances;

        var portfolio = Import(json);

        Assert.Equal((true, 500M, new AllocationRatios(0.6M, 0.75M, 0.06M, 0.05M)),
            (portfolio.IncludeCrypto, portfolio.CashTarget, portfolio.Ratios));
    }

    [Fact]
    public void ImportUsesDefaultsWithoutSettings()
    {
        var json = """{ "accounts": [] }""";

        var portfolio = Import(json);

        Assert.Equal((false, false, 0M, AllocationRatios.Default),
            (portfolio.IncludeCrypto, portfolio.IncludeBanks, portfolio.CashTarget, portfolio.Ratios));
    }

    [Fact]
    public void ImportMapsTickersToAssetClasses()
    {
        var json = Balances;

        var portfolio = Import(json);

        Assert.Equal(new[] {AssetClass.UsStocks, AssetClass.Cash, AssetClass.Bitcoin, AssetClass.Bonds, AssetClass.RealEstate},
            portfolio.Sections.SelectMany(x => x.Account.Positions).Select(x => x.AssetClass));
    }

    [Theory]
    [InlineData("""{ "accounts": [ { "name": "T", "kind": "taxable", "holdings": [ { "ticker": "XYZ", "shares": 1, "price": 1 } ] } ] }""")]
    [InlineData("""{ "accounts": [ { "name": "T", "kind": "taxable", "holdings": [ { "ticker": "VTI", "shares": 1 } ] } ] }""")]
    [InlineData("""{ "accounts": [ { "name": "T", "kind": "chequing", "holdings": [] } ] }""")]
    [InlineData("""{ "accounts": """)]
    [InlineData("""{ "accounts": [ { "name": "T", "kind": 9, "holdings": [] } ] }""")]
    [InlineData("""{ "accounts": [ { "kind": "taxable", "holdings": [] } ] }""")]
    [InlineData("""{ "accounts": [ { "name": "T", "kind": "taxable", "holdings": [ null ] } ] }""")]
    [InlineData("""{ "accounts": [ { "name": "T", "kind": "taxable", "holdings": [ { "ticker": "VTI", "shares": -1, "price": 1 } ] } ] }""")]
    [InlineData("""{ "accounts": [ { "name": "W", "kind": "wallet", "holdings": [ { "ticker": "VTI", "shares": 1, "price": 1 } ] } ] }""")]
    public void ImportRejectsInvalidBalances(string json)
    {
        void Act() => Import(json);

        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void ImportsTheShippedExample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "balances.example.json");

        var portfolio = JsonImporter.Import(path);

        Assert.Equal(5, portfolio.Sections.Count);
    }

    [Fact]
    public void BankCashGoesToATaxableAccountWhateverItsName()
    {
        var portfolio = Import("""
            { "accounts": [
              { "name": "Vanguard Brokerage", "kind": "taxable", "holdings": [ { "ticker": "VTI", "shares": 10, "price": 100 } ] },
              { "name": "Banks", "kind": "banks", "holdings": [ { "name": "Checking", "value": 300 } ] } ] }
            """);
        var options = CliOptions.Parse(new[] {"balances.json", "--cash", "include"});

        var result = PlanBuilder.Build(portfolio, options);

        Assert.Equal((300M, "Vanguard Brokerage"), (result.BankTransfer, result.TransferAccount));
    }

    [Fact]
    public void ImportPricesAWalletCoinAsTheCoinWhateverTheTicker()
    {
        var json = """{ "accounts": [ { "name": "W", "kind": "wallet", "holdings": [ { "ticker": "BTC", "shares": 1, "price": 80000 } ] } ] }""";

        var portfolio = Import(json);

        Assert.Equal("bitcoin", portfolio.Sections.Single().Account.Positions.Single().Ticker);
    }
}
