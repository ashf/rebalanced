using System.Collections.Generic;
using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using Xunit;

namespace ReBalanced.Application.Tests.TaxAware;

public class AllocationTargetsTests
{
    // values and Target column from the "investment rebalance" sheet with INCLUDE CRYPTO = 0
    private static readonly Dictionary<AssetClass, decimal> SheetValues = new()
    {
        [AssetClass.UsStocks] = 682484.9699M,
        [AssetClass.DevelopedStocks] = 403691.5104M,
        [AssetClass.EmergingStocks] = 131799.4325M,
        [AssetClass.Bonds] = 81646.053M,
        [AssetClass.Cash] = 2634.99M,
        [AssetClass.RealEstate] = 260000M
    };

    private static readonly AllocationSettings SheetSettings =
        new(0.1M, 1.79947590120815E12M, 3.7015231488983E11M, 0M, false);

    [Theory]
    [InlineData(AssetClass.UsStocks, 673266.8461)]
    [InlineData(AssetClass.DevelopedStocks, 413141.0192)]
    [InlineData(AssetClass.EmergingStocks, 137713.6731)]
    [InlineData(AssetClass.Bonds, 78135.41734)]
    [InlineData(AssetClass.Cash, 0)]
    [InlineData(AssetClass.RealEstate, 260000)]
    public void MatchesSpreadsheetTargets(AssetClass assetClass, double sheetTarget)
    {
        var targets = AllocationTargets.Calculate(SheetValues, SheetSettings);

        Assert.Equal(sheetTarget, (double) targets[assetClass], 2);
    }

    [Fact]
    public void SplitsCryptoByMarketCap()
    {
        var values = new Dictionary<AssetClass, decimal> {[AssetClass.UsStocks] = 1000M};
        var settings = new AllocationSettings(0.1M, 3M, 1M, 0M, true);

        var targets = AllocationTargets.Calculate(values, settings);

        Assert.Equal((75M, 25M), (targets[AssetClass.Bitcoin], targets[AssetClass.Ethereum]));
    }

    [Fact]
    public void LeavesCryptoUntargetedWhenIgnored()
    {
        var values = new Dictionary<AssetClass, decimal> {[AssetClass.UsStocks] = 1000M};
        var settings = new AllocationSettings(0.1M, 3M, 1M, 0M, false);

        var targets = AllocationTargets.Calculate(values, settings);

        Assert.DoesNotContain(AssetClass.Bitcoin, targets.Keys);
    }

    [Fact]
    public void TopsUpReitWhenRealEstateIsBelowFivePercent()
    {
        var values = new Dictionary<AssetClass, decimal>
        {
            [AssetClass.UsStocks] = 980M,
            [AssetClass.RealEstate] = 20M
        };
        var settings = new AllocationSettings(0.1M, 3M, 1M, 0M, false);

        var targets = AllocationTargets.Calculate(values, settings);

        Assert.Equal(30M, targets[AssetClass.Reit]);
    }

    [Fact]
    public void ThrowsWhenCryptoIsIncludedWithoutMarketCaps()
    {
        var values = new Dictionary<AssetClass, decimal> {[AssetClass.UsStocks] = 1000M};
        var settings = new AllocationSettings(0.1M, 0M, 0M, 0M, true);

        var exception = Record.Exception(() => AllocationTargets.Calculate(values, settings));

        Assert.IsType<System.InvalidOperationException>(exception);
    }
}
