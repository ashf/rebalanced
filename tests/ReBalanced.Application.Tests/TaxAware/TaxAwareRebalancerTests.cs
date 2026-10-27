using System.Collections.Generic;
using System.Linq;
using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using Xunit;

namespace ReBalanced.Application.Tests.TaxAware;

public class TaxAwareRebalancerTests
{
    private static Position Holding(string ticker, decimal value)
    {
        return new Position(ticker, AssetClasses.Of(ticker)!.Value, value, 1M);
    }

    private static HoldingAccount Taxable(params Position[] positions)
    {
        return new HoldingAccount("Taxable", AccountType.Taxable, true, positions);
    }

    private static HoldingAccount Roth(params Position[] positions)
    {
        return new HoldingAccount("Roth IRA", AccountType.Roth, true, positions);
    }

    private static List<(string, string, decimal)> Trades(RebalancePlan plan)
    {
        return plan.Trades.Select(x => (x.Account, x.Ticker, x.Amount)).OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();
    }

    [Fact]
    public void InvestsTaxableCashWithoutSelling()
    {
        var accounts = new[] {Taxable(Holding("VTI", 900M), Holding("CASH", 100M))};
        var targets = new Dictionary<AssetClass, decimal> {[AssetClass.UsStocks] = 1000M, [AssetClass.Cash] = 0M};

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0.01M);

        Assert.Equal(new List<(string, string, decimal)> {("Taxable", "VTI", 100M)}, Trades(plan));
    }

    [Fact]
    public void FixesDriftInsideTheBandWithRothTradesOnly()
    {
        var accounts = new[]
        {
            Taxable(Holding("VTI", 1000M)),
            Roth(Holding("VTI", 500M), Holding("BND", 500M))
        };
        var targets = new Dictionary<AssetClass, decimal> {[AssetClass.UsStocks] = 1550M, [AssetClass.Bonds] = 450M};

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0.05M);

        Assert.Equal(new List<(string, string, decimal)> {("Roth IRA", "BND", -50M), ("Roth IRA", "VTI", 50M)},
            Trades(plan));
    }

    [Fact]
    public void SellsInTaxableOnlyDownToTheBandEdge()
    {
        var accounts = new[] {Taxable(Holding("VTI", 800M), Holding("VEA", 200M))};
        var targets = new Dictionary<AssetClass, decimal>
        {
            [AssetClass.UsStocks] = 500M, [AssetClass.DevelopedStocks] = 500M
        };

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0.01M);

        Assert.Equal(new List<(string, string, decimal)> {("Taxable", "VEA", 290M), ("Taxable", "VTI", -290M)},
            Trades(plan));
    }

    [Fact]
    public void NeverTradesFixedAccounts()
    {
        var accounts = new[]
        {
            new HoldingAccount("401(k)", AccountType.Roth, false, new[] {Holding("TMBRX", 600M)}),
            Taxable(Holding("VTI", 400M))
        };
        var targets = new Dictionary<AssetClass, decimal> {[AssetClass.UsStocks] = 500M, [AssetClass.Bonds] = 500M};

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0.01M);

        Assert.DoesNotContain(plan.Trades, x => x.Account == "401(k)");
    }

    [Fact]
    public void BuysThePreferredTickerWithNewMoney()
    {
        var accounts = new[] {Roth(Holding("GBTC", 100M), Holding("CASH", 100M))};
        var targets = new Dictionary<AssetClass, decimal> {[AssetClass.Bitcoin] = 200M, [AssetClass.Cash] = 0M};

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0.01M);

        Assert.Equal(new List<(string, string, decimal)> {("Roth IRA", "BTC", 100M)}, Trades(plan));
    }

    [Fact]
    public void PutsNewBondsInTheRoth()
    {
        var accounts = new[] {Taxable(Holding("CASH", 100M)), Roth(Holding("CASH", 100M))};
        var targets = new Dictionary<AssetClass, decimal>
        {
            [AssetClass.UsStocks] = 100M, [AssetClass.Bonds] = 100M, [AssetClass.Cash] = 0M
        };

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0.01M);

        Assert.Equal(new List<(string, string, decimal)> {("Roth IRA", "BND", 100M), ("Taxable", "VTI", 100M)},
            Trades(plan));
    }

    [Fact]
    public void PlacesInternationalInTaxableAndBondsInRoth()
    {
        var accounts = new[]
        {
            Taxable(Holding("VTI", 600M), Holding("VEA", 300M), Holding("CASH", 100M)),
            Roth(Holding("VTI", 200M), Holding("BND", 300M))
        };
        var targets = new Dictionary<AssetClass, decimal>
        {
            [AssetClass.UsStocks] = 800M, [AssetClass.DevelopedStocks] = 500M, [AssetClass.Bonds] = 200M,
            [AssetClass.Cash] = 0M
        };

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0M);

        Assert.Equal(new List<(string, string, decimal)>
        {
            ("Roth IRA", "BND", -100M), ("Roth IRA", "VTI", 100M), ("Taxable", "VEA", 200M), ("Taxable", "VTI", -100M)
        }, Trades(plan));
    }

    [Fact]
    public void SellsAHoldingOutsideItsTaxEfficientHome()
    {
        var accounts = new[]
        {
            Taxable(Holding("VTI", 900M), Holding("BND", 100M)),
            Roth(Holding("VTI", 1000M))
        };
        var targets = new Dictionary<AssetClass, decimal> {[AssetClass.UsStocks] = 1900M, [AssetClass.Bonds] = 100M};

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0M);

        Assert.Equal(new List<(string, string, decimal)>
        {
            ("Roth IRA", "BND", 100M), ("Roth IRA", "VTI", -100M), ("Taxable", "BND", -100M), ("Taxable", "VTI", 100M)
        }, Trades(plan));
    }

    [Fact]
    public void WarnsWhenATargetCannotBeReached()
    {
        var accounts = new[]
        {
            new HoldingAccount("401(k)", AccountType.Roth, false, new[] {Holding("TMBRX", 600M)}),
            Roth(Holding("VTI", 400M))
        };
        var targets = new Dictionary<AssetClass, decimal> {[AssetClass.UsStocks] = 500M, [AssetClass.Bonds] = 500M};

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0M);

        Assert.Equal(2, plan.Warnings.Count);
    }

    [Fact]
    public void WarnsWhenAClassHasNoTaxEfficientHome()
    {
        var accounts = new[] {Taxable(Holding("VTI", 900M), Holding("CASH", 100M))};
        var targets = new Dictionary<AssetClass, decimal>
        {
            [AssetClass.UsStocks] = 900M, [AssetClass.Bonds] = 100M, [AssetClass.Cash] = 0M
        };

        var plan = new TaxAwareRebalancer().Rebalance(accounts, targets, 0M);

        Assert.Equal(new List<(string, string, decimal)> {("Taxable", "BND", 100M)}, Trades(plan));
        Assert.Single(plan.Warnings);
    }
}
