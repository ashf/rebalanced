using Ardalis.GuardClauses;
using Google.OrTools.LinearSolver;
using Microsoft.Extensions.Logging;
using ReBalanced.Application.Services.Extensions;
using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Application.Services;

public class RebalanceService : IRebalanceService
{
    private const int MaxIterations = 1000;
    private const double ToleranceStep = 0.0001;
    private readonly IAssetRepository _assetRepository;
    private readonly IAssetService _assetService;
    private readonly ILogger<RebalanceService> _logger;

    public RebalanceService(
        IAssetService assetService,
        IAssetRepository assetRepository,
        ILogger<RebalanceService> logger)
    {
        _assetService = assetService;
        _assetRepository = assetRepository;
        _logger = logger;
    }

    public async Task<Dictionary<string, decimal>> Rebalance(Portfolio portfolio)
    {
        var portfolioTotal = await portfolio.TotalValue(_assetService, true);

        var targetValuePerAsset =
            portfolio.Allocations.ToDictionary(
                x => x.Key,
                x => x.Value.Percentage * portfolioTotal);

        // an infeasible portfolio would otherwise run every iteration before failing
        var (widestSolver, widestConstraints, widestOptimization) =
            await SetupSystem(targetValuePerAsset, portfolio, MaxIterations * ToleranceStep);
        using (widestSolver)
        {
            var (widestStatus, _) = SolveSystem(widestSolver, widestConstraints, widestOptimization);
            if (widestStatus != Solver.ResultStatus.OPTIMAL)
                throw new InvalidOperationException(
                    $"No optimal rebalance within {MaxIterations * ToleranceStep:P0} tolerance (status {widestStatus})");
        }

        var tolerance = 0.0;

        var resultStatus = Solver.ResultStatus.NOT_SOLVED;
        var resultValues = new Dictionary<string, decimal>();

        double objectiveValue = 0;
        var iteration = 0;

        while (resultStatus != Solver.ResultStatus.OPTIMAL && iteration <= MaxIterations)
        {
            var (solver, constraints, optimization) =
                await SetupSystem(targetValuePerAsset, portfolio, tolerance);
            using var disposableSolver = solver;

            _logger.LogDebug("{Iteration}", iteration);
            (resultStatus, resultValues) = SolveSystem(solver, constraints, optimization);

            objectiveValue = solver.Objective().Value();

            iteration++;
            tolerance = iteration * ToleranceStep;
        }

        if (resultStatus != Solver.ResultStatus.OPTIMAL)
            throw new InvalidOperationException(
                $"No optimal rebalance found after {iteration} iterations (status {resultStatus})");

        _logger.LogDebug("Solution:");
        _logger.LogDebug("iterations = {Iteration}", iteration);
        _logger.LogDebug("tolerance = {Tolerance}", tolerance);
        _logger.LogDebug("Objective value = {ObjectiveValue}", objectiveValue);

        AddFractionalsToResults(portfolio, resultValues);

        return resultValues;
    }

    private static void AddFractionalsToResults(Portfolio portfolio, Dictionary<string, decimal> resultValues)
    {
        // whole-share accounts are solved without their fractional shares, so add them back
        foreach (var account in portfolio.Accounts.Values.Where(x => !x.AllowFractional))
        foreach (var holding in account.Holdings.Where(x => x.Asset.AssetType != AssetType.Cash))
        {
            var resultKey = GenerateVariableName(holding.Asset.Ticker, account.Name);
            if (!resultValues.TryGetValue(resultKey, out var resultValue)) continue;

            resultValues[resultKey] = resultValue + holding.Quantity - Math.Truncate(holding.Quantity);
        }
    }

    private async Task<(Solver solver, List<LinearConstraint> constraints, LinearExpr? optimization)>
        SetupSystem(
            Dictionary<string, decimal> targetValuePerAsset,
            Portfolio portfolio,
            double tolerance)
    {
        var solver = Solver.CreateSolver("SCIP");

        var accounts = portfolio.Accounts.Values;

        var variables = await PopulateVariables(accounts, solver);

        var constraints = new List<LinearConstraint>();
        constraints.AddRange(
            await GeneratePermissibleAssetsConstraints(portfolio.Allocations.Keys.ToHashSet(), accounts, variables));
        constraints.AddRange(
            await GenerateTargetAmountConstraints(targetValuePerAsset, accounts, tolerance, variables));

        var optimization = GenerateOptimization(portfolio.Allocations.Keys.ToHashSet(), accounts, variables);

        return (solver, constraints, optimization);
    }

    private async Task<Dictionary<string, Variable>> PopulateVariables(IEnumerable<Account> accounts, Solver solver)
    {
        var variables = new Dictionary<string, Variable>();

        foreach (var account in accounts)
        foreach (var assetName in account.PermissibleAssets)
        {
            var asset = await _assetRepository.Get(assetName);
            Guard.Against.Null(asset, assetName);

            var varName = GenerateVariableName(assetName, account.Name);
            variables.Add(varName,
                account.AllowFractional || asset.AssetType == AssetType.Cash
                    ? solver.MakeNumVar(0.0, double.MaxValue, varName)
                    : solver.MakeIntVar(0.0, int.MaxValue, varName));
        }

        return variables;
    }

