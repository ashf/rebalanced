using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using ReBalanced.Domain.Providers;
using ReBalanced.Domain.ValueTypes;
using ReBalanced.Infrastructure.Caching;
using ReBalanced.Infrastructure.TwelveData;

namespace ReBalanced.Infrastructure.Repositories;

public class TwelveDataAssetRepository : IAssetRepository
{
    // Twelve Data quotes crypto as currency pairs
    private static readonly Dictionary<string, string> CryptoSymbols = new()
    {
        ["bitcoin"] = "BTC/USD",
        ["ethereum"] = "ETH/USD"
    };

    private readonly IAssetCache _assetCache;
    private readonly ILogger<TwelveDataAssetRepository> _logger;
    private readonly IPriceApi _priceApi;
    private readonly TimeSpan _staleTime = TimeSpan.FromHours(1);

    public TwelveDataAssetRepository(IPriceApi priceApi, IAssetCache assetCache,
        ILogger<TwelveDataAssetRepository> logger)
    {
        _priceApi = priceApi;
        _assetCache = assetCache;
        _logger = logger;
    }

    public async Task SeedCache(Asset asset)
    {
        if (await _assetCache.ContainsAsset(asset.Ticker)) return;

        await _assetCache.Upsert(asset.Ticker, asset);
    }

    public async Task SeedCache(IEnumerable<Asset> assets)
    {
        foreach (var asset in assets) await SeedCache(asset);
    }

    public async Task<Asset?> Get(string? assetTicker)
    {
        await UpdateValues();
        return await _assetCache.Get(assetTicker);
    }

    public async Task<decimal?> GetValue(string? assetTicker)
    {
        await UpdateValues();
        return (await _assetCache.Get(assetTicker))?.Value;
    }

    public async Task<HashSet<string>> GetAllTickers()
    {
        return (await _assetCache.Assets()).Select(x => x.Ticker).ToHashSet();
    }

    public async Task UpdateValues()
    {
        var oldestUpdate = await _assetCache.OldestCacheItem();
        if (oldestUpdate.HasValue && DateTime.UtcNow - oldestUpdate <= _staleTime) return;

        var symbols = new Dictionary<string, Asset>();
        foreach (var asset in await _assetCache.Assets())
        {
            if (asset.AssetType is not (AssetType.Stock or AssetType.Crypto)) continue;

            var symbol = PriceSymbol(asset);
            if (symbol is null)
            {
                _logger.LogWarning("No Twelve Data symbol for {Ticker}", asset.Ticker);
                await _assetCache.Upsert(asset.Ticker, asset with {Updated = DateTime.UtcNow});
                continue;
            }

            symbols[symbol] = asset;
        }

        var prices = await _priceApi.GetPrices(symbols.Keys);

        foreach (var (symbol, asset) in symbols)
        {
            // keep the last known price but mark it checked, so one missing symbol cannot force a refetch on every read
            if (!prices.TryGetValue(symbol, out var price))
            {
                _logger.LogWarning("No Twelve Data price for {Symbol}; keeping {Value}", symbol, asset.Value);
                price = asset.Value;
            }

            await _assetCache.Upsert(asset.Ticker, asset with
            {
                Value = price,
                Updated = DateTime.UtcNow
            });
        }
    }

    public async Task PollAndUpsert(Asset asset)
    {
        var value = 1M;
        var symbol = PriceSymbol(asset);
        if (symbol is not null)
        {
            var prices = await _priceApi.GetPrices(new[] {symbol});
            Guard.Against.InvalidInput(prices, nameof(prices), x => x.ContainsKey(symbol), $"No price for {symbol}");
            value = prices[symbol];
        }

        await _assetCache.Upsert(asset.Ticker, asset with
        {
            Value = value,
            Updated = DateTime.UtcNow
        });
    }

    private static string? PriceSymbol(Asset asset)
    {
        return asset.AssetType switch
        {
            AssetType.Stock => asset.Ticker,
            AssetType.Crypto => CryptoSymbols.GetValueOrDefault(asset.Ticker),
            _ => null
        };
    }
}
