using ClosedXML.Excel;

namespace ReBalanced.Cli.Tests;

public class SpreadsheetImporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");

    public void Dispose()
    {
        File.Delete(_path);
    }

    private ImportedPortfolio Import(string? extraTaxableTicker = null, double extraTaxablePrice = 1,
        string? strayLabel = null)
    {
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            var headers = new[] {"Symbol", "Last", "Target", "Qty", "Target %", "Mkt Val"};
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(2, 1).Value = "VTI";
            sheet.Cell(3, 1).Value = "CASH";
            sheet.Cell(3, 3).Value = 500;
            sheet.Cell(4, 1).Value = "Total Investment";

            sheet.Cell("D6").Value = "crypto allocation";
            sheet.Cell("E6").Value = 0.1;
            sheet.Cell("D7").Value = "Market Cap";
            sheet.Cell("D8").Value = "bitcoin";
            sheet.Cell("E8").Value = 3;
            sheet.Cell("D9").Value = "ethereum";
            sheet.Cell("E9").Value = 1;
            sheet.Cell("G6").Value = "INCLUDE CRYPTO";
            sheet.Cell("H6").Value = 1;
            sheet.Cell("G7").Value = "INCLUDE BANKS";
            sheet.Cell("H7").Value = 0;

            var row = 11;
            void Section(string title, string? description) => Header(sheet, ref row, title, description);
            void Item(string label, double? price, double? qty, double value) =>
                Line(sheet, ref row, label, price, qty, value);
            void Total() => sheet.Cell(row++, 1).Value = "Total";

            Section("Indiviual Investment", "Taxable Acount");
            Item("VTI", 100, 10, 1000);
            Item("CASH", null, null, 50);
            if (extraTaxableTicker is not null) Item(extraTaxableTicker, extraTaxablePrice, 1, 1);
            Total();
            Section("Roth IRA", "Roth IRA");
            Item("GBTC", 40, 5, 200);
            Total();
            Section("401(k)", "TransAmerica Roth 401(k)");
            Item("TMBRX", 8, 100, 800);
            Total();
            Section("Banks", null);
            Item("Checking", null, null, 300);
            Item("Credit Card", null, null, -100);
            Total();
            Section("Bitcoin", "Bitcoin");
            Item("BTC (Trezor)", 50000, 0.1, 5000);
            Total();
            sheet.Cell(row, 1).Value = "Avg Buy Price";
            sheet.Cell(row++, 2).Value = 40000;
            if (strayLabel is not null) sheet.Cell(row++, 1).Value = strayLabel;
            Section("Woodside Health", "Woodside Health");
            Item("Palm Valley", 20000, 1, 20000);
            Total();

            workbook.SaveAs(_path);
        }

        return SpreadsheetImporter.Import(_path);
    }

    private static void Header(IXLWorksheet sheet, ref int row, string title, string? description)
    {
        sheet.Cell(row, 1).Value = title;
        if (description is not null) sheet.Cell(row, 2).Value = description;
        row++;
    }

    private static void Line(IXLWorksheet sheet, ref int row, string label, double? price, double? qty, double value)
    {
        sheet.Cell(row, 1).Value = label;
        if (price is not null) sheet.Cell(row, 2).Value = price.Value;
        if (qty is not null) sheet.Cell(row, 4).Value = qty.Value;
        sheet.Cell(row, 6).Value = value;
        row++;
    }

    [Fact]
    public void ImportReadsSectionKinds()
    {
        var portfolio = Import();

        var kinds = portfolio.Sections.Select(x => x.Kind).ToList();

        Assert.Equal(new[]
        {
            SectionKind.Taxable, SectionKind.RothIra, SectionKind.Retirement401K, SectionKind.Wallet,
            SectionKind.RealEstate
        }, kinds);
    }

    [Fact]
    public void ImportMarksTradableAccounts()
    {
        var portfolio = Import();

        var tradable = portfolio.Sections.Select(x => x.Account.Tradable).ToList();

        Assert.Equal(new[] {true, true, false, false, false}, tradable);
    }

    [Fact]
    public void ImportSumsBankCash()
    {
        var portfolio = Import();

        Assert.Equal(200, portfolio.BankCash);
    }

    [Fact]
    public void ImportReadsCashTarget()
    {
        var portfolio = Import();

        Assert.Equal(500, portfolio.CashTarget);
    }

    [Fact]
    public void ImportReadsIncludeCrypto()
    {
        var portfolio = Import();

        Assert.True(portfolio.IncludeCrypto);
    }

    [Fact]
    public void ImportReadsIncludeBanks()
    {
        var portfolio = Import();

        Assert.False(portfolio.IncludeBanks);
    }

    [Fact]
    public void ImportReadsCryptoSettings()
    {
        var portfolio = Import();

        Assert.Equal((0.1M, 3M, 1M), (portfolio.CryptoAllocation, portfolio.BitcoinMarketCap, portfolio.EthereumMarketCap));
    }

    [Fact]
    public void ImportMapsWalletToBitcoin()
    {
        var portfolio = Import();

        var position = portfolio.Sections.Single(x => x.Kind == SectionKind.Wallet).Account.Positions.Single();

        Assert.Equal(("bitcoin", Domain.Aggregates.PortfolioAggregate.AssetClass.Bitcoin, 0.1M, 50000M),
            (position.Ticker, position.AssetClass, position.Quantity, position.Price));
    }

    [Fact]
    public void ImportSkipsSummaryRows()
    {
        var portfolio = Import();

        Assert.DoesNotContain(portfolio.Sections, x => x.Title == "Avg Buy Price");
    }

    [Fact]
    public void ImportUnknownTickerThrows()
    {
        var ticker = "XYZ";

        void Act() => Import(ticker);

        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void ImportStrayLabelThrowsInsteadOfBecomingRealEstate()
    {
        var strayLabel = "cost";

        void Act() => Import(strayLabel: strayLabel);

        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void ImportUnpricedHoldingThrows()
    {
        var ticker = "VEA";

        void Act() => Import(ticker, 0);

        Assert.Throws<InvalidOperationException>(Act);
    }
}
