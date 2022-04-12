using Ardalis.GuardClauses;
using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.Entities.Aggregates;
using ReBalanced.Domain.Providers;

namespace ReBalanced.Application.Services;

public class PortfolioService : IPortfolioService
{
    private readonly IEntityRepository<Portfolio> _portfolioRepository;
    private readonly IRebalanceService _rebalanceService;

    public PortfolioService(
        IEntityRepository<Portfolio> portfolioRepository,
        IRebalanceService rebalanceService)
    {
        _portfolioRepository = portfolioRepository;
        _rebalanceService = rebalanceService;
    }

    public async Task<Portfolio> Create(string name)
    {
        var portfolio = new Portfolio(name);

        return await _portfolioRepository.Create(portfolio);
    }

    public async Task<Portfolio?> Get(Guid id)
    {
        return await _portfolioRepository.Get(id);
    }

    public async Task<Portfolio?> Update(Guid id, Portfolio portfolio)
    {
        return await _portfolioRepository.Update(id, portfolio);
    }

    public async Task<bool> Delete(Guid id)
    {
        return await _portfolioRepository.Delete(id);
    }

    public async Task<Portfolio?> AddAccount(Guid id, Account account)
    {
        var portfolio = await _portfolioRepository.Get(id);
        if (portfolio is null) return null;

        portfolio.AddAccount(account);
        return await _portfolioRepository.Update(id, portfolio);
    }

    public async Task Rebalance(Guid id)
    {
        var portfolio = await Get(id);

        Guard.Against.Null(portfolio, nameof(portfolio));

        var reblanceResult = _rebalanceService.Rebalance(portfolio);

        // TODO: do something with result
    }
}