using ReBalanced.Application.Services.TaxAware;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Cli;

public enum SectionKind
{
    Taxable,
    RothIra,
    Retirement401K,
    Banks,
    Wallet,
    RealEstate
}

public record ImportedSection(SectionKind Kind, HoldingAccount Account);

public record ImportedPortfolio(
    IReadOnlyList<ImportedSection> Sections,
    decimal BankCash,
    decimal CryptoAllocation,
    decimal BitcoinMarketCap,
    decimal EthereumMarketCap,
    decimal CashTarget,
    bool IncludeCrypto,
    bool IncludeBanks,
    AllocationRatios? Ratios = null,
    IReadOnlyList<string>? Warnings = null);

public static class SectionKinds
{
    // only the brokerage accounts are traded; everything else is held as is and counted
    public static bool IsTradable(this SectionKind kind)
    {
        return kind is SectionKind.Taxable or SectionKind.RothIra;
    }

    public static AccountType ToAccountType(this SectionKind kind)
    {
        return kind switch
        {
            SectionKind.Taxable or SectionKind.Banks => AccountType.Taxable,
            SectionKind.RothIra or SectionKind.Retirement401K => AccountType.Roth,
            SectionKind.Wallet => AccountType.CryptoWallet,
            _ => AccountType.Property
        };
    }
}
