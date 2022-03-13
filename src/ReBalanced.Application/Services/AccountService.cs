using Ardalis.GuardClauses;
using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Entities.Aggregates;
using ReBalanced.Domain.Providers;

namespace ReBalanced.Application.Services;

public class AccountService : IAccountService
{
    private readonly IEntityRepository<Account> _accountRepository;
    private readonly IAssetService _assetService;

    public AccountService(
        IEntityRepository<Account> accountRepository,
        IAssetService assetService)
    {
        _accountRepository = accountRepository;
        _assetService = assetService;
    }

    public async Task<Account> Create(string name, AccountType accountType, HoldingType holdingType,
        HashSet<string> permissibleAssets)
    {
        var account = new Account(name, accountType, holdingType, permissibleAssets);

        return await _accountRepository.Create(account);
    }

    public async Task<Account?> Get(Guid id)
    {
        return await _accountRepository.Get(id);
    }

    public async Task<Account?> Update(Guid id, Account account)
    {
        return await _accountRepository.Update(id, account);
    }

    public async Task<bool> Delete(Guid id)
    {
        return await _accountRepository.Delete(id);
    }

    public async Task<decimal> TotalValue(Guid id)
    {
        var account = await Get(id);
        Guard.Against.NotFound(id, id, nameof(Account));

        return _assetService.TotalValue(account!.Holdings);
    }
}