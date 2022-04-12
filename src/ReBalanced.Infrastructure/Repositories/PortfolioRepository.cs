using Microsoft.EntityFrameworkCore;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;
using ReBalanced.Infastructure;

namespace ReBalanced.Infrastructure.Providers;

public class PortfolioRepository : IEntityRepository<Portfolio>
{
    private readonly Context _scheduleContext;

    public PortfolioRepository(
        Context scheduleContext)
    {
        _scheduleContext = scheduleContext;
    }

    public async Task<Portfolio> Create(Portfolio portfolio)
    {
        await _scheduleContext.Portfolio.AddAsync(portfolio);
        await _scheduleContext.SaveChangesAsync();
        return portfolio;
    }

    public async Task<bool> Delete(Guid id)
    {
        var portfolio = await _scheduleContext.Portfolio.SingleOrDefaultAsync(x => x.Id == id);

        if (portfolio is null) return false;

        portfolio.IsDeleted = true;

        await _scheduleContext.SaveChangesAsync();

        return true;
    }

    public async Task<Portfolio?> Get(Guid id)
    {
        return await _scheduleContext.Portfolio
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public async Task<ICollection<Portfolio>> Get(ICollection<Guid> ids)
    {
        return await _scheduleContext.Portfolio
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Portfolio?> Update(Guid id, Portfolio targetPortfolio)
    {
        var sourcePortfolio = await _scheduleContext.Portfolio
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (sourcePortfolio is null) return null;

        sourcePortfolio = targetPortfolio;

        await _scheduleContext.SaveChangesAsync();

        return sourcePortfolio;
    }
}