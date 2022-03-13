using ReBalanced.Domain.Entities;

namespace ReBalanced.Application.Services.Interfaces;

public interface IAssetService
{
    decimal TotalValue(IEnumerable<Holding> holdings);
}