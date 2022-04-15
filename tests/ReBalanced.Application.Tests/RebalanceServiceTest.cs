using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    private readonly Dictionary<string?, Asset> _assetsInMemory = AssetsInMemory.AssetsSimple;
    private readonly ITestOutputHelper _testOutputHelper;

    public RebalanceServiceTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task CanRebalanceSimple()
    {
        // Arrange
        var assetRepository = Substitute.For<IAssetRepository>();
        assetRepository.Get(Arg.Any<string>()).Returns(x => _assetsInMemory[x.Arg<string>()]);
        assetRepository.GetValue(Arg.Any<string>()).Returns(x => _assetsInMemory[x.Arg<string>()].Value);
        assetRepository.GetAllTickers().Returns(_assetsInMemory.Keys.ToHashSet());

        var logger = Substitute.For<ILogger>();

        var assetService = new AssetService(assetRepository);
        var rebalanceService = new RebalanceService(assetService, assetRepository, logger);

        var allAssetTickersNoCryptoProperty = await assetRepository.GetAllTickers();
        allAssetTickersNoCryptoProperty.Remove("bitcoin");
        allAssetTickersNoCryptoProperty.Remove("ethereum");
        allAssetTickersNoCryptoProperty.Remove("ETHE");
        allAssetTickersNoCryptoProperty.Remove("GBTC");
        allAssetTickersNoCryptoProperty.Remove("PROPERTY");

        var portfolio = new Portfolio("Asher's Portfolio");

        var invAccount = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false,
            allAssetTickersNoCryptoProperty);
        invAccount.AddHolding(new Holding(AssetSeeds.VTI, 100M));
        invAccount.AddHolding(new Holding(AssetSeeds.VXUS, 50M));
        invAccount.AddHolding(new Holding(AssetSeeds.CASH, 200M));
        portfolio.AddAccount(invAccount);

        var rothAccount = new Account("Roth", AccountType.Roth, HoldingType.Quantity, false,
            allAssetTickersNoCryptoProperty);
        rothAccount.AddHolding(new Holding(AssetSeeds.VTI, 10M));
        rothAccount.AddHolding(new Holding(AssetSeeds.VNQ, 15M));
        rothAccount.AddHolding(new Holding(AssetSeeds.BND, 20M));
        rothAccount.AddHolding(new Holding(AssetSeeds.CASH, 100M));
        portfolio.AddAccount(rothAccount);

        var portfolioTotal = await portfolio.TotalValue(assetService);
        _testOutputHelper.WriteLine($"PortfolioTotal : {portfolioTotal}");

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
    }

    [Fact]
    public async Task CanRebalanceFractional()
    {
        // Arrange
        var assetRepository = Substitute.For<IAssetRepository>();
        assetRepository.Get(Arg.Any<string>()).Returns(x => _assetsInMemory[x.Arg<string>()]);
        assetRepository.GetValue(Arg.Any<string>()).Returns(x => _assetsInMemory[x.Arg<string>()].Value);
        assetRepository.GetAllTickers().Returns(_assetsInMemory.Keys.ToHashSet());

        var logger = Substitute.For<ILogger>();

        var assetService = new AssetService(assetRepository);
        var rebalanceService = new RebalanceService(assetService, assetRepository, logger);

        var allAssetTickersNoCryptoProperty = await assetRepository.GetAllTickers();
        allAssetTickersNoCryptoProperty.Remove("bitcoin");
        allAssetTickersNoCryptoProperty.Remove("ethereum");
        allAssetTickersNoCryptoProperty.Remove("PROPERTY");

        var portfolio = new Portfolio("Asher's Portfolio");

        var invAccount = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false,
            allAssetTickersNoCryptoProperty);
        invAccount.AddHolding(new Holding(AssetSeeds.VTI, 100.5M));
        invAccount.AddHolding(new Holding(AssetSeeds.VXUS, 50M));
        invAccount.AddHolding(new Holding(AssetSeeds.CASH, 200M));
        portfolio.AddAccount(invAccount);

        var rothAccount = new Account("Roth", AccountType.Roth, HoldingType.Quantity, false,
            allAssetTickersNoCryptoProperty);
        rothAccount.AddHolding(new Holding(AssetSeeds.VTI, 10M));
        rothAccount.AddHolding(new Holding(AssetSeeds.VNQ, 15M));
        rothAccount.AddHolding(new Holding(AssetSeeds.BND, 20M));
        rothAccount.AddHolding(new Holding(AssetSeeds.CASH, 100M));
        portfolio.AddAccount(rothAccount);

        var portfolioTotal = await portfolio.TotalValue(assetService);
        _testOutputHelper.WriteLine($"PortfolioTotal : {portfolioTotal}");

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
    }

    [Fact]
    public async Task CanRebalanceEquivalent()
    {
        // Arrange
        var assetRepository = Substitute.For<IAssetRepository>();
        assetRepository.Get(Arg.Any<string>()).Returns(x => _assetsInMemory[x.Arg<string>()]);
        assetRepository.GetValue(Arg.Any<string>()).Returns(x => _assetsInMemory[x.Arg<string>()].Value);
        assetRepository.GetAllTickers().Returns(_assetsInMemory.Keys.ToHashSet());

        var logger = Substitute.For<ILogger>();

        var assetService = new AssetService(assetRepository);
        var rebalanceService = new RebalanceService(assetService, assetRepository, logger);

        var allAssetTickersNoCryptoProperty = await assetRepository.GetAllTickers();
        allAssetTickersNoCryptoProperty.Remove("bitcoin");
        allAssetTickersNoCryptoProperty.Remove("ethereum");
        allAssetTickersNoCryptoProperty.Remove("PROPERTY");

        var portfolio = new Portfolio("Asher's Portfolio");

        var invAccount = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false,
            new HashSet<string?> {"CASH", "GBTC"});

        invAccount.AddHolding(new Holding(AssetSeeds.GBTC));
        invAccount.AddHolding(new Holding(AssetSeeds.CASH, 40000M));
        portfolio.AddAccount(invAccount);

        var portfolioTotal = await portfolio.TotalValue(assetService);
        _testOutputHelper.WriteLine($"PortfolioTotal : {portfolioTotal}");

        var allocations = new Dictionary<string, decimal>
        {
            {"bitcoin", 1M}
        };

        portfolio.SetAllocation(allocations.Select(x => new Allocation(x.Key, x.Value)).ToList());

        // Act
        var rebalanceResults = await rebalanceService.Rebalance(portfolio);

        // Assert
        _testOutputHelper.PrintResults(rebalanceResults, portfolio);
        Assert.True(rebalanceResults["GBTC_INV"] == 1000);
    }
}