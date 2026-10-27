using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Cli;

public static class Report
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
        var settings = result.Settings;
        var ratios = settings.Ratios ?? AllocationRatios.Default;
        Console.WriteLine($"Portfolio total {plan.Total:C} (crypto {(settings.IncludeCrypto ? "included" : "held as is")}, " +
                          $"bank cash {(result.IncludeCash ? "included" : "ignored")})");
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
            Console.WriteLine($"1. Transfer {result.BankTransfer:C} (net of card balances) from your banks into {result.TransferAccount}.\n");

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

        var taxable = result.Accounts.Where(x => x.AccountType == AccountType.Taxable).Select(x => x.Name).ToHashSet();
        if (plan.Trades.Any(x => taxable.Contains(x.Account) && x.Amount < 0))
            Console.WriteLine("\nTaxable sells realize capital gains or losses. Check each lot's cost basis before selling.");
    }

    private static string Percent(IReadOnlyDictionary<AssetClass, decimal> values, AssetClass assetClass, decimal total)
    {
        return (values.GetValueOrDefault(assetClass) / total).ToString("P2");
    }
}
