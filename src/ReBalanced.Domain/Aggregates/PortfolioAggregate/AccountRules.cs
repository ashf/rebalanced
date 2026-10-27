namespace ReBalanced.Domain.Aggregates.PortfolioAggregate;

// asset location: tickers each account type should hold first, and those it should avoid
public static class AccountRules
{
    public static HashSet<string> PriorityAssets(AccountType accountType)
    {
        return accountType switch
        {
            AccountType.Taxable => new HashSet<string> {"VEA", "VWO"},
            AccountType.Roth => new HashSet<string> {"VNQ", "BND", "GBTC", "ETHE"},
            AccountType.CryptoWallet => new HashSet<string> {"bitcoin", "ethereum"},
            AccountType.Property => new HashSet<string> {"PROPERTY"},
            _ => throw new NotImplementedException()
        };
    }

    public static HashSet<string> UndesiredAssets(AccountType accountType)
    {
        return accountType switch
        {
            AccountType.Taxable => new HashSet<string> {"GBTC", "ETHE"},
            AccountType.Roth => new HashSet<string> {"CASH"},
            AccountType.CryptoWallet => new HashSet<string> {"CASH"},
            AccountType.Property => new HashSet<string> {"CASH"},
            _ => throw new NotImplementedException()
        };
    }
}
