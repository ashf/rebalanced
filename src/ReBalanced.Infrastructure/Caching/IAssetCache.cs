using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Infrastructure.Caching;

public interface IAssetCache
{
    Task<Asset?> Get(string? ticker);
    Task Upsert(string? ticker, Asset asset);
    Task Delete(string ticker);
    Task<IEnumerable<Asset>> Assets();
    Task<bool> ContainsAsset(string ticker);
    Task<DateTimeOffset?> OldestCacheItem();
}