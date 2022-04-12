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
        return await _assetRepository.GetValue(holding.AssetTicker) * holding.Quantity;
    }

    public decimal TotalValue(IEnumerable<Holding> holdings)
    {
        return holdings.Sum(x => _assetRepository.GetValue(x.AssetTicker).Result * x.Quantity);
    }
}