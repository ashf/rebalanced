using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ReBalanced.Domain.ValueTypes;
using ReBalanced.Infrastructure.Caching;
using ReBalanced.Infrastructure.Repositories;
using ReBalanced.Infrastructure.Tests.Utility;
using ReBalanced.Infrastructure.TwelveData;
using Xunit;

namespace ReBalanced.Infrastructure.Tests;

public class TwelveDataAssetRepositoryTests
{
    [Fact]
    public async Task UpdateValuesOnEmptyCacheDoesNotThrow()
    {
        var priceApi = Substitute.For<IPriceApi>();
        priceApi.GetPrices(Arg.Any<IReadOnlyCollection<string>>()).Returns(new Dictionary<string, decimal>());
        var repository = new TwelveDataAssetRepository(priceApi, new LiteDbAssetCache(LiteDbUtility.GetTestLiteDb()),
            NullLogger<TwelveDataAssetRepository>.Instance);

        var exception = await Record.ExceptionAsync(() => repository.UpdateValues());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UpdateValuesSkipsMissingPriceAndUpdatesTheRest()
    {
        var cache = new LiteDbAssetCache(LiteDbUtility.GetTestLiteDb());
        var stale = DateTimeOffset.UtcNow.AddDays(-1);
        await cache.Upsert("bitcoin", new Asset("bitcoin", 1M, AssetType.Crypto, stale));
        await cache.Upsert("ethereum", new Asset("ethereum", 1M, AssetType.Crypto, stale));
        var priceApi = Substitute.For<IPriceApi>();
        priceApi.GetPrices(Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new Dictionary<string, decimal> {["ETH/USD"] = 3000M});
        var repository = new TwelveDataAssetRepository(priceApi, cache, NullLogger<TwelveDataAssetRepository>.Instance);

        await repository.UpdateValues();

        Assert.Equal(3000M, (await cache.Get("ethereum"))!.Value);
    }

    [Fact]
    public async Task UpdateValuesMarksUnpricedAssetChecked()
    {
        var cache = new LiteDbAssetCache(LiteDbUtility.GetTestLiteDb());
        var stale = DateTimeOffset.UtcNow.AddDays(-1);
        await cache.Upsert("VTI", new Asset("VTI", 378M, AssetType.Stock, stale));
        await cache.Upsert("dogecoin", new Asset("dogecoin", 1M, AssetType.Crypto, stale));
        var priceApi = Substitute.For<IPriceApi>();
        priceApi.GetPrices(Arg.Any<IReadOnlyCollection<string>>()).Returns(new Dictionary<string, decimal>());
        var repository = new TwelveDataAssetRepository(priceApi, cache, NullLogger<TwelveDataAssetRepository>.Instance);
        await repository.UpdateValues();

        await repository.UpdateValues();

        await priceApi.Received(1).GetPrices(Arg.Any<IReadOnlyCollection<string>>());
    }
}
