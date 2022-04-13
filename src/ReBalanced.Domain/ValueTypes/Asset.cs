namespace ReBalanced.Domain.ValueTypes;

public enum AssetType
{
    Stock,
    Crypto,
    Cash,
    Property
}

public record Asset(string Ticker, decimal Value, AssetType AssetType, DateTimeOffset Updated, bool Fractional = true,
    string EquivalentTicker = null!);