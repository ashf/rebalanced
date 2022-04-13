using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ReBalanced.Domain.ValueTypes;
using ReBalanced.Infrastructure.Caching;
using ReBalanced.Infrastructure.LiteDB;
using Xunit;
using Xunit.Abstractions;

namespace ReBalanced.Infrastructure.Tests;

public class LiteDbAssetCacheTests
{
    [Fact]
    public async Task CacheHit()
    {
        // Arrange
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        var dbPath = Path.Join(path, "rebalanced_test.db");
        var liteDbOptions = Options.Create(new LiteDbConfig{DatabasePath = dbPath});
        var db = new LiteDbContext(liteDbOptions);
        var cache = new LiteDbAssetCache(db);

        // Act
        var asset = new Asset("BND", 77.25M, AssetType.Stock, DateTime.UtcNow);
        await cache.Upsert(asset.Ticker, asset);
        var returnedAsset = await cache.Get(asset.Ticker);

        // Assert
        Assert.Equal(asset, returnedAsset);
    }
}