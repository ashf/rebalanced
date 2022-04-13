using Ardalis.GuardClauses;
using Google.OrTools.LinearSolver;
using Microsoft.Extensions.Logging;
using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Application.Services;

public class RebalanceService : IRebalanceService
{
    private const int MaxIterations = 100;
    private readonly IAssetRepository _assetRepository;
    private readonly IAssetService _assetService;
    private readonly ILogger _logger;

    public RebalanceService(
        IAssetService assetService,
        IAssetRepository assetRepository,
        ILogger logger)
    {
        _assetService = assetService;
        _assetRepository = assetRepository;
        _logger = logger;
    }

    public async Task<Dictionary<string, decimal>> Rebalance(Portfolio portfolio)
    {
        var portfolioTotal = portfolio.Accounts.Values.Sum(account => _assetService.TotalValue(account.Holdings));

        var targetValuePerAsset =
            portfolio.Allocations.ToDictionary(x => x.Key, x => x.Value.Percentage * portfolioTotal);

        var tolerance = 0.00;

        var resultStatus = Solver.ResultStatus.NOT_SOLVED;
        var resultValues = new Dictionary<string, decimal>();
        
        double objectiveValue = 0;
        var iterations = 0;

        while ((resultStatus != Solver.ResultStatus.OPTIMAL || objectiveValue <= 0) && iterations <= MaxIterations)
        {
            var (solver, constraints, optimization) =
                await SetupSystem(targetValuePerAsset, portfolio.Accounts.Values, tolerance);

            (resultStatus, resultValues) = SolveSystem(solver, constraints, optimization);

            objectiveValue = solver.Objective().Value();

            tolerance += 0.0125;
            iterations++;
        }

        // Check that the problem has an optimal solution.
        if (resultStatus == Solver.ResultStatus.OPTIMAL)
        {
            _logger.LogDebug("Solution:");
            _logger.LogDebug("iterations = {iterations}", iterations);
            _logger.LogDebug("tolerance = {tolerance}", tolerance);
            _logger.LogDebug("Objective value = {objectiveValue}", objectiveValue);
        }

        return resultValues;
    }

    private async Task<(Solver solver, List<LinearConstraint> constraints, LinearExpr? optimization)>
        SetupSystem(
            IReadOnlyDictionary<string, decimal> targetValuePerAsset,
            ICollection<Account> accounts,
            double tolerance)
    {
        var solver = Solver.CreateSolver("SCIP");

        var variables = await PopulateVariables(accounts, solver);

        var constraints = new List<LinearConstraint>();
        constraints.AddRange(await GeneratePermissibleAssetsConstraints(accounts, variables));
        constraints.AddRange(await GenerateTargetAmountConstraints(targetValuePerAsset, accounts, tolerance, variables));

        var optimization = GenerateOptimization(accounts, variables);

        return (solver, constraints, optimization);
    }

    private async Task<Dictionary<string, Variable>> PopulateVariables(IEnumerable<Account> accounts, Solver solver)
    {
        var variables = new Dictionary<string, Variable>();
        
        foreach (var account in accounts)
        {
            foreach (var assetName in account.PermissibleAssets)
            {
                var asset = await _assetRepository.Get(assetName);
                Guard.Against.Null(asset, nameof(asset));

                var varName = GenerateVariableName(assetName, account.Name);
                variables.Add(varName,
                    !account.AllowFractional || (asset.AssetType != AssetType.Cash)
                        ? solver.MakeIntVar(0.0, int.MaxValue, varName)
                        : solver.MakeNumVar(0.0, double.MaxValue, varName));
            }
        }

        return variables;
    }

    // a * VTI + b * VXUS + c * VNQ + d * BND + cash1 * CASH == account1
    private async Task<List<LinearConstraint>> GeneratePermissibleAssetsConstraints(
        IEnumerable<Account> accounts, IReadOnlyDictionary<string, Variable> variables)
    {
        var constraints = new List<LinearConstraint>();
        
        foreach (var account in accounts)
        {
            LinearExpr? expr = null;
            foreach (var assetName in account.PermissibleAssets)
            {
                var asset = await _assetRepository.Get(assetName);
                Guard.Against.Null(asset, nameof(asset));

                var localExpr = (double) asset.Value * variables[GenerateVariableName(asset.Ticker, account.Name)];
                AddExpr(ref expr, localExpr);
            }

            if (expr is not null) constraints.Add((double) _assetService.TotalValue(account.Holdings) == expr);
        }

        return constraints;
    }

    // a * VTI + e * VTI >= VTIAmount * (1 - tolerance)
    // a * VTI + e * VTI <= VTIAmount * (1 + tolerance)
    private async Task<List<LinearConstraint>> GenerateTargetAmountConstraints(
        IReadOnlyDictionary<string, decimal> targetValuePerAsset, ICollection<Account> accounts,
        double tolerance, IReadOnlyDictionary<string, Variable> variables)
    {
        var constraints = new List<LinearConstraint>();
        
        foreach (var assetName in targetValuePerAsset.Keys)
        {
            var asset = await _assetRepository.Get(assetName);
            Guard.Against.Null(asset, nameof(asset));

            LinearExpr? expr = null;
            foreach (var account in accounts)
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
                    var equivAsset = await _assetRepository.Get(asset.EquivalentTicker);
                    Guard.Against.Null(equivAsset, nameof(equivAsset));

                    var localExpr = (double) equivAsset.Value / assetValue *
                                    variables[GenerateVariableName(equivAsset.Ticker, account.Name)];
                    AddExpr(ref expr, localExpr);
                }
                else
                {
                    var localExpr = assetValue * variables[GenerateVariableName(assetName, account.Name)];
                    AddExpr(ref expr, localExpr);
                }
            }

            if (expr is null) continue;
            constraints.Add(expr >= (double) targetValuePerAsset[assetName] * (1 - tolerance));
            constraints.Add(expr <= (double) targetValuePerAsset[assetName] * (1 + tolerance));
        }

        return constraints;
    }
    
    // b + g + h - cash1 - cash2;
    private static LinearExpr? GenerateOptimization(
        IEnumerable<Account> accounts, IReadOnlyDictionary<string, Variable> variables)
    {
        LinearExpr? optimization = null;
        
        foreach (var account in accounts)
        {
            foreach (var assetName in account.PriorityAssets)
            {
                var localExpr = variables[GenerateVariableName(assetName, account.Name)];
                if (optimization is null) optimization = localExpr;
                else optimization += localExpr;
            }

            foreach (var assetName in account.UndesiredAssets)
            {
                var localExpr = variables[GenerateVariableName(assetName, account.Name)];
                if (optimization is null) optimization = localExpr;
                else optimization -= localExpr;
            }
        }

        return optimization;
    }

    private static (Solver.ResultStatus, Dictionary<string, decimal>) SolveSystem(
        Solver solver, List<LinearConstraint> constraints, LinearExpr? optimization)
    {
        Guard.Against.Null(optimization, nameof(optimization));

        foreach (var constraint in constraints) solver.Add(constraint);

        solver.Maximize(optimization);

        var resultStatus = solver.Solve();

        var resultValues = solver.variables().ToDictionary(x => x.Name(), x => (decimal) x.SolutionValue());

        return (resultStatus, resultValues);
    }
    
    private static void AddExpr(ref LinearExpr? expr, LinearExpr localExpr)
    {
        if (expr is null) expr = localExpr;
        else expr += localExpr;
    }

    private static string GenerateVariableName(string assetName, string accountName)
    {
        return $"{assetName}_{accountName}";
    }
}