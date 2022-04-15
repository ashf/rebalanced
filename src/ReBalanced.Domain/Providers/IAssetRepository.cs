using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Domain.Providers;

public interface IAssetRepository
{
    Task SeedCache(Asset asset);
    Task SeedCache(IEnumerable<Asset> assets);
    Task<Asset?> Get(string? assetTicker);
    Task<decimal?> GetValue(string? assetTicker);
    Task<HashSet<string>> GetAllTickers();
    Task UpdateValues();
}