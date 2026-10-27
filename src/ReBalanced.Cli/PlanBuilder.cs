using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Cli;

public record PlanResult(
    RebalancePlan Plan,
    IReadOnlyList<HoldingAccount> Accounts,
    decimal BankTransfer,
    bool IncludeCrypto,
    bool IncludeCash,
    decimal BankCash = 0);

public static class PlanBuilder
{
    public static PlanResult Build(ImportedPortfolio portfolio, CliOptions options)
    {
        var includeCrypto = options.IncludeCrypto ?? portfolio.IncludeCrypto;
        var includeCash = options.IncludeCash ?? portfolio.IncludeBanks;
        var bankTransfer = includeCash ? Math.Max(0, portfolio.BankCash) : 0;

        var accounts = portfolio.Sections.Select(x => x.Account).Select(account =>
        {
            // ignored crypto is held as is, outside the total, like the sheet's INCLUDE CRYPTO = 0
            var positions = account.Positions
                .Where(x => includeCrypto || x.AssetClass is not (AssetClass.Bitcoin or AssetClass.Ethereum))
                .ToList();
            if (bankTransfer > 0 && account.Name == "Taxable")
                positions.Add(new Position("CASH", AssetClass.Cash, bankTransfer, 1));
            return account with {Positions = positions};
        }).ToList();

        var settings = new AllocationSettings(portfolio.CryptoAllocation, portfolio.BitcoinMarketCap,
            portfolio.EthereumMarketCap, options.CashTarget ?? portfolio.CashTarget, includeCrypto);
        var targets = AllocationTargets.Calculate(TaxAwareRebalancer.ValueByClass(accounts), settings);

        return new PlanResult(new TaxAwareRebalancer().Rebalance(accounts, targets, options.Band),
            accounts, bankTransfer, includeCrypto, includeCash, portfolio.BankCash);
    }
}
