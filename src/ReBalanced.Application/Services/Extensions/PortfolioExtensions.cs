using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Application.Services.Extensions;

public static class PortfolioExtensions
{
    public static async Task<decimal> TotalValue(this Portfolio portfolio, IAssetService assetService, bool accountFractional = false)
    {
        var portfolioTotal = 0M;
        
        foreach (var account in portfolio.Accounts.Values)
        {
            portfolioTotal += await assetService.TotalValue(account, !accountFractional || account.AllowFractional);
        }

        return portfolioTotal;
    }
}