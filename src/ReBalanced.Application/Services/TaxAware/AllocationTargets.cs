using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Application.Services.TaxAware;

public record AllocationSettings(
    decimal CryptoAllocation,
    decimal BitcoinMarketCap,
    decimal EthereumMarketCap,
    decimal CashTarget,
    bool IncludeCrypto);

// mirrors the "investment rebalance" spreadsheet's Target % formulas
public static class AllocationTargets
{
    private const decimal MinimumRealEstate = 0.05M;
    private const decimal BondShare = 0.06M;
    private const decimal UsShare = 0.55M;
    private const decimal DevelopedShareOfInternational = 0.75M;

    public static Dictionary<AssetClass, decimal> Calculate(
        IReadOnlyDictionary<AssetClass, decimal> valueByClass, AllocationSettings settings)
    {
        var total = valueByClass.Values.Sum();
        var marketCaps = settings.BitcoinMarketCap + settings.EthereumMarketCap;
        if (total <= 0) throw new InvalidOperationException("The portfolio total is not positive");
        if (settings.IncludeCrypto && marketCaps <= 0)
            throw new InvalidOperationException("Crypto is included but the bitcoin and ethereum market caps are missing");

        var realEstate = valueByClass.GetValueOrDefault(AssetClass.RealEstate) / total;
        var cash = settings.CashTarget / total;
        var reit = (1 - cash) * Math.Max(0, MinimumRealEstate - realEstate);
        var crypto = settings.IncludeCrypto ? settings.CryptoAllocation : 0;
        var bonds = (1 - cash - realEstate - reit - crypto) * BondShare;
        var stocks = 1 - cash - realEstate - reit - crypto - bonds;

        var targets = new Dictionary<AssetClass, decimal>
        {
            [AssetClass.UsStocks] = stocks * UsShare,
            [AssetClass.DevelopedStocks] = stocks * (1 - UsShare) * DevelopedShareOfInternational,
            [AssetClass.EmergingStocks] = stocks * (1 - UsShare) * (1 - DevelopedShareOfInternational),
            [AssetClass.Bonds] = bonds,
            [AssetClass.Cash] = cash,
            [AssetClass.Reit] = reit,
            [AssetClass.RealEstate] = realEstate
        };

        if (settings.IncludeCrypto)
        {
            targets[AssetClass.Bitcoin] = crypto * settings.BitcoinMarketCap / marketCaps;
            targets[AssetClass.Ethereum] = crypto * settings.EthereumMarketCap / marketCaps;
        }

        return targets.ToDictionary(x => x.Key, x => x.Value * total);
    }
}
