using System.Collections.Generic;
using System.Linq;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Application.Tests.Utility;

public static class AssetsInMemory
{
    public static Dictionary<string, Asset> Assets { get; } = new List<Asset>
    {
        new("BND", 77.25M, AssetType.Stock, false),
        new("BTC", 40505M, AssetType.Crpyto),
        new("CASH", 1M, AssetType.Cash),
        new("ETH", 3036.71M, AssetType.Crpyto),
        new("ETHE", 22.48M, AssetType.Stock, false, "ETH"),
        new("GBTC", 28.16M, AssetType.Stock, false, "BTC"),
        new("Property", 1M, AssetType.Property),
        new("VEA", 47.27M, AssetType.Stock, false),
        new("VGSLX", 155.59M, AssetType.Stock, true, "VNQ"),
        new("VNQ", 109.28M, AssetType.Stock, false),
        new("VTI", 222.63M, AssetType.Stock, false),
        new("VWO", 45.84M, AssetType.Stock, false),
        new("VXUS", 58.58M, AssetType.Stock, false)
    }.ToDictionary(x => x.Ticker, x => x);
}