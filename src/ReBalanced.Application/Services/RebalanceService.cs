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
        var portfolioTotal = await portfolio.TotalValue(_assetService, true);

        var targetValuePerAsset =
            portfolio.Allocations.ToDictionary(
                x => x.Key,
                x => x.Value.Percentage * portfolioTotal);

        var tolerance = 0.00;

        var resultStatus = Solver.ResultStatus.NOT_SOLVED;
        var resultValues = new Dictionary<string, decimal>();

        double objectiveValue = 0;
        var iteration = 0;

        while (resultStatus != Solver.ResultStatus.OPTIMAL && objectiveValue <= 0 && iteration <= MaxIterations)
        {
            var (solver, constraints, optimization) =
                await SetupSystem(targetValuePerAsset, portfolio, tolerance);

            _logger.LogInformation("{Iteration}", iteration);
            (resultStatus, resultValues) = SolveSystem(solver, constraints, optimization);

            objectiveValue = solver.Objective().Value();

            tolerance += 0.0001;
            iteration++;
        }

        // Check that the problem has an optimal solution.
        if (resultStatus == Solver.ResultStatus.OPTIMAL)
        {
            _logger.LogDebug("Solution:");
            _logger.LogDebug("iterations = {Iteration}", iteration);
            _logger.LogDebug("tolerance = {Tolerance}", tolerance);
            _logger.LogDebug("Objective value = {ObjectiveValue}", objectiveValue);
        }

        AddFractionalsToResults(portfolio, resultValues);

        return resultValues;
    }

    private static void AddFractionalsToResults(Portfolio portfolio, Dictionary<string, decimal> resultValues)
    {
        // add back in fractionals
        foreach (var (resultKey, resultValue) in resultValues)
        {
            var assetName = resultKey.Split('_')[0];
            var accountName = resultKey.Split('_')[1];

            var account = portfolio.Accounts.Values.First(x => x.Name == accountName);
            Guard.Against.Null(account, nameof(account));

            if (account.AllowFractional) continue;

            var holding = account.Holdings.FirstOrDefault(x => x.Asset.Ticker == assetName);
            if (holding is null) continue;

            var fraction = holding.Quantity - Math.Truncate(holding.Quantity);

            resultValues[resultKey] = resultValue + fraction;
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

        foreach (var account in accounts)
        {
            LinearExpr? expr = null;
            foreach (var assetName in allocatedTickers)
            {
                var asset = await _assetRepository.Get(assetName);
                Guard.Against.Null(asset, nameof(asset));

                switch (account.PermissibleAssets.Contains(assetName))
                {
                    case false when account.PermissibleAssets.Contains(asset.EquivalentTicker):
                        asset = await _assetRepository.Get(asset.EquivalentTicker);
                        Guard.Against.Null(asset, nameof(asset));
                        break;
                    case false:
                        continue;
                }

                var localExpr = (double) asset.Value * variables[GenerateVariableName(asset.Ticker, account.Name)];
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

            if (expr is null) continue;
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

            var assetValue = (double) asset.Value;

            if (account.PermissibleAssets.Contains(asset.Ticker))
            {
                var localExpr = assetValue * variables[GenerateVariableName(asset.Ticker, account.Name)];
                AddExpr(ref expr, localExpr);
            }
            else
            {
                // check if EquivalentTicker is allowed (if asset isn't)
                if (asset.EquivalentTicker is not null &&
                    !account.PermissibleAssets.Contains(asset.EquivalentTicker)) continue;
                if (asset.EquivalentTicker is null) continue;

                var equivAsset = await _assetRepository.Get(asset.EquivalentTicker);
                Guard.Against.Null(equivAsset, nameof(equivAsset));

                var localEquivExpr = (double) equivAsset.Value *
                                     variables[GenerateVariableName(equivAsset.Ticker, account.Name)];
                AddExpr(ref expr, localEquivExpr);
            }
        }

        return expr;
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