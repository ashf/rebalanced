using Ardalis.GuardClauses;
using Google.OrTools.LinearSolver;
using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.Entities.Aggregates;
using ReBalanced.Domain.Providers;

namespace ReBalanced.Application.Services;

internal record VariableWrapper(Variable Variable, string AccountName, string AssetName);

public class RebalanceService : IRebalanceService
{
    private const int MaxIterations = 100;
    private readonly IAssetRepository _assetRepository;
    private readonly IAssetService _assetService;

    public RebalanceService(
        IAssetService assetService,
        IAssetRepository assetRepository)
    {
        _assetService = assetService;
        _assetRepository = assetRepository;
    }

    public async Task<Dictionary<string, decimal>?> Rebalance(Portfolio portfolio)
    {
        var portfolioTotal = portfolio.Accounts.Values.Sum(account => _assetService.TotalValue(account.Holdings));

        var targetValuePerAsset =
            portfolio.Allocations.ToDictionary(x => x.Key, x => x.Value.Percentage * portfolioTotal);

        var tolerance = 0.00;

        var resultStatus = Solver.ResultStatus.NOT_SOLVED;
        Dictionary<string, decimal>? resultValues = default;
        double objectiveValue = 0;
        var iterations = 0;

        while ((resultStatus != Solver.ResultStatus.OPTIMAL || objectiveValue <= 0) && iterations <= MaxIterations)
        {
            var (solver, constraints, optimization) =
                SetupSystem(targetValuePerAsset, portfolio.Accounts.Values, tolerance);

            (resultStatus, resultValues) = SolveSystem(solver, constraints, optimization);

            objectiveValue = solver.Objective().Value();

            tolerance += 0.0125;
            iterations++;
        }

        // Check that the problem has an optimal solution.
        if (resultStatus == Solver.ResultStatus.OPTIMAL)
        {
            Console.WriteLine("Solution:");
            Console.WriteLine($"iterations = {iterations}");
            Console.WriteLine($"tolerance = {tolerance}");
            Console.WriteLine("Objective value = " + objectiveValue);
        }

        //foreach (var asset in targetValuePerAsset)
        //{
        //    decimal targetAmount = 0;
        //    foreach (var account in portfolio.Accounts)
        //    {
        //        if (!account.PermissibleAssets.Contains(asset.Key)) continue;

        //        var varName = GenerateVariableName(asset.Key, account.Name);
        //        targetAmount += resultValues[varName];
        //    }
        //}

        return resultValues;
    }

    private (Solver solver, IEnumerable<LinearConstraint>? constraints, LinearExpr? optimization)
        SetupSystem(
            IReadOnlyDictionary<string, decimal> targetValuePerAsset,
            IEnumerable<Account> accounts,
            double tolerance)
    {
        var solver = Solver.CreateSolver("SCIP");

        var variables = new Dictionary<string, VariableWrapper>();

        var constraints = new List<LinearConstraint>();

        var enumeratedAccounts = accounts.ToList();
        foreach (var account in enumeratedAccounts)
        foreach (var assetName in account.PermissibleAssets)
        {
            var asset = _assetRepository.Get(assetName);
            var varName = GenerateVariableName(assetName, account.Name);
            variables.Add(varName,
                !asset.Fractional
                    ? new VariableWrapper(solver.MakeIntVar(0.0, int.MaxValue, varName), account.Name,
                        assetName)
                    : new VariableWrapper(solver.MakeNumVar(0.0, double.MaxValue, varName), account.Name,
                        assetName));
        }

        // a * VTI + b * VXUS + c * VNQ + d * BND + cash1 * CASH == account1,
        foreach (var account in enumeratedAccounts)
        {
            LinearExpr? expr = null;
            foreach (var assetName in account.PermissibleAssets)
            {
                var asset = _assetRepository.Get(assetName);
                var localExpr = (double) _assetRepository.GetValue(asset.Ticker) *
                                variables[GenerateVariableName(asset.Ticker, account.Name)].Variable;
                if (expr is null) expr = localExpr;
                else expr += localExpr;
            }

            if (expr is not null) constraints.Add((double) _assetService.TotalValue(account.Holdings) == expr);
        }

        // a * VTI + e * VTI >= VTIAmount * (1 - tolerance)
        // a * VTI + e * VTI <= VTIAmount * (1 + tolerance)
        foreach (var assetName in targetValuePerAsset.Keys)
        {
            var asset = _assetRepository.Get(assetName);
            LinearExpr? expr = null;
            foreach (var account in enumeratedAccounts)
            {
                if (!account.PermissibleAssets.Contains(assetName))
                {
                    if (asset.EquivalentTicker is not null &&
                        !account.PermissibleAssets.Contains(asset.EquivalentTicker)) continue;
                    if (asset.EquivalentTicker is null) continue;
                }

                if (assetName == "CASH" && account.UndesiredAssets.Contains("CASH")) continue;

                var assetValue = (double) asset.Value;

                if (asset.EquivalentTicker is not null && account.PermissibleAssets.Contains(asset.EquivalentTicker))
                {
                    var equivAsset = _assetRepository.Get(asset.EquivalentTicker);
                    var localExpr = (double) equivAsset.Value / assetValue *
                                    variables[GenerateVariableName(equivAsset.Ticker, account.Name)].Variable;
                    if (expr is null) expr = localExpr;
                    else expr += localExpr;
                }
                else
                {
                    var localExpr = assetValue * variables[GenerateVariableName(assetName, account.Name)].Variable;
                    if (expr is null) expr = localExpr;
                    else expr += localExpr;
                }
            }

            if (expr is null) continue;
            constraints.Add(expr >= (double) targetValuePerAsset[assetName] * (1 - tolerance));
            constraints.Add(expr <= (double) targetValuePerAsset[assetName] * (1 + tolerance));
        }

        // b + g + h - cash1 - cash2;
        LinearExpr? optimization = null;
        foreach (var account in enumeratedAccounts)
        {
            foreach (var assetName in account.PriorityAssets)
            {
                var localExpr = variables[GenerateVariableName(assetName, account.Name)].Variable;
                if (optimization is null) optimization = localExpr;
                else optimization += localExpr;
            }

            foreach (var assetName in account.UndesiredAssets)
            {
                var localExpr = variables[GenerateVariableName(assetName, account.Name)].Variable;
                if (optimization is null) optimization = localExpr;
                else optimization -= localExpr;
            }
        }

        return (solver, constraints, optimization);
    }

    private static (Solver.ResultStatus, Dictionary<string, decimal>) SolveSystem(
        Solver solver, IEnumerable<LinearConstraint>? constraints, LinearExpr? optimization)
    {
        Guard.Against.Null(constraints, nameof(constraints));
        Guard.Against.Null(optimization, nameof(optimization));

        foreach (var constraint in constraints) solver.Add(constraint);

        solver.Maximize(optimization);

        var resultStatus = solver.Solve();

        var resultValues = solver.variables().ToDictionary(x => x.Name(), x => (decimal) x.SolutionValue());

        return (resultStatus, resultValues);
    }

    private static string GenerateVariableName(string assetName, string accountName)
    {
        return $"{assetName}_{accountName}";
    }
}