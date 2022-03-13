using ReBalanced.Domain.Entities;

namespace ReBalanced.Application.Services.Interfaces;

public interface IRebalanceService
{
    public Task<Dictionary<string, decimal>?> Rebalance(Portfolio portfolio);
}