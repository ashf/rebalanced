using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Domain.Providers;

public interface IAssetRepository
{
    Asset Get(string assetTicker);
    decimal GetValue(string assetTicker);
    HashSet<string> GetAllTickers();
    void UpdateValues();
}