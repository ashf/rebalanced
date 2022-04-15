namespace ReBalanced.Domain.ValueTypes;

public enum AssetType
{
    Stock,
    Crypto,
    Cash,
    Property
}

public record Asset(string Ticker, decimal Value, AssetType AssetType, DateTimeOffset Updated = default,
    string EquivalentTicker = null!);