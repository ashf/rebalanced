using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Entities;

namespace ReBalanced.Application.Services.Interfaces;

public interface IAssetService
{
    Task<decimal> Value(Holding holding);
    decimal TotalValue(IEnumerable<Holding> holdings);
}