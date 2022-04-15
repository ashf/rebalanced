using System;
using System.Collections.Generic;
using System.Linq;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Application.Tests.Utility;

public static class AssetsInMemory
{
    public static Dictionary<string, Asset> AssetsComplex { get; } = new List<Asset>
    {
        new("BND", 77.07M, AssetType.Stock, DateTime.UtcNow),
        new("bitcoin", 39768.10M, AssetType.Crypto, DateTime.UtcNow, "GBTC"),
        new("CASH", 1M, AssetType.Cash, DateTime.UtcNow),
        new("ethereum", 2993.7M, AssetType.Crypto, DateTime.UtcNow, "ETHE"),
        new("ETHE", 21.93M, AssetType.Stock, DateTime.UtcNow, "ethereum"),
        new("GBTC", 28.54M, AssetType.Stock, DateTime.UtcNow, "bitcoin"),
        new("PROPERTY", 1M, AssetType.Property, DateTime.UtcNow),
        new("VEA", 47.05M, AssetType.Stock, DateTime.UtcNow),
        new("VGSLX", 154.83M, AssetType.Stock, DateTime.UtcNow, "VNQ"),
        new("VNQ", 108.92M, AssetType.Stock, DateTime.UtcNow),
        new("VTI", 221.2M, AssetType.Stock, DateTime.UtcNow),
        new("VWO", 45.5M, AssetType.Stock, DateTime.UtcNow),
        new("VXUS", 58.58M, AssetType.Stock, DateTime.UtcNow)
    }.ToDictionary(x => x.Ticker, x => x);

    public static Dictionary<string, Asset> AssetsSimple { get; } = new List<Asset>
    {
        new("BND", 50M, AssetType.Stock, DateTime.UtcNow),
        new("bitcoin", 40000M, AssetType.Crypto, DateTime.UtcNow, "GBTC"),
        new("CASH", 1M, AssetType.Cash, DateTime.UtcNow),
        new("ethereum", 3000M, AssetType.Crypto, DateTime.UtcNow, "ETHE"),
        new("ETHE", 30M, AssetType.Stock, DateTime.UtcNow, "ethereum"),
        new("GBTC", 40M, AssetType.Stock, DateTime.UtcNow, "bitcoin"),
        new("PROPERTY", 1M, AssetType.Property, DateTime.UtcNow),
        new("VEA", 50M, AssetType.Stock, DateTime.UtcNow),
        new("VGSLX", 150M, AssetType.Stock, DateTime.UtcNow, "VNQ"),
        new("VNQ", 100M, AssetType.Stock, DateTime.UtcNow),
        new("VTI", 200M, AssetType.Stock, DateTime.UtcNow),
        new("VWO", 45M, AssetType.Stock, DateTime.UtcNow),
        new("VXUS", 60M, AssetType.Stock, DateTime.UtcNow)
    }.ToDictionary(x => x.Ticker, x => x);
}