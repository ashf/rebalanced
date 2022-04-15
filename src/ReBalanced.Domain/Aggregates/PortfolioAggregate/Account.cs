using Ardalis.GuardClauses;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.Entities.Aggregates;
using ReBalanced.Domain.Seeds;

namespace ReBalanced.Domain.Aggregates.PortfolioAggregate;

public enum AccountType
{
    Taxable,
    Roth,
    CryptoWallet,
    Property
}

public enum HoldingType
{
    Quantity,
    Percentage
}

public class Account : BaseEntity, IAggregateRoot
{
    private readonly Dictionary<string, Holding> _holdings = new();

    public Account(string name, AccountType accountType, HoldingType holdingType, bool allowFractional,
        HashSet<string> permissibleAssets)
    {
        Name = name;
        AccountType = accountType;
        HoldingType = holdingType;
        AllowFractional = allowFractional;
        PermissibleAssets = permissibleAssets;

        AddHolding(new Holding(AssetSeeds.CASH));

        PriorityAssets = accountType switch
        {
            AccountType.Taxable => new HashSet<string> {"VEA", "VWO"},
            AccountType.Roth => new HashSet<string> {"VNQ", "BND", "GBTC", "ETHE"},
            AccountType.CryptoWallet => new HashSet<string> {"bitcoin", "ethereum"},
            AccountType.Property => new HashSet<string> {"PROPERTY"},
            _ => throw new NotImplementedException()
        };

        UndesiredAssets = accountType switch
        {
            AccountType.Taxable => new HashSet<string> {"GBTC", "ETHE"},
            AccountType.Roth => new HashSet<string> {"CASH"},
            AccountType.CryptoWallet => new HashSet<string> {"CASH"},
            AccountType.Property => new HashSet<string> {"CASH"},
            _ => throw new NotImplementedException()
        };
    }

    public bool AllowFractional { get; }

    public string Name { get; }
    public AccountType AccountType { get; }
    public HoldingType HoldingType { get; }

    public IEnumerable<Holding> Holdings => _holdings.Values.ToList().AsReadOnly();

    public HashSet<string> PriorityAssets { get; set; }
    public HashSet<string> UndesiredAssets { get; set; }
    public HashSet<string> PermissibleAssets { get; set; }

    public void AddHolding(Holding holding)
    {
        Guard.Against.InvalidInput(holding, nameof(Holding), x => PermissibleAssets.Contains(x.Asset.Ticker));

        if (!_holdings.ContainsKey(holding.Asset.Ticker))
            _holdings.Add(holding.Asset.Ticker, holding);
        else
            _holdings[holding.Asset.Ticker].AddQuantity(holding.Quantity);
    }

    public decimal AssetDifference(string assetName, decimal amount)
    {
        if (_holdings.ContainsKey(assetName)) return amount - _holdings[assetName].Quantity;

        return amount;
    }
}