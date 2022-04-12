﻿using Refit;

namespace ReBalanced.Infastructure.MBoum;

public interface IMBoumApi
{
    [Get("/qu/quote/?apikey={apikey}&symbol={symbols}")]
    Task<StockQuotes> GetStockQuotes(string apikey, string symbols);

    [Get("/cr/crypto/coin/quote/?apikey={apikey}&key={coin}")]
    Task<CoinQuote> GetCoinQuote(string apikey, string coin);
}