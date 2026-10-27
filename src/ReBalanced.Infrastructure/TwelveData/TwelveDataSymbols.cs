namespace ReBalanced.Infrastructure.TwelveData;

public static class TwelveDataSymbols
{
    // Twelve Data quotes crypto as currency pairs
    private static readonly Dictionary<string, string> CryptoPairs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bitcoin"] = "BTC/USD",
        ["ethereum"] = "ETH/USD"
    };

    public static string? CryptoPair(string coin)
    {
        return CryptoPairs.GetValueOrDefault(coin);
    }

    // a coin's currency pair, otherwise the ticker itself
    public static string For(string ticker)
    {
        return CryptoPair(ticker) ?? ticker;
    }
}
