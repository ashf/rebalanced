using LiteDB;
using ReBalanced.Domain.ValueTypes;
using ReBalanced.Infrastructure.LiteDB;

namespace ReBalanced.Infrastructure.Caching;

public class LiteDbAssetCache : IAssetCache
{
    private readonly ILiteCollection<Asset> _assets;

    public LiteDbAssetCache(LiteDbContext db)
    {
        _assets = db.Context.GetCollection<Asset>("assets");
    }

    public async Task<Asset?> Get(string ticker)
    {
        return await Task.Run(() => _assets.FindById(ticker));
    }

    public async Task Upsert(string ticker, Asset asset)
    {
        await Task.Run(() => _assets.Upsert(ticker, asset));
    }

    public async Task Delete(string ticker)
    {
        await Task.Run(() => _assets.Delete(ticker));
    }

    public async Task<IEnumerable<Asset>> Assets()
    {
        return await Task.Run(() => _assets.FindAll());
    }

    public async Task<bool> ContainsAsset(string ticker)
    {
        return await Task.Run(() => _assets.Exists(x => x.Ticker == ticker));
    }

    public async Task<DateTimeOffset> OldestCacheItem()
    {
        var oldestAsset = await Task.Run(() => _assets.Query().OrderBy(x => x.Updated).First());
        return oldestAsset.Updated;
    }
}