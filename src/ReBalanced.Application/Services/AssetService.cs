using Ardalis.GuardClauses;
using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Application.Services;

public class AssetService : IAssetService
{
    private readonly IAssetRepository _assetRepository;

    public AssetService(IAssetRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }

    public async Task<decimal> Value(Holding holding)
    {
        var assetValue = await _assetRepository.GetValue(holding.AssetTicker);
        Guard.Against.Null(assetValue, nameof(assetValue));
        return assetValue.Value * holding.Quantity;
    }

    public decimal TotalValue(Account account, bool includeFractional = true)
    {
        return account.Holdings.Sum(x =>
        {
            var asset = _assetRepository.Get(x.AssetTicker).Result;
            Guard.Against.Null(asset, nameof(asset));
            var quantity = (includeFractional || (asset.AssetType == AssetType.Cash)) ? x.Quantity : Math.Floor(x.Quantity);
            return asset.Value * quantity;
        });
    }
}