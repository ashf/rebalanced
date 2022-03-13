using ReBalanced.Domain.Providers;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Infastructure.Repositories;

public class AssetRepository : IAssetRepository
{
    private readonly Dictionary<string, Asset> _assetValues = new();

    public Asset Get(string assetTicker)
    {
        return _assetValues[assetTicker];
    }

    public decimal GetValue(string assetTicker)
    {
        return _assetValues[assetTicker].Value;
    }

    public HashSet<string> GetAllTickers()
    {
        return _assetValues.Keys.ToHashSet();
    }

    public void UpdateValues()
    {
        throw new NotImplementedException();
    }
}