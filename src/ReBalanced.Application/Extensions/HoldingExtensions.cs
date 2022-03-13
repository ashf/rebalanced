using ReBalanced.Domain.Entities;

namespace ReBalanced.Application.Extensions;

public static class HoldingExtensions
{
    public static decimal Value(this Holding holding)
    {
        var tickerValue = 1; // TODO: replace with api call (via infastructure layer)
        return holding.Quantity * tickerValue;
    }
}