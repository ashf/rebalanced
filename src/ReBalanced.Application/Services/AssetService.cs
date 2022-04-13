using Ardalis.GuardClauses;
using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;

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

    public decimal TotalValue(IEnumerable<Holding> holdings)
    {
        return holdings.Sum(x =>
        {
            var assetValue = _assetRepository.GetValue(x.AssetTicker).Result;
            Guard.Against.Null(assetValue, nameof(assetValue));
            return assetValue.Value * x.Quantity;
        });
    }
}