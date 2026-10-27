using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Cli;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Infrastructure.TwelveData;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine(CliOptions.Usage);
    return args.Length == 0 ? 1 : 0;
}

try
{
    var options = CliOptions.Parse(args);
    var portfolio = SpreadsheetImporter.Import(options.Path);
    if (options.LivePrices) portfolio = await LivePrices.Apply(portfolio);

    foreach (var warning in portfolio.Warnings ?? Array.Empty<string>()) Console.Error.WriteLine($"warning: {warning}");
    if (options.CryptoAllocation is not null && !(options.IncludeCrypto ?? portfolio.IncludeCrypto))
        Console.Error.WriteLine("warning: --crypto-allocation has no effect while crypto is ignored");
    var result = PlanBuilder.Build(portfolio, options);
    Report.Print(result);
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or HttpRequestException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static class LivePrices
{
    private static readonly Dictionary<string, string> CryptoSymbols = new()
    {
        ["bitcoin"] = "BTC/USD",
        ["ethereum"] = "ETH/USD"
    };

    public static async Task<ImportedPortfolio> Apply(ImportedPortfolio portfolio)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"), true)
            .AddEnvironmentVariables()
            .Build();
        var apiKey = config["TwelveData:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("--live-prices needs TwelveData:ApiKey (see README)");

        using var client = new HttpClient {BaseAddress = new Uri("https://api.twelvedata.com/")};
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("apikey", apiKey);

        var symbols = portfolio.Sections.SelectMany(x => x.Account.Positions)
            .Where(x => x.AssetClass is not (AssetClass.Cash or AssetClass.RealEstate))
            .Select(x => Symbol(x.Ticker)).Distinct().ToList();
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
                        prices.TryGetValue(Symbol(x.Ticker), out var price) ? x with {Price = price} : x).ToList()
                }
            }).ToList()
        };
    }

    private static string Symbol(string ticker)
    {
        return CryptoSymbols.GetValueOrDefault(ticker, ticker);
    }
}

static class Report
{
    private static readonly Dictionary<AssetClass, string> Names = new()
    {
        [AssetClass.UsStocks] = "US stocks",
        [AssetClass.DevelopedStocks] = "Developed intl",
        [AssetClass.EmergingStocks] = "Emerging mkts",
        [AssetClass.Bonds] = "Bonds",
        [AssetClass.Bitcoin] = "Bitcoin",
        [AssetClass.Ethereum] = "Ethereum",
        [AssetClass.Cash] = "Cash",
        [AssetClass.Reit] = "REIT",
        [AssetClass.RealEstate] = "Real estate"
    };

    public static void Print(PlanResult result)
    {
        var plan = result.Plan;
        Console.WriteLine($"Portfolio total {plan.Total:C} (crypto {(result.IncludeCrypto ? "included" : "held as is")}, " +
                          $"bank cash {(result.IncludeCash ? "included" : "ignored")})");
        if (result.Settings is { Ratios: { } ratios } settings)
            Console.WriteLine($"Allocation: US {ratios.UsShare:P1} of stocks, developed {ratios.DevelopedShareOfInternational:P1} " +
                              $"of international, bonds {ratios.BondShare:P1}, real estate floor {ratios.MinimumRealEstate:P1}, " +
                              $"crypto {(settings.IncludeCrypto ? settings.CryptoAllocation.ToString("P1") : "held as is")}, " +
                              $"cash target {settings.CashTarget:C}");
        Console.WriteLine();

        foreach (var account in result.Accounts.Where(x => x.Value != 0))
            Console.WriteLine($"  {account.Name,-28} {(account.Tradable ? "trade" : "hold"),-6} {account.Value,16:C}");
        Console.WriteLine();

        if (result.IncludeCash && result.BankCash < 0)
            Console.WriteLine($"Bank balances net to {result.BankCash:C} (cards exceed cash), so nothing is transferred.\n");

        if (result.BankTransfer > 0)
            Console.WriteLine($"1. Transfer {result.BankTransfer:C} (net of card balances) from your banks into Taxable.\n");

        if (plan.Trades.Count == 0) Console.WriteLine("No trades needed: every asset class is within its band.");

        foreach (var account in plan.Trades.GroupBy(x => x.Account))
        {
            Console.WriteLine($"{account.Key} (sell first, then buy)");
            foreach (var trade in account.OrderBy(x => x.Amount))
                Console.WriteLine($"  {(trade.Amount < 0 ? "SELL" : "BUY"),-5} {Math.Abs(trade.Amount),14:C}  {trade.Ticker,-6}" +
                                  (trade.Price > 0 ? $"  ~{Math.Abs(trade.Shares):N3} sh @ {trade.Price:C}" : ""));
            Console.WriteLine();
        }

        Console.WriteLine($"{"Asset class",-16} {"Target",8} {"Before",8} {"After",8}");
        foreach (var assetClass in plan.Targets.Keys.Union(plan.Before.Keys).OrderBy(x => x))
            Console.WriteLine($"{Names[assetClass],-16} {Percent(plan.Targets, assetClass, plan.Total),8} " +
                              $"{Percent(plan.Before, assetClass, plan.Total),8} {Percent(plan.After, assetClass, plan.Total),8}");

        foreach (var warning in plan.Warnings) Console.WriteLine($"\nwarning: {warning}");

        if (plan.Trades.Any(x => x.Account == "Taxable" && x.Amount < 0))
            Console.WriteLine("\nTaxable sells realize capital gains or losses. Check each lot's cost basis before selling.");
    }

    private static string Percent(IReadOnlyDictionary<AssetClass, decimal> values, AssetClass assetClass, decimal total)
    {
        return (values.GetValueOrDefault(assetClass) / total).ToString("P2");
    }
}
