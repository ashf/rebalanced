using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Cli;

public record PlanResult(
    RebalancePlan Plan,
    IReadOnlyList<HoldingAccount> Accounts,
    AllocationSettings Settings,
    bool IncludeCash,
    decimal BankCash,
    decimal BankTransfer,
    string? TransferAccount);

public static class PlanBuilder
{
    public static PlanResult Build(ImportedPortfolio portfolio, CliOptions options)
    {
        var includeCrypto = options.IncludeCrypto ?? portfolio.IncludeCrypto;
        var includeCash = options.IncludeCash ?? portfolio.IncludeBanks;
        // bank cash moves into the (first) taxable account, whatever it is named
        var transferTo = portfolio.Sections.FirstOrDefault(x => x.Kind == SectionKind.Taxable)?.Account;
        var bankTransfer = includeCash && transferTo is not null ? Math.Max(0, portfolio.BankCash) : 0;

        var accounts = portfolio.Sections.Select(x => x.Account).Select(account =>
        {
            // ignored crypto is held as is, outside the total, like the sheet's INCLUDE CRYPTO = 0
            var positions = account.Positions
                .Where(x => includeCrypto || x.AssetClass is not (AssetClass.Bitcoin or AssetClass.Ethereum))
                .ToList();
            if (bankTransfer > 0 && ReferenceEquals(account, transferTo))
                positions.Add(Position.Cash(bankTransfer));
            return account with {Positions = positions};
        }).ToList();

        var sheetRatios = portfolio.Ratios ?? AllocationRatios.Default;
        var ratios = new AllocationRatios(
            options.UsShare ?? sheetRatios.UsShare,
            options.DevelopedShare ?? sheetRatios.DevelopedShareOfInternational,
            options.BondShare ?? sheetRatios.BondShare,
            options.MinimumRealEstate ?? sheetRatios.MinimumRealEstate);
        var settings = new AllocationSettings(options.CryptoAllocation ?? portfolio.CryptoAllocation,
            portfolio.BitcoinMarketCap, portfolio.EthereumMarketCap, options.CashTarget ?? portfolio.CashTarget,
            includeCrypto, ratios);
        var targets = AllocationTargets.Calculate(TaxAwareRebalancer.ValueByClass(accounts), settings);

        return new PlanResult(new TaxAwareRebalancer().Rebalance(accounts, targets, options.Band),
            accounts, settings, includeCash, portfolio.BankCash, bankTransfer, transferTo?.Name);
    }
}
