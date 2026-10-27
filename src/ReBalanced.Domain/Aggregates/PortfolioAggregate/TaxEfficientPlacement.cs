namespace ReBalanced.Domain.Aggregates.PortfolioAggregate;

// Bogleheads tax-efficient fund placement (https://www.bogleheads.org/wiki/Tax-efficient_fund_placement):
// the least tax-efficient classes go in tax-advantaged accounts, international goes in taxable for the foreign tax
// credit, and tax-efficient US stocks fill whatever space is left. Crypto is not covered there; it goes in the Roth
// for its expected growth.
public static class TaxEfficientPlacement
{
    // the account types a class may be held in, most preferred first
    public static IReadOnlyList<AccountType> Homes(AssetClass assetClass)
    {
        return assetClass switch
        {
            AssetClass.Bonds or AssetClass.Reit or AssetClass.Bitcoin or AssetClass.Ethereum =>
                new[] {AccountType.Roth},
            AssetClass.DevelopedStocks or AssetClass.EmergingStocks => new[] {AccountType.Taxable},
            AssetClass.UsStocks => new[] {AccountType.Taxable, AccountType.Roth},
            AssetClass.Cash => new[] {AccountType.Taxable, AccountType.Roth},
            _ => Array.Empty<AccountType>()
        };
    }
}
