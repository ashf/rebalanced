using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.Providers;

namespace ReBalanced.Application.Services;

public class AssetService : IAssetService
{
    private readonly IAssetRepository _assetRepository;

    public AssetService(IAssetRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }

    public decimal TotalValue(IEnumerable<Holding> holdings)
    {
        return holdings.Sum(x => _assetRepository.GetValue(x.AssetTicker) * x.Quantity);
    }
}