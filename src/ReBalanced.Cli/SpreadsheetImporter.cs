using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Cli;

// Reads the "investment rebalance" Google Sheet exported as .xlsx, using the values the sheet last calculated.
public static class SpreadsheetImporter
{
    private const int Label = 1, Price = 2, Quantity = 4, TargetPercent = 5, Value = 6;

    public static ImportedPortfolio Import(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet(1);

        var marketCap = Find(sheet, "Market Cap");
        var warnings = new List<string>();
        var sections = ReadSections(sheet, Find(sheet, "Total Investment").Address.RowNumber + 1);

        return new ImportedPortfolio(
            sections.Where(x => x.Kind != SectionKind.Banks).ToList(),
            sections.Where(x => x.Kind == SectionKind.Banks).Sum(x => x.Account.Value),
            Number(Find(sheet, "crypto allocation").CellRight()),
            Number(sheet.Cell(marketCap.Address.RowNumber + 1, marketCap.Address.ColumnNumber + 1)),
            Number(sheet.Cell(marketCap.Address.RowNumber + 2, marketCap.Address.ColumnNumber + 1)),
            Number(sheet.Cell(Find(sheet, AssetClasses.CashTicker, Label).Address.RowNumber, 3)),
            Number(Find(sheet, "INCLUDE CRYPTO").CellRight()) != 0,
            Number(Find(sheet, "INCLUDE BANKS").CellRight()) != 0,
            ReadRatios(sheet, warnings),
            warnings);
    }

    // The ratios are constants inside the sheet's formulas, so they are worked back out from the calculated values.
    private static AllocationRatios ReadRatios(IXLWorksheet sheet, List<string> warnings)
    {
        var fallback = AllocationRatios.Default;

        decimal Derived(string name, decimal? value, decimal defaultValue)
        {
            if (value is >= 0 and <= 1) return Math.Round(value.Value, 6);
            warnings.Add($"Could not read the {name} from the sheet; using {defaultValue:P2}");
            return defaultValue;
        }

        decimal? Ratio(decimal? numerator, decimal? denominator) => denominator > 0 ? numerator / denominator : null;
        decimal? Right(string label) => FindOrNull(sheet, label) is { } cell ? NumberOrNull(cell.CellRight()) : null;
        decimal? Target(string ticker) => FindOrNull(sheet, ticker, Label) is { } cell
            ? NumberOrNull(sheet.Cell(cell.Address.RowNumber, TargetPercent))
            : null;

        var (us, developed, emerging) = (Target("VTI"), Target("VEA"), Target("VWO"));
        var cryptoInUse = FindOrNull(sheet, "Actual Crypto Alloc") is { } crypto ? NumberOrNull(crypto.CellBelow()) : 0;
        var bonds = Right("bond allocation");
        var cashAndProperty = Right("cash + prop alloc");

        return new AllocationRatios(
            Derived("US share of stocks", Ratio(us, us + developed + emerging), fallback.UsShare),
            Derived("developed share of international", Ratio(developed, developed + emerging),
                fallback.DevelopedShareOfInternational),
            Derived("bond share", Ratio(bonds, 1 - cashAndProperty - cryptoInUse), fallback.BondShare),
            Derived("real estate floor", MinimumRealEstate(sheet), fallback.MinimumRealEstate));
    }

