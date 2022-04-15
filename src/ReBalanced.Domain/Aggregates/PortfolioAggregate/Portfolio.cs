using Ardalis.GuardClauses;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.Entities.Aggregates;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Domain.Aggregates.PortfolioAggregate;

public class Portfolio : BaseEntity, IAggregateRoot
{
    public Portfolio(string name)
    {
        Name = name;

        Allocations.Add("CASH", new Allocation("CASH", 100));
    }

    public string Name { get; }
    public Dictionary<Guid, Account> Accounts { get; } = new();
    public Dictionary<string, Allocation> Allocations { get; private set; } = new();

    public void SetAllocation(ICollection<Allocation> newAllocationRules)
    {
        Guard.Against.InvalidInput(
            newAllocationRules,
            nameof(Allocation.Percentage),
            x => x.Sum(allocation => allocation.Percentage) == 1,
            "Allocation Percentages must add up to 100%");

        Allocations = newAllocationRules.ToDictionary(x => x.AssetTicker, x => x);
    }

    public void AddAccount(Account account)
    {
        Guard.Against.InvalidInput(account, nameof(Account), x => !Accounts.ContainsKey(x.Id),
            "Portfolio already contains this account");

        Accounts.Add(account.Id, account);
    }

    public void DeleteAccount(Guid accountId)
    {
        Guard.Against.InvalidInput(accountId, nameof(accountId), x => !Accounts.ContainsKey(x),
            "Portfolio doesn't contain this account");

        Accounts.Remove(accountId);
    }

    public void AddHolding(Guid accountId, Holding holding)
    {
        Guard.Against.InvalidInput(accountId, nameof(accountId), x => !Accounts.ContainsKey(x),
            "Portfolio doesn't contain this account");

        Accounts[accountId].AddHolding(holding);
    }

    public HashSet<Asset> Assets()
    {
        return Accounts.Values
            .Select(account => account.Holdings)
            .SelectMany(holdings => holdings.Select(holding => holding.Asset))
            .ToHashSet();
    }
}