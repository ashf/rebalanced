namespace ReBalanced.Domain.ValueTypes;

public enum AssetType
{
    Stock,
    Crpyto,
    Cash,
    Property
}

public record Asset(string Ticker, decimal Value, AssetType AssetType, bool Fractional = true,
    string EquivalentTicker = null);