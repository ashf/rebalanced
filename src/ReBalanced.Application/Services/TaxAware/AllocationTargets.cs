using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Application.Services.TaxAware;

public record AllocationSettings(
    decimal CryptoAllocation,
    decimal BitcoinMarketCap,
    decimal EthereumMarketCap,
    decimal CashTarget,
    bool IncludeCrypto,
    AllocationRatios? Ratios = null);

public record AllocationRatios(
    decimal UsShare,
    decimal DevelopedShareOfInternational,
    decimal BondShare,
    decimal MinimumRealEstate)
{
    // the constants in the "investment rebalance" spreadsheet's formulas
    public static AllocationRatios Default { get; } = new(0.55M, 0.75M, 0.06M, 0.05M);
}

// mirrors the "investment rebalance" spreadsheet's Target % formulas
public static class AllocationTargets
{
    public static Dictionary<AssetClass, decimal> Calculate(
        IReadOnlyDictionary<AssetClass, decimal> valueByClass, AllocationSettings settings)
    {
        var total = valueByClass.Values.Sum();
        var marketCaps = settings.BitcoinMarketCap + settings.EthereumMarketCap;
        if (total <= 0) throw new InvalidOperationException("The portfolio total is not positive");
        if (settings.IncludeCrypto && marketCaps <= 0)
            throw new InvalidOperationException("Crypto is included but the bitcoin and ethereum market caps are missing");

        var (usShare, developedShare, bondShare, minimumRealEstate) = settings.Ratios ?? AllocationRatios.Default;
        var realEstate = valueByClass.GetValueOrDefault(AssetClass.RealEstate) / total;
        var cash = settings.CashTarget / total;
        var reit = (1 - cash) * Math.Max(0, minimumRealEstate - realEstate);
        var crypto = settings.IncludeCrypto ? settings.CryptoAllocation : 0;
        var remainder = 1 - cash - realEstate - reit - crypto;
        if (remainder < 0)
            throw new InvalidOperationException(
                "Cash, real estate and crypto add up to more than the whole portfolio; lower one of them");
        var bonds = remainder * bondShare;
        var stocks = remainder - bonds;

        var targets = new Dictionary<AssetClass, decimal>
        {
            [AssetClass.UsStocks] = stocks * usShare,
            [AssetClass.DevelopedStocks] = stocks * (1 - usShare) * developedShare,
            [AssetClass.EmergingStocks] = stocks * (1 - usShare) * (1 - developedShare),
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