    private static decimal? MinimumRealEstate(IXLWorksheet sheet)
    {
        var reitCell = FindOrNull(sheet, "reit allocation")?.CellRight();
        if (reitCell is null) return null;

        // reit = (1 - cash) * (floor - real estate) while real estate is under the floor
        var reit = NumberOrNull(reitCell);
        var cash = FindOrNull(sheet, "cash allocation") is { } c ? NumberOrNull(c.CellRight()) : null;
        var realEstate = FindOrNull(sheet, "prop allocation") is { } p ? NumberOrNull(p.CellRight()) : null;
        if (reit > 0)
            return cash < 1 && realEstate is not null ? reit / (1 - cash) + realEstate : null;

        // with real estate above the floor the reit cell is 0, so read the floor from its formula, e.g. IF(C14>0.05, ...)
        var match = Regex.Match(reitCell.FormulaA1, @">\s*([0-9]*\.?[0-9]+)");
        return match.Success ? decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static List<ImportedSection> ReadSections(IXLWorksheet sheet, int startRow)
    {
        var sections = new List<ImportedSection>();
        var lastRow = sheet.LastRowUsed()!.RowNumber();

        for (var row = startRow; row <= lastRow; row++)
        {
            var title = Text(sheet.Cell(row, Label));
            // summary rows such as "Avg Buy Price" carry a number in column B; section headers do not
            if (title.Length == 0 || Read(sheet.Cell(row, Price)).IsNumber) continue;

            var kind = KindOf(title, Text(sheet.Cell(row, Price)));
            var positions = new List<Position>();
            for (row++; row <= lastRow && !Text(sheet.Cell(row, Label)).Equals("Total", StringComparison.OrdinalIgnoreCase); row++)
            {
                var position = ReadPosition(sheet.Row(row), kind, title);
                if (position is not null && position.Value != 0) positions.Add(position);
            }

            var name = kind == SectionKind.Taxable ? "Taxable" : title;
            sections.Add(new ImportedSection(kind,
                new HoldingAccount(name, kind.ToAccountType(), kind.IsTradable(), positions)));
        }

        return sections;
    }

    private static Position? ReadPosition(IXLRow row, SectionKind kind, string section)
    {
        var label = Text(row.Cell(Label));
        if (label.Length == 0) return null;

        if (label.Equals(AssetClasses.CashTicker, StringComparison.OrdinalIgnoreCase) || kind == SectionKind.Banks)
            return Position.Cash(Number(row.Cell(Value)));

        switch (kind)
        {
            case SectionKind.RealEstate:
                // unrecognized sections are real estate only if every row looks like a property
                if (Number(row.Cell(Quantity)) <= 0)
                    throw new InvalidOperationException(
                        $"Row {row.RowNumber()}: \"{section}\" is not a known account section, and \"{label}\" " +
                        "has no quantity in column D, so it is not a property either");
                return new Position(label, AssetClass.RealEstate, 1, Number(row.Cell(Value)));
            case SectionKind.Wallet:
                var coin = section.Contains("Ethereum", StringComparison.OrdinalIgnoreCase) ? "ethereum" : "bitcoin";
                return Priced(row, coin, AssetClasses.Of(coin)!.Value, section);
            default:
                var assetClass = AssetClasses.Of(label) ?? throw new InvalidOperationException(
                    $"Unknown ticker {label} in {section}; add it to AssetClasses");
                return Priced(row, label, assetClass, section);
        }
    }

    private static Position Priced(IXLRow row, string ticker, AssetClass assetClass, string section)
    {
        var quantity = Number(row.Cell(Quantity));
        var price = Number(row.Cell(Price));
        if (quantity != 0 && price <= 0)
            throw new InvalidOperationException(
                $"Row {row.RowNumber()}: {ticker} in {section} has no price; open the sheet in Google Sheets " +
                "so it recalculates, then download it again");
        return new Position(ticker, assetClass, quantity, price);
    }

    private static SectionKind KindOf(string title, string description)
    {
        bool Has(string text) => title.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                                 description.Contains(text, StringComparison.OrdinalIgnoreCase);

        if (Has("Taxable")) return SectionKind.Taxable;
        if (Has("401(k)")) return SectionKind.Retirement401K;
        if (Has("Roth IRA")) return SectionKind.RothIra;
        if (Has("Banks")) return SectionKind.Banks;
        if (Has("Bitcoin") || Has("Ethereum")) return SectionKind.Wallet;
        return SectionKind.RealEstate;
    }

    private static IXLCell Find(IXLWorksheet sheet, string text, int? column = null)
    {
        return FindOrNull(sheet, text, column)
               ?? throw new InvalidOperationException($"Cell \"{text}\" not found in the spreadsheet");
    }

    private static IXLCell? FindOrNull(IXLWorksheet sheet, string text, int? column = null)
    {
        var cells = column is null ? sheet.CellsUsed() : sheet.Column(column.Value).CellsUsed();
        return cells.FirstOrDefault(x => Text(x).Equals(text, StringComparison.OrdinalIgnoreCase));
    }

    private static XLCellValue Read(IXLCell cell)
    {
        // Google Sheets formulas (GOOGLEFINANCE) cannot be recalculated here, so use the values saved with the file
        return cell.HasFormula ? cell.CachedValue : cell.Value;
    }

    private static string Text(IXLCell cell)
    {
        var value = Read(cell);
        return value.IsText ? value.GetText().Trim() : "";
    }

    private static decimal? NumberOrNull(IXLCell cell)
    {
        var value = Read(cell);
        return value.IsNumber ? (decimal) value.GetNumber() : null;
    }

    private static decimal Number(IXLCell cell)
    {
        return NumberOrNull(cell) ?? 0;
    }
}
