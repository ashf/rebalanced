using Microsoft.EntityFrameworkCore;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.Providers;

namespace ReBalanced.Infastructure.Providers;

public class HoldingRepository : IEntityRepository<Holding>
{
    private readonly Context _scheduleContext;

    public HoldingRepository(
        Context scheduleContext)
    {
        _scheduleContext = scheduleContext;
    }

    public async Task<Holding> Create(Holding holding)
    {
        await _scheduleContext.Holding.AddAsync(holding);
        await _scheduleContext.SaveChangesAsync();
        return holding;
    }

    public async Task<bool> Delete(Guid id)
    {
        var holding = await _scheduleContext.Holding.SingleOrDefaultAsync(x => x.Id == id);

        if (holding is null) return false;

        holding.IsDeleted = true;

        await _scheduleContext.SaveChangesAsync();

        return true;
    }

    public async Task<Holding?> Get(Guid id)
    {
        return await _scheduleContext.Holding
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public async Task<ICollection<Holding>> Get(ICollection<Guid> ids)
    {
        return await _scheduleContext.Holding
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Holding?> Update(Guid id, Holding targetHolding)
    {
        var sourceHolding = await _scheduleContext.Holding
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (sourceHolding is null) return null;

        sourceHolding = targetHolding;

        await _scheduleContext.SaveChangesAsync();

        return sourceHolding;
    }
}