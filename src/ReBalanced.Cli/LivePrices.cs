using Microsoft.Extensions.Configuration;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Infrastructure.TwelveData;

namespace ReBalanced.Cli;

public static class LivePrices
{
    public static async Task<ImportedPortfolio> Apply(ImportedPortfolio portfolio)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"), true)
            .AddEnvironmentVariables()
            .Build();
        var apiKey = config["TwelveData:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("--live-prices needs TwelveData:ApiKey (see README)");

        using var client = TwelveDataExtensions.Configure(new HttpClient(), apiKey);

        var symbols = portfolio.Sections.SelectMany(x => x.Account.Positions)
            .Where(x => x.AssetClass is not (AssetClass.Cash or AssetClass.RealEstate))
            .Select(x => TwelveDataSymbols.For(x.Ticker)).Distinct().ToList();
        var prices = await new TwelveDataApi(client).GetPrices(symbols);

        foreach (var missing in symbols.Where(x => !prices.ContainsKey(x)))
            Console.Error.WriteLine($"warning: no Twelve Data price for {missing}; using the sheet's price");

        return portfolio with
        {
            Sections = portfolio.Sections.Select(section => section with
            {
                Account = section.Account with
                {
                    Positions = section.Account.Positions.Select(x =>
                        prices.TryGetValue(TwelveDataSymbols.For(x.Ticker), out var price) ? x with {Price = price} : x).ToList()
                }
            }).ToList()
        };
    }
}
