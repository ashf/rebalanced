using System.Collections.Generic;
using System.Threading.Tasks;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;
using Xunit;
using Xunit.Abstractions;

namespace ReBalanced.Application.Tests.Utility;

public static class ResultsChecker
{
    public static async Task WithinTolerance(
        ITestOutputHelper testOutputHelper, Dictionary<string, decimal> rebalanceResults,
        IAssetRepository assetRepository, decimal portfolioTotal,
        IReadOnlyDictionary<string, Allocation> allocations, decimal tolerance)
    {
        var resultsByTicker = new Dictionary<string, decimal>();

        foreach (var (key, amount) in rebalanceResults)
        {
            if (amount == 0) continue;

            var ticker = key.Split('_')[0];

            var asset = await assetRepository.Get(ticker);
            Assert.NotNull(asset);

            var allocatedTicker = ticker;
            if (!allocations.ContainsKey(ticker) && !string.IsNullOrWhiteSpace(asset!.EquivalentTicker))
            {
                Assert.True(allocations.ContainsKey(asset.EquivalentTicker));
                allocatedTicker = asset.EquivalentTicker;
            }

            var value = amount * await assetRepository.GetValue(ticker) ?? 0;

            if (!resultsByTicker.ContainsKey(allocatedTicker)) resultsByTicker[allocatedTicker] = value;
            else resultsByTicker[allocatedTicker] += value;
        }

        testOutputHelper.WriteLine($"\ntolerance = {tolerance:P}");

        foreach (var (ticker, value) in resultsByTicker)
        {
            if (value == 0 && !allocations.ContainsKey(ticker)) continue;
            var finalAllocation = value / portfolioTotal;
            var lowerTolerance = allocations[ticker].Percentage * (1M - tolerance);
            var upperTolerance = allocations[ticker].Percentage * (1M + tolerance);
            Assert.True(finalAllocation >= lowerTolerance,
                $"{ticker}: {finalAllocation:P} < {lowerTolerance:P)}");
            Assert.True(finalAllocation <= upperTolerance,
                $"{ticker}: {finalAllocation:P} > {upperTolerance:P}");
            testOutputHelper.WriteLine(
                $"{ticker}: {lowerTolerance:P} <= {finalAllocation:P} <= {upperTolerance:P} ({allocations[ticker].Percentage:P})");
        }
    }
}