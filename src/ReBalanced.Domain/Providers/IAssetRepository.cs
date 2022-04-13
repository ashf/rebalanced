using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Domain.Providers;

public interface IAssetRepository
{
    Task<Asset?> Get(string assetTicker);
    Task<decimal?> GetValue(string assetTicker);
    Task<HashSet<string>> GetAllTickers();
    Task UpdateValues();
}