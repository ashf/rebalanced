using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Application.Services.Interfaces;

public interface IAccountService : IBaseEntityService<Account>
{
    public Task<Account> Create(string name, AccountType accountType, HoldingType holdingType,
        HashSet<string> permissibleAssets);

    public Task<decimal> TotalValue(Guid id);
}