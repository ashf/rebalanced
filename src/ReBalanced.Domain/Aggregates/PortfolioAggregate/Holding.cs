using Ardalis.GuardClauses;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Domain.Aggregates.PortfolioAggregate;

public class Holding : BaseEntity
{
    public Holding(Asset asset, decimal quantity = 0)
    {
        Asset = asset;
        Quantity = quantity;
    }

    public Asset Asset { get; }
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