using System.Globalization;
using ReBalanced.Cli;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine(CliOptions.Usage);
    return args.Length == 0 ? 1 : 0;
}

try
{
    var options = CliOptions.Parse(args);
    var portfolio = Path.GetExtension(options.Path).Equals(".json", StringComparison.OrdinalIgnoreCase)
        ? JsonImporter.Import(options.Path)
        : SpreadsheetImporter.Import(options.Path);
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
