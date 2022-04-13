using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Application.Services.Interfaces;

public interface IPortfolioService : IBaseEntityService<Portfolio>
{
    public Task<Portfolio> Create(string name);
}