    // a * VTI + b * VXUS + c * VNQ + d * BND + cash1 * CASH == account1
    private async Task<List<LinearConstraint>> GeneratePermissibleAssetsConstraints(
        IReadOnlySet<string> allocatedTickers, IEnumerable<Account> accounts,
        IReadOnlyDictionary<string, Variable> variables)
    {
        var constraints = new List<LinearConstraint>();

        // CASH is always in the sum so whole-share accounts can hold their leftover value
        var tickers = allocatedTickers.Append("CASH").ToHashSet();

        foreach (var account in accounts)
        {
            LinearExpr? expr = null;
            var used = new HashSet<string>();
            foreach (var assetName in tickers)
            {
                var asset = await _assetRepository.Get(assetName);
                Guard.Against.Null(asset, nameof(asset));

                var permitted = await ResolvePermittedAsset(account, asset);
                if (permitted is null || !used.Add(permitted.Ticker)) continue;

                var localExpr = (double) permitted.Value *
                                variables[GenerateVariableName(permitted.Ticker, account.Name)];
                AddExpr(ref expr, localExpr);
            }

            if (expr is not null)
                constraints.Add((double) await _assetService.TotalValue(account, account.AllowFractional) == expr);
        }

        return constraints;
    }

    // a * VTI + e * VTI >= VTIAmount * (1 - tolerance)
    // a * VTI + e * VTI <= VTIAmount * (1 + tolerance)
    private async Task<List<LinearConstraint>> GenerateTargetAmountConstraints(
        Dictionary<string, decimal> targetValuePerAsset, ICollection<Account> accounts,
        double tolerance, IReadOnlyDictionary<string, Variable> variables)
    {
        var constraints = new List<LinearConstraint>();

        foreach (var assetName in targetValuePerAsset.Keys)
        {
            var asset = await _assetRepository.Get(assetName);
            Guard.Against.Null(asset, nameof(asset));

            var expr = await GenerateTargetAmountConstraintPerAsset(accounts, variables, asset);

            if (expr is null)
            {
                if (asset.AssetType == AssetType.Cash) continue;
                throw new InvalidOperationException($"No account can hold {assetName} or its equivalent");
            }
            constraints.Add(expr >= (double) targetValuePerAsset[assetName] * (1 - tolerance));
            constraints.Add(expr <= (double) targetValuePerAsset[assetName] * (1 + tolerance));
        }

        return constraints;
    }

    // a * VTI + e * VTI
    private async Task<LinearExpr?> GenerateTargetAmountConstraintPerAsset(
        IEnumerable<Account> accounts, IReadOnlyDictionary<string, Variable> variables,
        Asset asset)
    {
        LinearExpr? expr = null;

        foreach (var account in accounts)
        {
            // dont create constraint for cash if undesired
            if (asset.AssetType == AssetType.Cash && account.UndesiredAssets.Contains("CASH")) continue;

            var permitted = await ResolvePermittedAsset(account, asset);
            if (permitted is null) continue;

            var localExpr = (double) permitted.Value *
                            variables[GenerateVariableName(permitted.Ticker, account.Name)];
            AddExpr(ref expr, localExpr);
        }

        return expr;
    }

    // the asset itself if the account can hold it, else its equivalent if the account can hold that
    private async Task<Asset?> ResolvePermittedAsset(Account account, Asset asset)
    {
        if (account.PermissibleAssets.Contains(asset.Ticker)) return asset;
        if (asset.EquivalentTicker is null || !account.PermissibleAssets.Contains(asset.EquivalentTicker)) return null;

        var equivAsset = await _assetRepository.Get(asset.EquivalentTicker);
        Guard.Against.Null(equivAsset, nameof(equivAsset));
        return equivAsset;
    }

    // b + g + h - cash1 - cash2;
    private static LinearExpr GenerateOptimization(
        IReadOnlySet<string> allocatedTickers, IEnumerable<Account> accounts,
        IReadOnlyDictionary<string, Variable> variables)
    {
        LinearExpr? optimization = null;
        LinearExpr? backupOptimization = null;

        foreach (var account in accounts)
        {
            foreach (var assetName in account.PriorityAssets)
            {
                if (!account.PermissibleAssets.Contains(assetName)) continue;

                var variable = variables[GenerateVariableName(assetName, account.Name)];

                var weight = assetName == "CASH" ? 0.5 : 1;

                backupOptimization = AddExprToOptimization(backupOptimization, variable, true, weight);

                if (allocatedTickers.Contains(assetName))
                    optimization = AddExprToOptimization(optimization, variable, true, weight);
            }

            foreach (var assetName in account.UndesiredAssets)
            {
                if (!account.PermissibleAssets.Contains(assetName)) continue;

                var variable = variables[GenerateVariableName(assetName, account.Name)];

                var weight = assetName == "CASH" ? 0.5 : 1;

                backupOptimization = AddExprToOptimization(backupOptimization, variable, false, weight);
                optimization = AddExprToOptimization(optimization, variable, false, weight);
            }
        }

        optimization ??= backupOptimization;

        Guard.Against.Null(optimization, nameof(optimization));

        return optimization;
    }

    private static LinearExpr AddExprToOptimization(LinearExpr? optimization, Variable variable, bool positive,
        double weight = 1)
    {
        var expr = (positive ? 1 : -1) * variable * weight;
        if (optimization is null) optimization = expr;
        else optimization += expr;
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

    private static string GenerateVariableName(string? assetName, string accountName)
    {
        return $"{assetName}_{accountName}";
    }
}