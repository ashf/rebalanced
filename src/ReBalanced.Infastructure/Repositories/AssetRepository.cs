using Microsoft.Extensions.Configuration;
using ReBalanced.Domain.Providers;
using ReBalanced.Domain.ValueTypes;
using ReBalanced.Infastructure.MBoum;
using Refit;

namespace ReBalanced.Infrastructure.Repositories;

public class AssetRepository : IAssetRepository
{
    private readonly Dictionary<string, Asset> _assetCache = new();
    private readonly IConfiguration _configuration;
    private IMBoumApi _mBoumApi;
    private DateTime? _lastCacheRefresh;
    private TimeSpan _staleTime = TimeSpan.FromHours(1);

    public AssetRepository(IConfiguration configuration, IMBoumApi mBoumApi)
    {
        _configuration = configuration;
        _mBoumApi = mBoumApi;//RestService.For<IMBoumApi>("https://mboum.com/api/v1");
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
        if ((_lastCacheRefresh is null) || ((DateTime.UtcNow - _lastCacheRefresh.Value) > _staleTime))
        {
            await UpdateStocks();
            await UpdateCrpyto();
        }
    }
    
    private async Task UpdateStocks()
    {
        var stockAssets = _assetCache
            .Where(x => x.Value.AssetType == AssetType.Stock)
            .Select(x => x.Key);
            
        var stockList = string.Join(',', stockAssets);

        var stockQuotes = await _mBoumApi.GetStockQuotes(_configuration["MBOUM:API_KEY"],stockList);
            
        foreach (var quote in stockQuotes.Data)
        {
            if (_assetCache.ContainsKey(quote.Symbol))
            {
                _assetCache[quote.Symbol] = _assetCache[quote.Symbol] with { Value = (decimal) quote.Ask };
            }
            else
            {
                _assetCache[quote.Symbol] = new Asset(quote.Symbol, (decimal) quote.Ask, AssetType.Stock, false);
            }
        }
    }
    
    private async Task UpdateCrpyto()
    {
        var crpytoAssets = _assetCache
            .Where(x => x.Value.AssetType == AssetType.Crypto)
            .Select(x => x.Key);

        foreach (var crpytoAsset in crpytoAssets)
        {
            var cryptoAsset = await _mBoumApi.GetCoinQuote(_configuration["MBOUM:API_KEY"],crpytoAsset);
                
            if (_assetCache.ContainsKey(cryptoAsset.Meta.Key))
            {
                _assetCache[cryptoAsset.Meta.Key] = _assetCache[cryptoAsset.Meta.Key] with { Value = (decimal) cryptoAsset.Data.Price };
            }
            else
            {
                _assetCache[cryptoAsset.Meta.Key] = new Asset(cryptoAsset.Meta.Key, (decimal) cryptoAsset.Data.Price, AssetType.Crypto);
            }
        }
    }
}