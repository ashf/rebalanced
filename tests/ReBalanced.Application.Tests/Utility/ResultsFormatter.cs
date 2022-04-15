using System.Collections.Generic;
using System.Linq;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using Xunit.Abstractions;

namespace ReBalanced.Application.Tests.Utility;

public static class ResultsFormatter
{
    public static void PrintResults(this ITestOutputHelper testOutputHelper,
        Dictionary<string, decimal> rebalanceResults, Portfolio portfolio)
    {
        foreach (var (asset, amount) in rebalanceResults!)
        {
            var assetName = asset.Split('_')[0];
            var accountName = asset.Split('_')[1];
            var account = portfolio.Accounts.Values.First(x => x.Name == accountName);
            var difference = account.AssetDifference(assetName, amount);
            testOutputHelper.WriteLine($"{asset} : {amount} ({difference})");
        }
    }
}