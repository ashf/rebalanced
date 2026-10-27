using Google.OrTools.LinearSolver;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;

namespace ReBalanced.Application.Services.TaxAware;

// Dollar-based rebalancing across accounts as a linear program. Each class may only be held in its tax-efficient
// homes (TaxEfficientPlacement), even if that takes a taxable sale. Drift beyond the band is fixed first, drift inside
// it only where that is cheap (cash and tax-advantaged trades), and selling in a taxable account costs the most.
public class TaxAwareRebalancer
{
    private const double OutsideBandPenalty = 1000;
    private const double InsideBandPenalty = 1;
    private const double TaxableSellPenalty = 3;
    private const double TurnoverPenalty = 0.01;
    private const double LocationWeight = 0.2;
    private const double NonPreferredBuyPenalty = 0.001;

    public RebalancePlan Rebalance(IReadOnlyList<HoldingAccount> accounts,
        IReadOnlyDictionary<AssetClass, decimal> targets, decimal band)
    {
        var total = accounts.Sum(x => x.Value);
        var prices = accounts.SelectMany(x => x.Positions).Where(x => x.Price > 0)
            .GroupBy(x => x.Ticker).ToDictionary(x => x.Key, x => x.First().Price);

        using var solver = Solver.CreateSolver("GLOP");
        var objective = solver.Objective();
        objective.SetMinimization();

        var fixedByClass = new Dictionary<AssetClass, double>();
        var holdings = new List<(HoldingAccount Account, string Ticker, AssetClass AssetClass, decimal Current, Variable Final)>();

        var warnings = new List<string>();
        var targeted = targets.Where(x => x.Key != AssetClass.RealEstate && x.Value > 0).Select(x => x.Key).ToList();
        var tradable = accounts.Where(x => x.Tradable).ToList();
        // a class whose home account type is missing from the portfolio may go in any traded account
        var homeless = targeted.Where(x => !tradable.Any(a => TaxEfficientPlacement.Homes(x).Contains(a.AccountType)))
            .ToHashSet();
        foreach (var assetClass in homeless)
            warnings.Add($"No traded account is a tax-efficient home for {assetClass}, so any account may hold it");

        bool Allowed(HoldingAccount account, AssetClass assetClass) =>
            homeless.Contains(assetClass) || TaxEfficientPlacement.Homes(assetClass).Contains(account.AccountType);

        foreach (var account in accounts)
        {
            if (!account.Tradable)
            {
                foreach (var position in account.Positions)
                    fixedByClass[position.AssetClass] =
                        fixedByClass.GetValueOrDefault(position.AssetClass) + (double) position.Value;
                continue;
            }

            var current = account.Positions.GroupBy(x => x.Ticker)
                .ToDictionary(x => x.Key, x => (x.First().AssetClass, Value: x.Sum(p => p.Value)));
            foreach (var assetClass in targeted.Union(new[] {AssetClass.Cash}).Where(x => Allowed(account, x)))
            {
                var ticker = AssetClasses.PreferredTicker(assetClass);
                if (ticker is not null) current.TryAdd(ticker, (assetClass, 0M));
            }

            var accountTotal = solver.MakeConstraint((double) account.Value, (double) account.Value);

            foreach (var (ticker, (assetClass, value)) in current)
            {
                // a holding outside its homes must be sold
                var final = solver.MakeNumVar(0, Allowed(account, assetClass) ? double.PositiveInfinity : 0,
                    $"{account.Name}:{ticker}");
                var buy = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var sell = solver.MakeNumVar(0, double.PositiveInfinity, "");
                var balance = solver.MakeConstraint((double) value, (double) value);
                balance.SetCoefficient(final, 1);
                balance.SetCoefficient(buy, -1);
                balance.SetCoefficient(sell, 1);
                accountTotal.SetCoefficient(final, 1);

                // spending or receiving cash is the other side of a trade, not a trade
                if (assetClass != AssetClass.Cash)
                {
                    objective.SetCoefficient(buy, TurnoverPenalty +
                                                  (ticker == AssetClasses.PreferredTicker(assetClass) ? 0 : NonPreferredBuyPenalty));
                    objective.SetCoefficient(sell,
                        TurnoverPenalty + (account.AccountType == AccountType.Taxable ? TaxableSellPenalty : 0));
                }
                // within its allowed homes, a class leans to the first one (e.g. cash in taxable rather than the Roth)
                var homes = TaxEfficientPlacement.Homes(assetClass);
                objective.SetCoefficient(final, homes.Count > 1 && homes[0] != account.AccountType ? LocationWeight : 0);

                holdings.Add((account, ticker, assetClass, value, final));
            }
        }

        var bandDollars = (double) (band * total);
        foreach (var assetClass in targets.Keys.Union(fixedByClass.Keys).Union(holdings.Select(x => x.AssetClass)))
        {
            // class value + under - over = target, with the first bandDollars of each side cheap
            var deviation = solver.MakeConstraint(
                (double) targets.GetValueOrDefault(assetClass) - fixedByClass.GetValueOrDefault(assetClass),
                (double) targets.GetValueOrDefault(assetClass) - fixedByClass.GetValueOrDefault(assetClass));
            foreach (var holding in holdings.Where(x => x.AssetClass == assetClass))
                deviation.SetCoefficient(holding.Final, 1);

            foreach (var (sign, upper, penalty) in new[]
                     {
                         (1, bandDollars, InsideBandPenalty), (-1, bandDollars, InsideBandPenalty),
                         (1, double.PositiveInfinity, OutsideBandPenalty), (-1, double.PositiveInfinity, OutsideBandPenalty)
                     })
            {
                var slack = solver.MakeNumVar(0, upper, "");
                deviation.SetCoefficient(slack, sign);
                objective.SetCoefficient(slack, penalty);
            }
        }

        var status = solver.Solve();
        if (status != Solver.ResultStatus.OPTIMAL)
            throw new InvalidOperationException($"No optimal rebalance found (status {status})");

        var trades = holdings
            .Where(x => x.AssetClass != AssetClass.Cash)
            .Select(x => new Trade(x.Account.Name, x.Ticker,
                Math.Round((decimal) x.Final.SolutionValue() - x.Current, 2), prices.GetValueOrDefault(x.Ticker)))
            .Where(x => Math.Abs(x.Amount) >= 0.01M)
            .ToList();

        var after = fixedByClass.ToDictionary(x => x.Key, x => (decimal) x.Value);
        foreach (var holding in holdings)
            after[holding.AssetClass] =
                after.GetValueOrDefault(holding.AssetClass) + (decimal) holding.Final.SolutionValue();

        foreach (var (assetClass, target) in targets)
        {
            var miss = after.GetValueOrDefault(assetClass) - target;
            if (Math.Abs(miss) > Math.Max(band * total, 1))
                warnings.Add($"{assetClass} ends {Math.Abs(miss):C} {(miss > 0 ? "over" : "under")} its target: " +
                             "held accounts and placement rules leave no trade that closes the gap");
        }

        return new RebalancePlan(trades, ValueByClass(accounts), after, targets, total, warnings);
    }

    public static Dictionary<AssetClass, decimal> ValueByClass(IEnumerable<HoldingAccount> accounts)
    {
        return accounts.SelectMany(x => x.Positions)
            .GroupBy(x => x.AssetClass)
            .ToDictionary(x => x.Key, x => x.Sum(p => p.Value));
    }
}
