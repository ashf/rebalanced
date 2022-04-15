using LiteDB.Async;
using ReBalanced.Domain.ValueTypes;
using ReBalanced.Infrastructure.LiteDB;

namespace ReBalanced.Infrastructure.Caching;

public class LiteDbAssetCache : IAssetCache
{
    private readonly ILiteCollectionAsync<Asset> _assets;

    public LiteDbAssetCache(LiteDbContext db)
    {
        _assets = db.Context.GetCollection<Asset>("assets");
    }

    public async Task<Asset?> Get(string? ticker)
    {
        return await _assets.FindByIdAsync(ticker);
    }

    public async Task Upsert(string? ticker, Asset asset)
    {
        await _assets.UpsertAsync(ticker, asset);
    }

    public async Task Delete(string ticker)
    {
        await _assets.DeleteAsync(ticker);
    }

    public async Task<IEnumerable<Asset>> Assets()
    {
        return await _assets.FindAllAsync();
    }

    public async Task<bool> ContainsAsset(string ticker)
    {
        return await _assets.ExistsAsync(x => x.Ticker == ticker);
    }

    public async Task<DateTimeOffset?> OldestCacheItem()
    {
        return (await _assets.FindAsync(asset => asset.AssetType == AssetType.Stock || asset.AssetType == AssetType.Crypto))
            .Min(x => x.Updated);
    }
}