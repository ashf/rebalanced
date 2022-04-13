using LiteDB;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;
using ReBalanced.Infrastructure.LiteDB;

namespace ReBalanced.Infrastructure.Repositories;

public class LiteDbPortfolioRepository : IEntityRepository<Portfolio>
{
    private readonly ILiteCollection<Portfolio> _portfolios;

    public LiteDbPortfolioRepository(LiteDbContext db)
    {
        _portfolios = db.Context.GetCollection<Portfolio>("portfolios");
    }

    public async Task<Portfolio> Create(Portfolio portfolio)
    {
        await Task.Run(() => _portfolios.Insert(portfolio));
        return portfolio;
    }

    public async Task<bool> Delete(Guid id)
    {
        var portfolio = _portfolios.FindById(id);

        if (portfolio is null) return false;

        portfolio.IsDeleted = true;

        await Task.Run(() => _portfolios.Update(portfolio));

        return true;
    }

    public async Task<Portfolio?> Get(Guid id)
    {
        return await Task.Run(() => _portfolios.FindById(id));
    }

    public async Task<ICollection<Portfolio>> Get(ICollection<Guid> ids)
    {
        return (await Task.Run(() => _portfolios.Find(x => ids.Contains(x.Id)))).ToList();
    }

    public async Task<Portfolio?> Update(Guid id, Portfolio targetPortfolio)
    {
        var sourcePortfolio = _portfolios.FindById(id);

        if (sourcePortfolio is null) return null;
        if (sourcePortfolio.IsDeleted) return null;

        await Task.Run(() => _portfolios.Update(id, targetPortfolio));

        return targetPortfolio;
    }
}