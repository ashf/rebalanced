using Microsoft.Extensions.Options;

namespace ReBalanced.Infrastructure.MBoum;

public class MBoumApiOptions
{
    public const string MBoum = "MBoum";
    public string ApiKey { get; set; } = string.Empty;
}

public class MBoumApi : IMBoumApi
{
    private readonly MBoumApiOptions _options;
    private readonly IRefitMBoumApi _refitMBoumApi;

    public MBoumApi(IRefitMBoumApi refitMBoumApi, IOptions<MBoumApiOptions> options)
    {
        _refitMBoumApi = refitMBoumApi;
        _options = options.Value;
    }

    public async Task<StockQuotes> GetStockQuotes(string? symbols)
    {
        return await _refitMBoumApi.GetStockQuotes(_options.ApiKey, symbols);
    }

    public async Task<CoinQuote> GetCoinQuote(string? coin)
    {
        return await _refitMBoumApi.GetCoinQuote(_options.ApiKey, coin);
    }
}