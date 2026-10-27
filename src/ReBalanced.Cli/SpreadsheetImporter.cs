using ClosedXML.Excel;
using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Cli;

public enum SectionKind
{
    Taxable,
    RothIra,
    Retirement401K,
    Banks,
    Wallet,
    RealEstate
}

public record ImportedSection(string Title, SectionKind Kind, HoldingAccount Account);

public record ImportedPortfolio(
    IReadOnlyList<ImportedSection> Sections,
    decimal BankCash,
    decimal CryptoAllocation,
    decimal BitcoinMarketCap,
    decimal EthereumMarketCap,
    decimal CashTarget,
    bool IncludeCrypto,
    bool IncludeBanks);

// Reads the "investment rebalance" Google Sheet exported as .xlsx, using the values the sheet last calculated.
public static class SpreadsheetImporter
{
    private const int Label = 1, Price = 2, Quantity = 4, Value = 6;

    public static ImportedPortfolio Import(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet(1);

        var marketCap = Find(sheet, "Market Cap");
        var sections = ReadSections(sheet, Find(sheet, "Total Investment").Address.RowNumber + 1);

        return new ImportedPortfolio(
            sections.Where(x => x.Kind != SectionKind.Banks).ToList(),
            sections.Where(x => x.Kind == SectionKind.Banks).Sum(x => x.Account.Value),
            Number(Find(sheet, "crypto allocation").CellRight()),
            Number(sheet.Cell(marketCap.Address.RowNumber + 1, marketCap.Address.ColumnNumber + 1)),
            Number(sheet.Cell(marketCap.Address.RowNumber + 2, marketCap.Address.ColumnNumber + 1)),
            Number(sheet.Cell(FindInColumn(sheet, Label, "CASH").Address.RowNumber, 3)),
            Number(Find(sheet, "INCLUDE CRYPTO").CellRight()) != 0,
            Number(Find(sheet, "INCLUDE BANKS").CellRight()) != 0);
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
            var tradable = kind is SectionKind.Taxable or SectionKind.RothIra;
            sections.Add(new ImportedSection(title, kind,
                new HoldingAccount(name, AccountTypeOf(kind), tradable, positions)));
        }

        return sections;
    }

    private static Position? ReadPosition(IXLRow row, SectionKind kind, string section)
    {
        var label = Text(row.Cell(Label));
        if (label.Length == 0) return null;

        if (label.Equals("CASH", StringComparison.OrdinalIgnoreCase) || kind == SectionKind.Banks)
            return new Position("CASH", AssetClass.Cash, Number(row.Cell(Value)), 1);

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

    private static AccountType AccountTypeOf(SectionKind kind)
    {
        return kind switch
        {
            SectionKind.Taxable or SectionKind.Banks => AccountType.Taxable,
            SectionKind.RothIra or SectionKind.Retirement401K => AccountType.Roth,
            SectionKind.Wallet => AccountType.CryptoWallet,
            _ => AccountType.Property
        };
    }

    private static IXLCell Find(IXLWorksheet sheet, string text)
    {
        return sheet.CellsUsed().FirstOrDefault(x => Text(x).Equals(text, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"Cell \"{text}\" not found in the spreadsheet");
    }

    private static IXLCell FindInColumn(IXLWorksheet sheet, int column, string text)
    {
        return sheet.Column(column).CellsUsed().FirstOrDefault(x => Text(x) == text)
               ?? throw new InvalidOperationException($"\"{text}\" not found in column {column}");
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

    private static decimal Number(IXLCell cell)
    {
        var value = Read(cell);
        return value.IsNumber ? (decimal) value.GetNumber() : 0;
    }
}
