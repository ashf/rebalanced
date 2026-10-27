using System.Text.Json;
using System.Text.Json.Serialization;
using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Cli;

public record JsonBalances(JsonSettings? Settings, List<JsonAccount> Accounts);

public record JsonSettings(
    bool IncludeCrypto = false,
    bool IncludeBanks = false,
    decimal CashTarget = 0,
    decimal CryptoAllocation = 0.1M,
    decimal BitcoinMarketCap = 0,
    decimal EthereumMarketCap = 0,
    JsonRatios? Ratios = null);

public record JsonRatios(decimal? UsShare, decimal? DevelopedShare, decimal? BondShare, decimal? MinimumRealEstate);

public record JsonAccount(string Name, SectionKind Kind, List<JsonHolding> Holdings);

// a security is ticker + shares + price; cash, bank and property lines are a value
public record JsonHolding(string? Ticker, string? Name, decimal? Shares, decimal? Price, decimal? Value);

// Reads current balances from a JSON file (see examples/balances.example.json) as an alternative to the spreadsheet.
public static class JsonImporter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = {new JsonStringEnumConverter(allowIntegerValues: false)},
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ImportedPortfolio Import(string path)
    {
        JsonBalances balances;
        try
        {
            balances = JsonSerializer.Deserialize<JsonBalances>(File.ReadAllText(path), Options)
                       ?? throw new InvalidOperationException($"{path} is empty");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{path} is not valid balances JSON: {ex.Message}", ex);
        }

        var settings = balances.Settings ?? new JsonSettings();
        var accounts = balances.Accounts ?? throw new InvalidOperationException($"{path} has no \"accounts\"");
        var defaults = AllocationRatios.Default;

        var sections = accounts.Select((account, index) =>
        {
            if (account is null || string.IsNullOrWhiteSpace(account.Name))
                throw new InvalidOperationException($"Account {index + 1} in {path} needs a name");

            var positions = (account.Holdings ?? new List<JsonHolding>())
                .Select(x => ToPosition(account, x ?? throw new InvalidOperationException(
                    $"{account.Name} has an empty holding")))
                .Where(x => x.Value != 0)
                .ToList();
            return new ImportedSection(account.Kind,
                new HoldingAccount(account.Name, account.Kind.ToAccountType(), account.Kind.IsTradable(), positions));
        }).ToList();

        return new ImportedPortfolio(
            sections.Where(x => x.Kind != SectionKind.Banks).ToList(),
            sections.Where(x => x.Kind == SectionKind.Banks).Sum(x => x.Account.Value),
            settings.CryptoAllocation,
            settings.BitcoinMarketCap,
            settings.EthereumMarketCap,
            settings.CashTarget,
            settings.IncludeCrypto,
            settings.IncludeBanks,
            new AllocationRatios(
                settings.Ratios?.UsShare ?? defaults.UsShare,
                settings.Ratios?.DevelopedShare ?? defaults.DevelopedShareOfInternational,
                settings.Ratios?.BondShare ?? defaults.BondShare,
                settings.Ratios?.MinimumRealEstate ?? defaults.MinimumRealEstate));
    }

    private static Position ToPosition(JsonAccount account, JsonHolding holding)
    {
        var label = holding.Ticker ?? holding.Name ?? "(unnamed)";
        string Where() => $"{label} in {account.Name}";
        decimal Value() => holding.Value ?? holding.Shares * holding.Price ??
            throw new InvalidOperationException($"{Where()} needs a value, or shares and a price");

        if (account.Kind == SectionKind.Banks ||
            string.Equals(holding.Ticker, AssetClasses.CashTicker, StringComparison.OrdinalIgnoreCase))
            return Position.Cash(Value());
        if (account.Kind == SectionKind.RealEstate)
            return Value() is var value and >= 0
                ? new Position(label, AssetClass.RealEstate, 1, value)
                : throw new InvalidOperationException($"{Where()} has a negative value");

        var ticker = holding.Ticker ?? throw new InvalidOperationException($"{Where()} needs a ticker");
        var assetClass = AssetClasses.Of(ticker) ?? throw new InvalidOperationException(
            $"Unknown ticker {ticker} in {account.Name}; add it to AssetClasses");
        if (holding.Shares is not >= 0 || holding.Price is not > 0)
            throw new InvalidOperationException($"{Where()} needs non-negative shares and a positive price");

        // a wallet holds the coin itself, priced as BTC/USD or ETH/USD, whichever ticker spells it
        if (account.Kind == SectionKind.Wallet)
            ticker = assetClass switch
            {
                AssetClass.Bitcoin => "bitcoin",
                AssetClass.Ethereum => "ethereum",
                _ => throw new InvalidOperationException($"{Where()}: a wallet can only hold bitcoin or ethereum")
            };

        return new Position(ticker, assetClass, holding.Shares.Value, holding.Price.Value);
    }
}
