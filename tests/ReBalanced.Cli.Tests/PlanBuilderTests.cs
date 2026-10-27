using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Cli.Tests;

public class PlanBuilderTests
{
    private static readonly CliOptions Defaults = new("plan.xlsx", null, null, null, 0.01M, false);

    private static ImportedPortfolio Portfolio(decimal bankCash, bool includeCrypto = true, bool includeBanks = true)
    {
        var taxable = new HoldingAccount("Taxable", AccountType.Taxable, true, new[]
        {
            new Position("VTI", AssetClass.UsStocks, 10, 100),
            new Position("CASH", AssetClass.Cash, 50, 1)
        });
        var roth = new HoldingAccount("Roth IRA", AccountType.Roth, true, new[]
        {
            new Position("BND", AssetClass.Bonds, 10, 50)
        });
        var wallet = new HoldingAccount("Bitcoin", AccountType.CryptoWallet, false, new[]
        {
            new Position("bitcoin", AssetClass.Bitcoin, 0.1M, 50000)
        });
        var sections = new[]
        {
            new ImportedSection(SectionKind.Taxable, taxable),
            new ImportedSection(SectionKind.RothIra, roth),
            new ImportedSection(SectionKind.Wallet, wallet)
        };
        return new ImportedPortfolio(sections, bankCash, 0.1M, 3, 1, 50, includeCrypto, includeBanks);
    }

    [Fact]
    public void IgnoredCryptoHasNoCryptoTrades()
    {
        var portfolio = Portfolio(0);
        var options = Defaults with {IncludeCrypto = false};

        var result = PlanBuilder.Build(portfolio, options);

        Assert.DoesNotContain(result.Plan.Trades, x => x.Ticker is "BTC" or "ETH" or "bitcoin" or "ethereum");
    }

    [Fact]
    public void IgnoredCryptoHasNoCryptoClasses()
    {
        var portfolio = Portfolio(0);
        var options = Defaults with {IncludeCrypto = false};

        var result = PlanBuilder.Build(portfolio, options);

        Assert.DoesNotContain(result.Plan.Targets.Keys.Concat(result.Plan.Before.Keys).Concat(result.Plan.After.Keys),
            x => x is AssetClass.Bitcoin or AssetClass.Ethereum);
    }

    [Fact]
    public void IncludedCashTransfersBankCash()
    {
        var portfolio = Portfolio(200);
        var options = Defaults with {IncludeCash = true};

        var result = PlanBuilder.Build(portfolio, options);

        Assert.Equal(200, result.BankTransfer);
    }

    [Fact]
    public void IncludedCashAddsCashPositionToTaxable()
    {
        var portfolio = Portfolio(200);
        var options = Defaults with {IncludeCash = true};

        var result = PlanBuilder.Build(portfolio, options);

        var taxable = result.Accounts.Single(x => x.Name == "Taxable");
        Assert.Contains(new Position("CASH", AssetClass.Cash, 200, 1), taxable.Positions);
    }

    [Fact]
    public void IgnoredCashTransfersNothing()
    {
        var portfolio = Portfolio(200);
        var options = Defaults with {IncludeCash = false};

        var result = PlanBuilder.Build(portfolio, options);

        Assert.Equal(0, result.BankTransfer);
    }

    [Fact]
    public void NegativeBankCashTransfersNothing()
    {
        var portfolio = Portfolio(-100);
        var options = Defaults with {IncludeCash = true};

        var result = PlanBuilder.Build(portfolio, options);

        Assert.Equal(0, result.BankTransfer);
    }
}
