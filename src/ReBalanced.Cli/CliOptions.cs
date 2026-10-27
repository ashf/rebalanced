using System.Globalization;

namespace ReBalanced.Cli;

public record CliOptions(
    string Path,
    bool? IncludeCrypto,
    bool? IncludeCash,
    decimal? CashTarget,
    decimal Band,
    bool LivePrices,
    decimal? UsShare = null,
    decimal? DevelopedShare = null,
    decimal? BondShare = null,
    decimal? MinimumRealEstate = null,
    decimal? CryptoAllocation = null)
{
    public const string Usage = """
        Usage: rebalanced <spreadsheet.xlsx> [options]

          --crypto include|ignore   rebalance crypto, or hold it as is (default: the sheet's INCLUDE CRYPTO)
          --cash include|ignore     invest bank balances, or leave them out (default: the sheet's INCLUDE BANKS)
          --cash-target <dollars>   cash to keep uninvested (default: the sheet's CASH target)
          --band <fraction>         drift allowed before taxable sells, e.g. 0.01 = 1 point (default: 0, exact targets)
          --live-prices             price holdings with Twelve Data instead of the sheet's saved prices

        Allocation ratios (fractions; default: worked out from the sheet's Target % formulas):
          --us-share <f>            US share of stocks, e.g. 0.55
          --developed-share <f>     developed share of international stocks, e.g. 0.75
          --bond-share <f>          bonds as a share of what is left after cash, real estate and crypto, e.g. 0.06
          --min-real-estate <f>     real estate floor, topped up with a REIT, e.g. 0.05
          --crypto-allocation <f>   crypto share when crypto is included, e.g. 0.1
        """;

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        string? path = null;
        bool? includeCrypto = null, includeCash = null;
        decimal? cashTarget = null;
        var band = 0M;
        var livePrices = false;
        decimal? usShare = null, developedShare = null, bondShare = null, minimumRealEstate = null, cryptoAllocation = null;

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
                case "--us-share": usShare = Fraction(Next()); break;
                case "--developed-share": developedShare = Fraction(Next()); break;
                case "--bond-share": bondShare = Fraction(Next()); break;
                case "--min-real-estate": minimumRealEstate = Fraction(Next()); break;
                case "--crypto-allocation": cryptoAllocation = Fraction(Next()); break;
                case var option when option.StartsWith('-'): throw new ArgumentException($"Unknown option {option}");
                default: path = path is null ? args[i] : throw new ArgumentException("Only one spreadsheet path"); break;
            }
        }

        return new CliOptions(path ?? throw new ArgumentException("Missing spreadsheet path"),
            includeCrypto, includeCash, cashTarget, band, livePrices,
            usShare, developedShare, bondShare, minimumRealEstate, cryptoAllocation);
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

    private static decimal Fraction(string value)
    {
        var number = Decimal(value);
        return number <= 1 ? number : throw new ArgumentException($"Expected a fraction between 0 and 1, got {value}");
    }

    private static decimal Decimal(string value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number >= 0
            ? number
            : throw new ArgumentException($"Expected a non-negative number, got {value}");
    }
}
