using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Application.Services.Interfaces;

public interface IAssetService
{
    Task<decimal> Value(Holding holding);
    decimal TotalValue(Account account, bool includeFractional = true);
}