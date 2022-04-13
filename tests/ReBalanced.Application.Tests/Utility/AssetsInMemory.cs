using System;
using System.Collections.Generic;
using System.Linq;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Application.Tests.Utility;

public static class AssetsInMemory
{
    public static Dictionary<string, Asset> AssetsComplex { get; } = new List<Asset>
    {
        new("BND", 77.25M, AssetType.Stock, DateTime.UtcNow),
        new("bitcoin", 40505M, AssetType.Crypto, DateTime.UtcNow),
        new("CASH", 1M, AssetType.Cash, DateTime.UtcNow),
        new("ethereum", 3036.71M, AssetType.Crypto, DateTime.UtcNow),
        new("ETHE", 22.48M, AssetType.Stock, DateTime.UtcNow, "ethereum"),
        new("GBTC", 28.16M, AssetType.Stock, DateTime.UtcNow, "bitcoin"),
        new("Property", 1M, AssetType.Property, DateTime.UtcNow),
        new("VEA", 47.27M, AssetType.Stock, DateTime.UtcNow),
        new("VGSLX", 155.59M, AssetType.Stock, DateTime.UtcNow, "VNQ"),
        new("VNQ", 109.28M, AssetType.Stock, DateTime.UtcNow),
        new("VTI", 222.63M, AssetType.Stock, DateTime.UtcNow),
        new("VWO", 45.84M, AssetType.Stock, DateTime.UtcNow),
        new("VXUS", 58.58M, AssetType.Stock, DateTime.UtcNow)
    }.ToDictionary(x => x.Ticker, x => x);
    
    public static Dictionary<string, Asset> AssetsSimple { get; } = new List<Asset>
    {
        new("BND", 50M, AssetType.Stock, DateTime.UtcNow),
        new("bitcoin", 40000M, AssetType.Crypto, DateTime.UtcNow),
        new("CASH", 1M, AssetType.Cash, DateTime.UtcNow),
        new("ethereum", 3000M, AssetType.Crypto, DateTime.UtcNow),
        new("ETHE", 30M, AssetType.Stock, DateTime.UtcNow, "ethereum"),
        new("GBTC", 40M, AssetType.Stock, DateTime.UtcNow, "bitcoin"),
        new("Property", 1M, AssetType.Property, DateTime.UtcNow),
        new("VEA", 50M, AssetType.Stock, DateTime.UtcNow),
        new("VGSLX", 150M, AssetType.Stock, DateTime.UtcNow, "VNQ"),
        new("VNQ", 100M, AssetType.Stock, DateTime.UtcNow),
        new("VTI", 200M, AssetType.Stock, DateTime.UtcNow),
        new("VWO", 45M, AssetType.Stock, DateTime.UtcNow),
        new("VXUS", 60M, AssetType.Stock, DateTime.UtcNow)
    }.ToDictionary(x => x.Ticker, x => x);
}