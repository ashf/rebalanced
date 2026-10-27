using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Application.Services.TaxAware;

public record Position(string Ticker, AssetClass AssetClass, decimal Quantity, decimal Price)
{
    public decimal Value => Quantity * Price;
}

// a non-tradable account still counts toward the portfolio total and its asset classes
public record HoldingAccount(string Name, AccountType AccountType, bool Tradable, IReadOnlyList<Position> Positions)
{
    public decimal Value => Positions.Sum(x => x.Value);
}

public record Trade(string Account, string Ticker, decimal Amount, decimal Price)
{
    public decimal Shares => Price == 0 ? 0 : Amount / Price;
}

public record RebalancePlan(
    IReadOnlyList<Trade> Trades,
    IReadOnlyDictionary<AssetClass, decimal> Before,
    IReadOnlyDictionary<AssetClass, decimal> After,
    IReadOnlyDictionary<AssetClass, decimal> Targets,
    decimal Total,
    IReadOnlyList<string> Warnings);
