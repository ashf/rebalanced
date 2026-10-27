using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace ReBalanced.Infrastructure.TwelveData;

public class TwelveDataApi : IPriceApi
{
    // each symbol costs a credit, and the free plan allows 8 credits a minute
    private const int MaxSymbolsPerRequest = 8;
    private static readonly TimeSpan RateLimitWait = TimeSpan.FromSeconds(61);

    private readonly Func<TimeSpan, Task> _delay;
    private readonly HttpClient _httpClient;

    [ActivatorUtilitiesConstructor]
    public TwelveDataApi(HttpClient httpClient) : this(httpClient, Task.Delay)
    {
    }

    public TwelveDataApi(HttpClient httpClient, Func<TimeSpan, Task> delay)
    {
        _httpClient = httpClient;
        _delay = delay;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetPrices(IReadOnlyCollection<string> symbols)
    {
        var prices = new Dictionary<string, decimal>();
        foreach (var batch in symbols.Chunk(MaxSymbolsPerRequest))
        foreach (var (symbol, price) in await GetBatch(batch))
            prices[symbol] = price;

        return prices;
    }

    // one symbol returns {"price":"1.0"}; several return {"SYM":{"price":"1.0"}, ...} with per-symbol errors inline
    private async Task<Dictionary<string, decimal>> GetBatch(IReadOnlyCollection<string> symbols)
    {
        var url = $"price?symbol={Uri.EscapeDataString(string.Join(',', symbols))}";
        using var response = await GetWithOneRateLimitRetry(url);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        if (root.TryGetProperty("status", out var status) && status.GetString() == "error")
            throw new HttpRequestException(
                $"Twelve Data error: {(root.TryGetProperty("message", out var message) ? message.GetString() : "unknown")}");

        var prices = new Dictionary<string, decimal>();
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

    // the per-minute credit limit answers with HTTP 429 or with {"code":429} in the body
    private async Task<HttpResponseMessage> GetWithOneRateLimitRetry(string url)
    {
        var response = await _httpClient.GetAsync(url);
        if (response.StatusCode != HttpStatusCode.TooManyRequests &&
            !(await response.Content.ReadAsStringAsync()).Contains("\"code\":429"))
            return response;

        response.Dispose();
        await _delay(RateLimitWait);
        return await _httpClient.GetAsync(url);
    }

    private static bool TryReadPrice(JsonElement quote, out decimal price)
    {
        price = 0;
        return quote.TryGetProperty("price", out var value) &&
               decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out price);
    }
}
