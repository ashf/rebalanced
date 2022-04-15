using System.Reflection;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Domain.Seeds;

public class AssetSeeds
{
    public static readonly Asset CASH = new Asset("CASH", 1M, AssetType.Cash);
    public static readonly Asset PROPERTY = new Asset("PROPERTY", 1M, AssetType.Property);
    
    public static readonly Asset BND = new Asset("BND", 1M, AssetType.Stock);
    public static readonly Asset ETHE = new Asset("ETHE", 1M, AssetType.Stock, default, "ethereum");
    public static readonly Asset GBTC = new Asset("GBTC", 1M, AssetType.Stock, default, "bitcoin");
    public static readonly Asset VEA = new Asset("VEA", 1M, AssetType.Stock);
    public static readonly Asset VGSLX = new Asset("VGSLX", 1M, AssetType.Stock, default, "VNQ");
    public static readonly Asset VNQ = new Asset("VNQ", 1M, AssetType.Stock);
    public static readonly Asset VTI = new Asset("VTI", 1M, AssetType.Stock);
    public static readonly Asset VWO = new Asset("VWO", 1M, AssetType.Stock);
    public static readonly Asset VXUS = new Asset("VXUS", 1M, AssetType.Stock);
    
    public static readonly Asset bitcoin = new Asset("bitcoin", 1M, AssetType.Crypto, default, "GBTC");
    public static readonly Asset ethereum = new Asset("ethereum", 1M, AssetType.Crypto, default, "ETHE");

    public static IEnumerable<Asset> GetAllSeeds()
    {
        
        var type = typeof(AssetSeeds);
        var properties = type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.FieldType == typeof(Asset));
        foreach (var property in properties)
        {
            yield return property.GetValue(null) as Asset ?? throw new InvalidOperationException();
        }
    }
}