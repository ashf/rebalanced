using System;
using System.Collections.Generic;
using System.Linq;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Application.Tests.Utility;

public static class AssetsInMemory
{
    public static Dictionary<string, Asset> Assets { get; } = new List<Asset>
    {
        new("BND", 77.25M, AssetType.Stock, DateTime.UtcNow, false),
        new("bitcoin", 40505M, AssetType.Crypto, DateTime.UtcNow),
        new("CASH", 1M, AssetType.Cash, DateTime.UtcNow),
        new("ethereum", 3036.71M, AssetType.Crypto, DateTime.UtcNow),
        new("ETHE", 22.48M, AssetType.Stock, DateTime.UtcNow, false, "ethereum"),
        new("GBTC", 28.16M, AssetType.Stock, DateTime.UtcNow, false, "bitcoin"),
        new("Property", 1M, AssetType.Property, DateTime.UtcNow),
        new("VEA", 47.27M, AssetType.Stock, DateTime.UtcNow, false),
        new("VGSLX", 155.59M, AssetType.Stock, DateTime.UtcNow, true, "VNQ"),
        new("VNQ", 109.28M, AssetType.Stock, DateTime.UtcNow, false),
        new("VTI", 222.63M, AssetType.Stock, DateTime.UtcNow, false),
        new("VWO", 45.84M, AssetType.Stock, DateTime.UtcNow, false),
        new("VXUS", 58.58M, AssetType.Stock, DateTime.UtcNow, false)
    }.ToDictionary(x => x.Ticker, x => x);
}