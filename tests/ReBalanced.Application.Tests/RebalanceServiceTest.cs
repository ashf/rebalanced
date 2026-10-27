using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ReBalanced.Application.Services;
using ReBalanced.Application.Services.Extensions;
using ReBalanced.Application.Tests.Utility;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;
using ReBalanced.Domain.Seeds;
using ReBalanced.Domain.ValueTypes;
using Xunit;
using Xunit.Abstractions;

namespace ReBalanced.Application.Tests;

public class RebalanceServiceTest
{
    private readonly Dictionary<string, Asset> _assetsInMemory = AssetsInMemory.AssetsSimple;
    private readonly ITestOutputHelper _testOutputHelper;

    public RebalanceServiceTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    private async Task<(IAssetRepository assetRepository, AssetService assetService, RebalanceService rebalanceService,
        HashSet<string> allAssets)> Setup()
    {
        var assetRepository = Substitute.For<IAssetRepository>();
        assetRepository.Get(Arg.Any<string>()).Returns(x => _assetsInMemory.GetValueOrDefault(x.Arg<string>()));
        assetRepository.GetValue(Arg.Any<string>())
            .Returns(x => _assetsInMemory.GetValueOrDefault(x.Arg<string>())?.Value);
        assetRepository.GetAllTickers().Returns(_assetsInMemory.Keys.ToHashSet());

        var assetService = new AssetService(assetRepository);
        var rebalanceService =
            new RebalanceService(assetService, assetRepository, NullLogger<RebalanceService>.Instance);

        var allAssets = await assetRepository.GetAllTickers();

        return (assetRepository, assetService, rebalanceService, allAssets);
    }

    [Fact]
    public async Task CanRebalanceSimple()
    {
        // Arrange
        var (assetRepository, assetService, rebalanceService, allAssets) = await Setup();

        var portfolio = new Portfolio("Test Portfolio");

        var invAccount = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false, allAssets);
        invAccount.AddHolding(new Holding(AssetSeeds.VTI, 100M));
        invAccount.AddHolding(new Holding(AssetSeeds.VXUS, 50M));
        invAccount.AddHolding(new Holding(AssetSeeds.CASH, 200M));
        portfolio.AddAccount(invAccount);

        var rothAccount = new Account("Roth", AccountType.Roth, HoldingType.Quantity, false, allAssets);
        rothAccount.AddHolding(new Holding(AssetSeeds.VTI, 10M));
        rothAccount.AddHolding(new Holding(AssetSeeds.VNQ, 15M));
        rothAccount.AddHolding(new Holding(AssetSeeds.BND, 20M));
        rothAccount.AddHolding(new Holding(AssetSeeds.CASH, 100M));
        portfolio.AddAccount(rothAccount);

        var portfolioTotal = await portfolio.TotalValue(assetService);
        _testOutputHelper.WriteLine($"PortfolioTotal : {portfolioTotal}\n");

        var allocations = new Dictionary<string, decimal>
        {
            {"VTI", .3725M},
            {"VXUS", .3825M},
            {"VNQ", .045M},
            {"BND", .1M},
            {"CASH", .1M}
        };

        portfolio.SetAllocation(allocations.Select(x => new Allocation(x.Key, x.Value)).ToList());

        // Act
        var rebalanceResults = await rebalanceService.Rebalance(portfolio);

