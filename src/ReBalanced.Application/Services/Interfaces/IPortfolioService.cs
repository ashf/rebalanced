using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Entities;

namespace ReBalanced.Application.Services.Interfaces;

public interface IPortfolioService : IBaseEntityService<Portfolio>
{
    public Task<Portfolio> Create(string name);
}