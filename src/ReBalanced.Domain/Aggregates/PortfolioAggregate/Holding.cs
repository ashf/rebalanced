using Ardalis.GuardClauses;
using ReBalanced.Domain.Entities;

namespace ReBalanced.Domain.Aggregates.PortfolioAggregate;

public class Holding : BaseEntity
{
    public Holding(string assetTicker, decimal quantity = 0)
    {
        AssetTicker = assetTicker;
        Quantity = quantity;
    }

    public string AssetTicker { get; }
    public decimal Quantity { get; private set; }

    public void AddQuantity(decimal quantity)
    {
        Guard.Against.Negative(Quantity + quantity, nameof(quantity));

        Quantity += quantity;
    }

    public void UpdateQuantity(decimal quantity)
    {
        Guard.Against.Negative(quantity, nameof(quantity));

        Quantity = quantity;
    }
}