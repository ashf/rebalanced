namespace ReBalanced.Domain.Aggregates.PortfolioAggregate;

public enum AssetClass
{
    UsStocks,
    DevelopedStocks,
    EmergingStocks,
    Bonds,
    Bitcoin,
    Ethereum,
    Cash,
    Reit,
    RealEstate
}

public static class AssetClasses
{
    public const string CashTicker = "CASH";

    private static readonly Dictionary<string, AssetClass> ByTicker = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VTI"] = AssetClass.UsStocks,
        ["VEA"] = AssetClass.DevelopedStocks,
        ["VWO"] = AssetClass.EmergingStocks,
        ["BND"] = AssetClass.Bonds,
        ["TMBRX"] = AssetClass.Bonds,
        ["bitcoin"] = AssetClass.Bitcoin,
        ["GBTC"] = AssetClass.Bitcoin,
        ["BTC"] = AssetClass.Bitcoin, // Grayscale Bitcoin Mini Trust ETF
        ["ethereum"] = AssetClass.Ethereum,
        ["ETHE"] = AssetClass.Ethereum,
        ["ETH"] = AssetClass.Ethereum, // Grayscale Ethereum Mini Trust ETF
        [CashTicker] = AssetClass.Cash,
        ["VNQ"] = AssetClass.Reit,
        ["VGSLX"] = AssetClass.Reit,
        ["PROPERTY"] = AssetClass.RealEstate
    };

    // the ticker bought when an account needs more of a class
    private static readonly Dictionary<AssetClass, string> Preferred = new()
    {
        [AssetClass.UsStocks] = "VTI",
        [AssetClass.DevelopedStocks] = "VEA",
        [AssetClass.EmergingStocks] = "VWO",
        [AssetClass.Bonds] = "BND",
        [AssetClass.Bitcoin] = "BTC",
        [AssetClass.Ethereum] = "ETH",
        [AssetClass.Cash] = CashTicker,
        [AssetClass.Reit] = "VNQ"
    };

    public static AssetClass? Of(string ticker)
    {
        return ByTicker.TryGetValue(ticker, out var assetClass) ? assetClass : null;
    }

    public static string? PreferredTicker(AssetClass assetClass)
    {
        return Preferred.GetValueOrDefault(assetClass);
    }
}
