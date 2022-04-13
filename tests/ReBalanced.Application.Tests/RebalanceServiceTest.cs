using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ReBalanced.Application.Services;
using ReBalanced.Application.Tests.Utility;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.Entities.Aggregates;
using ReBalanced.Domain.Providers;
using ReBalanced.Domain.ValueTypes;
using Xunit;
using Xunit.Abstractions;

namespace ReBalanced.Application.Tests;

public class RebalanceServiceTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Dictionary<string, Asset> _assetsInMemory = AssetsInMemory.Assets;

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

        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger>();
        
        var assetService = new AssetService(assetRepository);
        var rebalanceService = new RebalanceService(assetService, assetRepository, logger);

        var allAssetTickersNoCryptoProperty = await assetRepository.GetAllTickers();
        allAssetTickersNoCryptoProperty.Remove("bitcoin");
        allAssetTickersNoCryptoProperty.Remove("ethereum");
        allAssetTickersNoCryptoProperty.Remove("Property");

        var portfolio = new Portfolio("Asher's Portfolio");

        var invAccount = new Account("INV", AccountType.Taxable, HoldingType.Quantity, allAssetTickersNoCryptoProperty);
        invAccount.AddHolding(new Holding("VTI", 100M));
        invAccount.AddHolding(new Holding("VXUS", 50M));
        invAccount.AddHolding(new Holding("CASH", 200M));
        portfolio.AddAccount(invAccount);

        var rothAccount = new Account("Roth", AccountType.Roth, HoldingType.Quantity, allAssetTickersNoCryptoProperty);
        rothAccount.AddHolding(new Holding("VTI", 10M));
        rothAccount.AddHolding(new Holding("VNQ", 15M));
        rothAccount.AddHolding(new Holding("BND", 20M));
        rothAccount.AddHolding(new Holding("CASH", 100M));
        portfolio.AddAccount(rothAccount);

        var portfolioTotal = portfolio.Accounts.Values.Sum(account => assetService.TotalValue(account.Holdings));
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
        _testOutputHelper.PrintResults(rebalanceResults!, portfolio);
    }
}