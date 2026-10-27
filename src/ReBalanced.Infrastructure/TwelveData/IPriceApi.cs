namespace ReBalanced.Infrastructure.TwelveData;

public interface IPriceApi
{
    Task<IReadOnlyDictionary<string, decimal>> GetPrices(IReadOnlyCollection<string> symbols);
}
