using LiteDB;
using LiteDB.Async;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;
using ReBalanced.Infrastructure.LiteDB;

namespace ReBalanced.Infrastructure.Repositories;

public class LiteDbPortfolioRepository : IEntityRepository<Portfolio>
{
    private readonly ILiteCollectionAsync<Portfolio> _portfolios;

    public LiteDbPortfolioRepository(LiteDbContext db)
    {
        _portfolios = db.Context.GetCollection<Portfolio>("portfolios");
    }

    public async Task<Portfolio> Create(Portfolio portfolio)
    {
        await _portfolios.InsertAsync(portfolio);
        return portfolio;
    }

    public async Task<bool> Delete(Guid id)
    {
        var portfolio = await _portfolios.FindByIdAsync(id);

        if (portfolio is null) return false;

        portfolio.IsDeleted = true;

        await _portfolios.UpdateAsync(portfolio);

        return true;
    }

    public async Task<Portfolio?> Get(Guid id)
    {
        var portfolio = await _portfolios.FindByIdAsync(id);
        return !portfolio.IsDeleted ? portfolio : null;
    }

    public async Task<ICollection<Portfolio>> Get(ICollection<Guid> ids)
    {
        return (await _portfolios.FindAsync(x => ids.Contains(x.Id) && !x.IsDeleted)).ToList();
    }

    public async Task<Portfolio?> Update(Guid id, Portfolio targetPortfolio)
    {
        var sourcePortfolio = await _portfolios.FindByIdAsync(id);

        if (sourcePortfolio is null) return null;
        if (sourcePortfolio.IsDeleted) return null;

        await _portfolios.UpdateAsync(id, targetPortfolio);

        return targetPortfolio;
    }
}