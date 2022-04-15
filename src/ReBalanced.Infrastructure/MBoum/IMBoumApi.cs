namespace ReBalanced.Infrastructure.MBoum;

public interface IMBoumApi
{
    Task<StockQuotes> GetStockQuotes(string symbols);
    Task<CoinQuote> GetCoinQuote(string coin);
}