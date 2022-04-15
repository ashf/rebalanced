using System;
using System.Threading.Tasks;
using ReBalanced.Domain.ValueTypes;
using ReBalanced.Infrastructure.Caching;
using ReBalanced.Infrastructure.Tests.Utility;
using Xunit;

namespace ReBalanced.Infrastructure.Tests;

public class LiteDbAssetCacheTests
{
    [Fact]
    public async Task CacheHit()
    {
        // Arrange
        var db = LiteDbUtility.GetTestLiteDb();
        var cache = new LiteDbAssetCache(db);

        // Act
        var asset = new Asset("BND", 77.25M, AssetType.Stock, DateTime.UtcNow);
        await cache.Upsert(asset.Ticker, asset);
        var returnedAsset = await cache.Get(asset.Ticker);

        // Assert
        Assert.Equal(asset, returnedAsset);
    }
}