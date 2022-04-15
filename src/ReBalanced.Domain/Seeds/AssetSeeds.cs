using System.Reflection;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Domain.Seeds;

public class AssetSeeds
{
    public static readonly Asset CASH = new("CASH", 1M, AssetType.Cash);
    public static readonly Asset PROPERTY = new("PROPERTY", 1M, AssetType.Property);

    public static readonly Asset BND = new("BND", 1M, AssetType.Stock);
    public static readonly Asset ETHE = new("ETHE", 1M, AssetType.Stock, default, "ethereum");
    public static readonly Asset GBTC = new("GBTC", 1M, AssetType.Stock, default, "bitcoin");
    public static readonly Asset VEA = new("VEA", 1M, AssetType.Stock);
    public static readonly Asset VGSLX = new("VGSLX", 1M, AssetType.Stock, default, "VNQ");
    public static readonly Asset VNQ = new("VNQ", 1M, AssetType.Stock, default, "VGSLX");
    public static readonly Asset VTI = new("VTI", 1M, AssetType.Stock);
    public static readonly Asset VWO = new("VWO", 1M, AssetType.Stock);
    public static readonly Asset VXUS = new("VXUS", 1M, AssetType.Stock);

    public static readonly Asset bitcoin = new("bitcoin", 1M, AssetType.Crypto, default, "GBTC");
    public static readonly Asset ethereum = new("ethereum", 1M, AssetType.Crypto, default, "ETHE");

    public static IEnumerable<Asset> GetAllSeeds()
    {
        var type = typeof(AssetSeeds);
        var properties = type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.FieldType == typeof(Asset));
        foreach (var property in properties)
            yield return property.GetValue(null) as Asset ?? throw new InvalidOperationException();
    }
}