using Microsoft.Extensions.Configuration;
using ReBalanced.Domain.Providers;
using ReBalanced.Domain.ValueTypes;
using ReBalanced.Infastructure.MBoum;

namespace ReBalanced.Infrastructure.Repositories;

public class AssetRepository : IAssetRepository
{
    private readonly Dictionary<string, Asset> _assetCache = new();
    private readonly IConfiguration _configuration;
    private DateTime? _lastCacheRefresh;
    private readonly IMBoumApi _mBoumApi;
    private readonly TimeSpan _staleTime = TimeSpan.FromHours(1);

    public AssetRepository(IConfiguration configuration, IMBoumApi mBoumApi)
    {
        _configuration = configuration;
        _mBoumApi = mBoumApi; //RestService.For<IMBoumApi>("https://mboum.com/api/v1");
    }

    public async Task<Asset> Get(string assetTicker)
    {
        await UpdateValues();
        return _assetCache[assetTicker];
    }

    public async Task<decimal> GetValue(string assetTicker)
    {
        await UpdateValues();
        return _assetCache[assetTicker].Value;
    }

    public HashSet<string> GetAllTickers()
    {
        return _assetCache.Keys.ToHashSet();
    }

    public async Task UpdateValues()
    {
        if (_lastCacheRefresh is null || DateTime.UtcNow - _lastCacheRefresh.Value > _staleTime)
        {
            await UpdateStocks();
            await UpdateCrpyto();

            _lastCacheRefresh = DateTime.UtcNow;
        }
    }

    private async Task UpdateStocks()
    {
        var stockAssets = _assetCache
            .Where(x => x.Value.AssetType == AssetType.Stock)
            .Select(x => x.Key);

        var stockList = string.Join(',', stockAssets);

        var stockQuotes = await _mBoumApi.GetStockQuotes(_configuration["MBOUM:API_KEY"], stockList);

        if (stockQuotes.Data is null) return;

        foreach (var quote in stockQuotes.Data)
            if (_assetCache.ContainsKey(quote.Symbol!))
                _assetCache[quote.Symbol!] = _assetCache[quote.Symbol!] with {Value = (decimal) quote.Ask};
            else
                _assetCache[quote.Symbol!] = new Asset(quote.Symbol!, (decimal) quote.Ask, AssetType.Stock, false);
    }

    private async Task UpdateCrpyto()
    {
        var crpytoAssets = _assetCache
            .Where(x => x.Value.AssetType == AssetType.Crypto)
            .Select(x => x.Key);

        foreach (var crpytoAsset in crpytoAssets)
        {
            var cryptoQuote = await _mBoumApi.GetCoinQuote(_configuration["MBOUM:API_KEY"], crpytoAsset);

            if (cryptoQuote.Meta is null || cryptoQuote.Data is null) return;

            if (_assetCache.ContainsKey(cryptoQuote.Meta.Key!))
                _assetCache[cryptoQuote.Meta.Key!] = _assetCache[cryptoQuote.Meta.Key!] with
                {
                    Value = (decimal) cryptoQuote.Data.Price
                };
            else
                _assetCache[cryptoQuote.Meta.Key!] = new Asset(cryptoQuote.Meta.Key!, (decimal) cryptoQuote.Data.Price,
                    AssetType.Crypto);
        }
    }
}