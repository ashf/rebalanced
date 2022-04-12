using Refit;

namespace ReBalanced.Infastructure.MBoum;

public interface IMBoumApi
{
    [Get("/qu/quote/?symbol={symbols}")]
    Task<StockQuotes> GetStockQuotes(string symbols);

    [Get("/cr/crypto/coin/quote={coin}")]
    Task<CoinQuote> GetCoinQuote(string coin);
}