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
        var assetValue = await _assetRepository.GetValue(holding.Asset.Ticker);
        Guard.Against.Null(assetValue, nameof(assetValue));
        return assetValue.Value * holding.Quantity;
    }

    public async Task<decimal> TotalValue(Account account, bool includeFractional = true)
    {
        var sum = 0M;

        foreach (var holding in account.Holdings)
        {
            var asset = await _assetRepository.Get(holding.Asset.Ticker);
            Guard.Against.Null(asset, nameof(asset));
            var quantity = includeFractional || asset.AssetType == AssetType.Cash
                ? holding.Quantity
                : Math.Floor(holding.Quantity);
            sum += asset.Value * quantity;
        }

        return sum;
    }
}