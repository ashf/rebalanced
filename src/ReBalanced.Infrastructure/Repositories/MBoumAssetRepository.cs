using Ardalis.GuardClauses;
using Microsoft.Extensions.Configuration;
using ReBalanced.Domain.Providers;
using ReBalanced.Domain.ValueTypes;
using ReBalanced.Infrastructure.Caching;
using ReBalanced.Infrastructure.MBoum;

namespace ReBalanced.Infrastructure.Repositories;

public class MBoumAssetRepository : IAssetRepository
{
    private readonly IAssetCache _assetCache;
    private readonly IConfiguration _configuration;
    private readonly IMBoumApi _mBoumApi;
    private readonly TimeSpan _staleTime = TimeSpan.FromHours(1);

    public MBoumAssetRepository(IConfiguration configuration, IMBoumApi mBoumApi, IAssetCache assetCache)
    {
        _configuration = configuration;
        _mBoumApi = mBoumApi;
        _assetCache = assetCache;
    }

    public async Task<Asset?> Get(string assetTicker)
    {
        await UpdateValues();
        return await _assetCache.Get(assetTicker);
    }

    public async Task<decimal?> GetValue(string assetTicker)
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
        if (DateTime.UtcNow - oldestUpdate > _staleTime)
        {
            await UpdateStocks();
            await UpdateCrpyto();
        }
    }

    public async Task PollAndUpsert(Asset asset)
    {
        var value = 1M;
        switch (asset)
        {
            case {AssetType: AssetType.Crypto}:
            {
                var quote = await GetCoinQuote(asset.Ticker);
                Guard.Against.Null(quote, nameof(quote));
                Guard.Against.Null(quote.Data, nameof(quote.Data));
                value = (decimal) quote.Data.Price;
                break;
            }
            case {AssetType: AssetType.Stock}:
            {
                var quote = await GetStockQuotes(asset.Ticker);
                Guard.Against.Null(quote, nameof(quote));
                Guard.Against.NullOrEmpty(quote.Data, nameof(quote.Data));
                value = (decimal) quote.Data[0].Ask;
                break;
            }
        }

        await _assetCache.Upsert(asset.Ticker, asset with
        {
            Value = value,
            Updated = DateTimeOffset.UtcNow
        });
    }

    private async Task UpdateStocks()
    {
        var assets = await _assetCache.Assets();
        var stockAssets = from asset in assets where asset.AssetType == AssetType.Stock select asset.Ticker;

        var stockList = string.Join(',', stockAssets);

        var stockQuotes = await GetStockQuotes(stockList);

        if (stockQuotes.Data is null) return;

        foreach (var quote in stockQuotes.Data)
        {
            var asset = await _assetCache.Get(quote.Symbol!);
            await _assetCache.Upsert(quote.Symbol!, asset! with
            {
                Value = (decimal) quote.Ask,
                Updated = DateTimeOffset.UtcNow
            });
        }
    }

    private async Task UpdateCrpyto()
    {
        var assets = await _assetCache.Assets();
        var crpytoAssets = from asset in assets where asset.AssetType == AssetType.Crypto select asset.Ticker;

        foreach (var crpytoAsset in crpytoAssets)
        {
            var cryptoQuote = await GetCoinQuote(crpytoAsset);

            if (cryptoQuote.Meta is null || cryptoQuote.Data is null) return;

            var asset = await _assetCache.Get(cryptoQuote.Meta.Key!);

            await _assetCache.Upsert(cryptoQuote.Meta.Key!, asset! with
            {
                Value = (decimal) cryptoQuote.Data.Price,
                Updated = DateTimeOffset.UtcNow
            });
        }
    }

    private async Task<CoinQuote> GetCoinQuote(string key)
    {
        return await _mBoumApi.GetCoinQuote(_configuration["MBOUM:API_KEY"], key);
    }

    private async Task<StockQuotes> GetStockQuotes(string symbols)
    {
        return await _mBoumApi.GetStockQuotes(_configuration["MBOUM:API_KEY"], symbols);
    }
}