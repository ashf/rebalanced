using Ardalis.GuardClauses;
using ReBalanced.Domain.Entities.Aggregates;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Domain.Entities;

public class Portfolio : BaseEntity
{
    public Portfolio(string name)
    {
        Name = name;

        Allocations.Add("CASH", new Allocation("CASH", 100));
    }

    public string Name { get; }
    public Dictionary<Guid, Account> Accounts { get; } = new();
    public Dictionary<string, Allocation> Allocations { get; private set; } = new();

    public void UpdateAllocations(ICollection<Allocation> newAllocationRules)
    {
        Guard.Against.InvalidInput(
            newAllocationRules,
            nameof(Allocation.Percentage),
            x => x.Sum(allocation => allocation.Percentage) != 100,
            "Allocation Percentages must add up to 100%");

        Allocations = newAllocationRules.ToDictionary(x => x.AssetTicker, x => x);
    }

    public void AddAccount(Account account)
    {
        Guard.Against.InvalidInput(account, nameof(Account), x => !Accounts.ContainsKey(x.Id),
            "Portfolio already contains this account");

        Accounts.Add(account.Id, account);
    }

    public void RemoveAccount(Guid accountId)
    {
        Guard.Against.InvalidInput(accountId, nameof(accountId), x => Accounts.ContainsKey(x),
            "Portfolio already contains this account");

        Accounts.Remove(accountId);
    }
}