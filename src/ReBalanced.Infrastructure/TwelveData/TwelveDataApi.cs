using System.Globalization;
using System.Text.Json;

namespace ReBalanced.Infrastructure.TwelveData;

public class TwelveDataApi : IPriceApi
{
    private readonly HttpClient _httpClient;

    public TwelveDataApi(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // one symbol returns {"price":"1.0"}; several return {"SYM":{"price":"1.0"}, ...} with per-symbol errors inline
    public async Task<IReadOnlyDictionary<string, decimal>> GetPrices(IReadOnlyCollection<string> symbols)
    {
        var prices = new Dictionary<string, decimal>();
        if (symbols.Count == 0) return prices;

        using var response =
            await _httpClient.GetAsync($"price?symbol={Uri.EscapeDataString(string.Join(',', symbols))}");
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        if (root.TryGetProperty("status", out var status) && status.GetString() == "error")
            throw new HttpRequestException(
                $"Twelve Data error: {(root.TryGetProperty("message", out var message) ? message.GetString() : "unknown")}");

        if (symbols.Count == 1)
        {
            if (TryReadPrice(root, out var price)) prices[symbols.First()] = price;
            return prices;
        }

        var quotes = root.EnumerateObject().ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in symbols)
            if (quotes.TryGetValue(symbol, out var quote) && TryReadPrice(quote, out var price))
                prices[symbol] = price;

        return prices;
    }

    private static bool TryReadPrice(JsonElement quote, out decimal price)
    {
        price = 0;
        return quote.TryGetProperty("price", out var value) &&
               decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out price);
    }
}
