using Microsoft.EntityFrameworkCore;
using ReBalanced.Domain.Entities.Aggregates;
using ReBalanced.Domain.Providers;

namespace ReBalanced.Infastructure.Providers;

public class AccountRepository : IEntityRepository<Account>
{
    private readonly Context _scheduleContext;

    public AccountRepository(
        Context scheduleContext)
    {
        _scheduleContext = scheduleContext;
    }

    public async Task<Account> Create(Account account)
    {
        await _scheduleContext.Account.AddAsync(account);
        await _scheduleContext.SaveChangesAsync();
        return account;
    }

    public async Task<bool> Delete(Guid id)
    {
        var Account = await _scheduleContext.Account.SingleOrDefaultAsync(x => x.Id == id);

        if (Account is null) return false;

        Account.IsDeleted = true;

        await _scheduleContext.SaveChangesAsync();

        return true;
    }

    public async Task<Account?> Get(Guid id)
    {
        return await _scheduleContext.Account
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public async Task<ICollection<Account>> Get(ICollection<Guid> ids)
    {
        return await _scheduleContext.Account
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Account?> Update(Guid id, Account targetAccount)
    {
        var sourceAccount = await _scheduleContext.Account
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (sourceAccount is null) return null;

        sourceAccount = targetAccount;

        await _scheduleContext.SaveChangesAsync();

        return sourceAccount;
    }
}