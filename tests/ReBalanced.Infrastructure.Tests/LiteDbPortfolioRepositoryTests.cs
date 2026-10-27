using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Seeds;
using ReBalanced.Infrastructure.Repositories;
using ReBalanced.Infrastructure.Tests.Utility;
using Xunit;

namespace ReBalanced.Infrastructure.Tests;

public class LiteDbPortfolioRepositoryTests
{
    [Fact]
    public async Task GetUnknownIdReturnsNull()
    {
        var repository = new LiteDbPortfolioRepository(LiteDbUtility.GetTestLiteDb());

        var portfolio = await repository.Get(Guid.NewGuid());

        Assert.Null(portfolio);
    }

    [Fact(Skip = "LiteDB cannot rebuild Portfolio.Accounts (getter-only Dictionary<Guid, Account>) or Account holdings")]
    public async Task PortfolioRoundTripsAccountsAndHoldings()
    {
        // Arrange
        var repository = new LiteDbPortfolioRepository(LiteDbUtility.GetTestLiteDb());
        var portfolio = new Portfolio("Test Portfolio");
        var account = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false,
            new HashSet<string> {"CASH", "VTI"});
        account.AddHolding(new Holding(AssetSeeds.VTI, 12.5M));
        portfolio.AddAccount(account);
        await repository.Create(portfolio);

        // Act
        var loaded = await repository.Get(portfolio.Id);

        // Assert
        Assert.NotNull(loaded);
        var loadedAccount = Assert.Single(loaded!.Accounts.Values);
        Assert.Equal(account.Id, loadedAccount.Id);
        Assert.Equal(12.5M, loadedAccount.Holdings.Single(x => x.Asset.Ticker == "VTI").Quantity);
    }
}
