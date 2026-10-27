using System.Globalization;

namespace ReBalanced.Cli;

public record CliOptions(
    string Path,
    bool? IncludeCrypto,
    bool? IncludeCash,
    decimal? CashTarget,
    decimal Band,
    bool LivePrices)
{
    public const string Usage = """
        Usage: rebalanced <spreadsheet.xlsx> [options]

          --crypto include|ignore   rebalance crypto, or hold it as is (default: the sheet's INCLUDE CRYPTO)
          --cash include|ignore     invest bank balances, or leave them out (default: the sheet's INCLUDE BANKS)
          --cash-target <dollars>   cash to keep uninvested (default: the sheet's CASH target)
          --band <fraction>         drift allowed before taxable sells, e.g. 0.01 = 1 point (default: 0.01)
          --live-prices             price holdings with Twelve Data instead of the sheet's saved prices
        """;

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        string? path = null;
        bool? includeCrypto = null, includeCash = null;
        decimal? cashTarget = null;
        var band = 0.01M;
        var livePrices = false;

        for (var i = 0; i < args.Count; i++)
        {
            string Next() => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");

            switch (args[i])
            {
                case "--crypto": includeCrypto = IncludeOrIgnore(Next()); break;
                case "--cash": includeCash = IncludeOrIgnore(Next()); break;
                case "--cash-target": cashTarget = Decimal(Next()); break;
                case "--band": band = Decimal(Next()); break;
                case "--live-prices": livePrices = true; break;
                case var option when option.StartsWith('-'): throw new ArgumentException($"Unknown option {option}");
                default: path = path is null ? args[i] : throw new ArgumentException("Only one spreadsheet path"); break;
            }
        }

        return new CliOptions(path ?? throw new ArgumentException("Missing spreadsheet path"),
            includeCrypto, includeCash, cashTarget, band, livePrices);
    }

    private static bool IncludeOrIgnore(string value)
    {
        return value switch
        {
            "include" => true,
            "ignore" => false,
            _ => throw new ArgumentException($"Expected include or ignore, got {value}")
        };
    }

    private static decimal Decimal(string value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number >= 0
            ? number
            : throw new ArgumentException($"Expected a non-negative number, got {value}");
    }
}