        // Assert
        _testOutputHelper.PrintResults(rebalanceResults, portfolio);
        await ResultsChecker.WithinTolerance(_testOutputHelper, rebalanceResults, assetRepository, portfolioTotal,
            portfolio.Allocations, .05M);
    }

    [Fact]
    public async Task CanRebalanceFractional()
    {
        // Arrange
        var (assetRepository, assetService, rebalanceService, allAssets) = await Setup();

        var portfolio = new Portfolio("Test Portfolio");

        var invAccount = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false, allAssets);
        invAccount.AddHolding(new Holding(AssetSeeds.VTI, 100.5M));
        invAccount.AddHolding(new Holding(AssetSeeds.VXUS, 50M));
        invAccount.AddHolding(new Holding(AssetSeeds.CASH, 200M));
        portfolio.AddAccount(invAccount);

        var rothAccount = new Account("Roth", AccountType.Roth, HoldingType.Quantity, false, allAssets);
        rothAccount.AddHolding(new Holding(AssetSeeds.VTI, 10M));
        rothAccount.AddHolding(new Holding(AssetSeeds.VNQ, 15M));
        rothAccount.AddHolding(new Holding(AssetSeeds.BND, 20M));
        rothAccount.AddHolding(new Holding(AssetSeeds.CASH, 100M));
        portfolio.AddAccount(rothAccount);

        var portfolioTotal = await portfolio.TotalValue(assetService);
        _testOutputHelper.WriteLine($"PortfolioTotal : {portfolioTotal}\n");

        var allocations = new Dictionary<string, decimal>
        {
            {"VTI", .3725M},
            {"VXUS", .3825M},
            {"VNQ", .045M},
            {"BND", .1M},
            {"CASH", .1M}
        };

        portfolio.SetAllocation(allocations.Select(x => new Allocation(x.Key, x.Value)).ToList());

        // Act
        var rebalanceResults = await rebalanceService.Rebalance(portfolio);

        // Assert
        _testOutputHelper.PrintResults(rebalanceResults, portfolio);
        await ResultsChecker.WithinTolerance(_testOutputHelper, rebalanceResults, assetRepository, portfolioTotal,
            portfolio.Allocations, .04M);
    }

    [Fact]
    public async Task CanRebalanceEquivalent()
    {
        // Arrange
        var (assetRepository, assetService, rebalanceService, _) = await Setup();

        var portfolio = new Portfolio("Test Portfolio");

        var invAccount = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false,
            new HashSet<string> {"CASH", "GBTC"});

        invAccount.AddHolding(new Holding(AssetSeeds.GBTC));
        invAccount.AddHolding(new Holding(AssetSeeds.CASH, 40000M));
        portfolio.AddAccount(invAccount);

        var portfolioTotal = await portfolio.TotalValue(assetService);
        _testOutputHelper.WriteLine($"PortfolioTotal : {portfolioTotal}\n");

        var allocations = new Dictionary<string, decimal>
        {
            {"bitcoin", 1M}
        };

        portfolio.SetAllocation(allocations.Select(x => new Allocation(x.Key, x.Value)).ToList());
        var gbtcPrice = _assetsInMemory["GBTC"].Value;

        // Act
        var rebalanceResults = await rebalanceService.Rebalance(portfolio);

        // Assert
        _testOutputHelper.PrintResults(rebalanceResults, portfolio);
        Assert.Equal((double) portfolioTotal, (double) (rebalanceResults["GBTC_INV"] * gbtcPrice), 2);
        await ResultsChecker.WithinTolerance(_testOutputHelper, rebalanceResults, assetRepository, portfolioTotal,
            portfolio.Allocations, .01M);
    }

    [Fact]
    public async Task CanRebalanceAccountNameWithUnderscore()
    {
        // Arrange
        var (assetRepository, assetService, rebalanceService, allAssets) = await Setup();

        var portfolio = new Portfolio("Test Portfolio");

        var invAccount = new Account("MY_INV", AccountType.Taxable, HoldingType.Quantity, false, allAssets);
        invAccount.AddHolding(new Holding(AssetSeeds.VTI, 100.5M));
        invAccount.AddHolding(new Holding(AssetSeeds.CASH, 2000M));
        portfolio.AddAccount(invAccount);

        var portfolioTotal = await portfolio.TotalValue(assetService);

        portfolio.SetAllocation(new List<Allocation> {new("VTI", .9M), new("CASH", .1M)});

        // Act
        var rebalanceResults = await rebalanceService.Rebalance(portfolio);

        // Assert
        _testOutputHelper.PrintResults(rebalanceResults, portfolio);
        await ResultsChecker.WithinTolerance(_testOutputHelper, rebalanceResults, assetRepository, portfolioTotal,
            portfolio.Allocations, .05M);
    }

    [Fact]
    public async Task RebalanceThrowsWhenNoOptimalSolution()
    {
        // Arrange
        var (_, _, rebalanceService, allAssets) = await Setup();

        var portfolio = new Portfolio("Test Portfolio");

        // one whole VTI share is worth the whole account, so a 50% VTI target is unreachable
        var rothAccount = new Account("Roth", AccountType.Roth, HoldingType.Quantity, false, allAssets);
        rothAccount.AddHolding(new Holding(AssetSeeds.VTI, 1M));
        portfolio.AddAccount(rothAccount);

        portfolio.SetAllocation(new List<Allocation> {new("VTI", .5M), new("VNQ", .5M)});

        // Act
        var exception = await Record.ExceptionAsync(() => rebalanceService.Rebalance(portfolio));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task RebalanceThrowsWhenNoAccountCanHoldAllocation()
    {
        // Arrange
        var (_, _, rebalanceService, allAssets) = await Setup();

        var portfolio = new Portfolio("Test Portfolio");

        var invAccount = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false, allAssets);
        invAccount.AddHolding(new Holding(AssetSeeds.CASH, 1000M));
        portfolio.AddAccount(invAccount);

        portfolio.SetAllocation(new List<Allocation> {new("PROPERTY", .5M), new("CASH", .5M)});

        // Act
        var exception = await Record.ExceptionAsync(() => rebalanceService.Rebalance(portfolio));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("PROPERTY", exception.Message);
    }

    [Fact]
    public async Task RebalanceCountsSharedEquivalentOnce()
    {
        // Arrange
        var (_, _, rebalanceService, _) = await Setup();

        var portfolio = new Portfolio("Test Portfolio");

        var invAccount = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false,
            new HashSet<string> {"CASH", "GBTC"});
        invAccount.AddHolding(new Holding(AssetSeeds.CASH, 40000M));
        portfolio.AddAccount(invAccount);

        // bitcoin and GBTC both resolve to the GBTC position in this account
        portfolio.SetAllocation(new List<Allocation> {new("bitcoin", .5M), new("GBTC", .5M)});
        var gbtcPrice = _assetsInMemory["GBTC"].Value;

        // Act
        var rebalanceResults = await rebalanceService.Rebalance(portfolio);

        // Assert
        Assert.Equal(40000.0, (double) (rebalanceResults["GBTC_INV"] * gbtcPrice + rebalanceResults["CASH_INV"]), 2);
    }
}
