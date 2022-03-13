<Query Kind="Program">
  <NuGetReference>Google.OrTools</NuGetReference>
  <NuGetReference>Google.OrTools.runtime.linux-x64</NuGetReference>
  <NuGetReference>Humanizer</NuGetReference>
  <NuGetReference>Microsoft.EntityFrameworkCore</NuGetReference>
  <NuGetReference>Microsoft.EntityFrameworkCore.Relational</NuGetReference>
  <NuGetReference>Microsoft.EntityFrameworkCore.Tools</NuGetReference>
  <NuGetReference>Npgsql.EntityFrameworkCore.PostgreSQL</NuGetReference>
  <NuGetReference>TimeZoneConverter</NuGetReference>
  <Namespace>Google.OrTools.LinearSolver</Namespace>
  <Namespace>Humanizer</Namespace>
  <Namespace>Microsoft.EntityFrameworkCore</Namespace>
  <Namespace>Microsoft.EntityFrameworkCore.Query</Namespace>
  <Namespace>Microsoft.EntityFrameworkCore.Query.SqlExpressions</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Text.Json.Serialization</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>TimeZoneConverter</Namespace>
  <RuntimeVersion>6.0</RuntimeVersion>
</Query>

public class Account
{
	public string Name { get; set; }
	public List<Holding> Holdings { get; set; } = new();
	public List<string> PriorityAssets { get; set; } = new();
	public List<string> UndesiredAssets { get; set; } = new();
	public double Value(Dictionary<string, Asset> assets)
	{
		return Holdings.Sum(holding => holding.Amount * assets[holding.AssetName].Value);
	}
}

public record Holding(string AssetName, double Amount);
public record Asset(string Name, double Value);
public record VariableWrapper(Variable Variable, string AccountName, string AssetName);

void Main()
{
	var assets = new Dictionary<string, Asset>()
	{
		{"VTI", new Asset("VTI", 211.99) },
		{"VEA", new Asset("VEA", 45.15 ) },
		{"VWO", new Asset("VWO", 43.86) },
		{"VXUS", new Asset("VXUS", 56.26) },
		{"VNQ", new Asset("VNQ", 103.67) },
		{"BND", new Asset("BND", 80.37) },
		{"CASH", new Asset("CASH", 1) },
	};
	
	var accounts = new List<Account>();
	
	var investmentAccount = new Account();
	investmentAccount.Name = "INV";
	investmentAccount.Holdings.Add(new Holding("VTI", 100));
	investmentAccount.Holdings.Add(new Holding("VXUS", 50));
	investmentAccount.Holdings.Add(new Holding("CASH", 200));
	investmentAccount.PriorityAssets.Add("VXUS");
	investmentAccount.PriorityAssets.Add("CASH");
	accounts.Add(investmentAccount);

	var rothAccount = new Account();
	rothAccount.Name = "ROTH";
	rothAccount.Holdings.Add(new Holding("VTI", 10));
	rothAccount.Holdings.Add(new Holding("VNQ", 15));
	rothAccount.Holdings.Add(new Holding("BND", 20));
	rothAccount.Holdings.Add(new Holding("CASH", 100));
	rothAccount.PriorityAssets.Add("VNQ");
	rothAccount.PriorityAssets.Add("BND");
	rothAccount.UndesiredAssets.Add("CASH");
	accounts.Add(rothAccount);	

	var allocations = new Dictionary<string, double>()
	{
		{ "VTI", .3725 },
		{ "VXUS", .3825 },
		{ "VNQ", .045 },
		{ "BND", .1 },
		{ "CASH", .1 }
	};
	
	IterativeSolve(assets, accounts, allocations);	
}

public void IterativeSolve(Dictionary<string, Asset> assets, List<Account> accounts, Dictionary<string, double> allocations)
{
	double total = accounts.Sum(account => account.Value(assets));
	
	var targetValuePerAsset = allocations.ToDictionary(x => x.Key, x => x.Value * total).Dump();

	var tolerance = 0.00;

	Solver.ResultStatus resultStatus = Solver.ResultStatus.NOT_SOLVED;
	Dictionary<string, decimal> resultValues = default;
	double objectiveValue = 0;
	var iterations = 0;

	while (resultStatus != Solver.ResultStatus.OPTIMAL || objectiveValue <= 0)
	{
		var (solver, constraints, optimization) = SetupSystem(assets, targetValuePerAsset, accounts, tolerance);

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
		resultValues.Dump();
	}
}

public string GenerateVariableName(string assetName, string accountName)
{
	return $"{assetName}_{accountName}";
}

public (Solver solver, IEnumerable<LinearConstraint> constraints, LinearExpr optimization) SetupSystem(
	Dictionary<string, Asset> assets, 
	Dictionary<string, double> targetValuePerAsset,
	List<Account> accounts,
	double tolerance)
{
	var solver = Solver.CreateSolver("SCIP");

	var variables = new Dictionary<string, VariableWrapper>();
	
	var constraints = new List<LinearConstraint>();

	foreach (var account in accounts)
	{
		foreach (var assetName in assets.Keys)
		{
			var varName = GenerateVariableName(assetName, account.Name);
			if (assetName != "CASH")
			{
				variables.Add(varName, new VariableWrapper(solver.MakeIntVar(0.0, int.MaxValue, varName), account.Name, assetName));
			}
			else
			{
				variables.Add(varName, new VariableWrapper(solver.MakeNumVar(0.0, double.MaxValue, varName), account.Name, assetName));
			}
		}
	}
	
	// a * VTI + b * VXUS + c * VNQ + d * BND + cash1 * CASH == account1,
	foreach (var account in accounts)
	{
		LinearExpr expr = null;
		foreach (var assetName in targetValuePerAsset.Keys)
		{
			var asset = assets[assetName];
			var localExpr = asset.Value * variables[GenerateVariableName(asset.Name, account.Name)].Variable;
			if (expr is null) expr = localExpr;
			else expr += localExpr;
		}		
		constraints.Add(account.Value(assets) == expr);
	}

	// a * VTI + e * VTI >= VTIAmount * (1 - tolerance)
	// a * VTI + e * VTI <= VTIAmount * (1 + tolerance)
	foreach (var asset in assets)
	{
		if (!targetValuePerAsset.ContainsKey(asset.Key)) continue;
		
		LinearExpr expr = null;
		foreach (var account in accounts)
		{
			if ((asset.Key == "CASH") && account.UndesiredAssets.Contains("CASH")) continue;
			
			var localExpr = asset.Value.Value * variables[GenerateVariableName(asset.Key, account.Name)].Variable;
			if (expr is null) expr = localExpr;
			else expr += localExpr;
		}

		if (expr is not null)
		{
			constraints.Add(expr >= targetValuePerAsset[asset.Key] * (1 - tolerance));
			constraints.Add(expr <= targetValuePerAsset[asset.Key] * (1 + tolerance));
		}
	}

	// b + g + h - cash1 - cash2;
	LinearExpr optimization = null;
	foreach (var account in accounts)
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

public (Solver.ResultStatus, Dictionary<string, decimal>) SolveSystem(Solver solver, IEnumerable<LinearConstraint> constraints, LinearExpr optimization)
{
	foreach (var constraint in constraints)
	{
		solver.Add(constraint);
	}

	solver.Maximize(optimization);

	var resultStatus = solver.Solve();

	var resultValues = solver.variables().ToDictionary(x => x.Name(), x => (decimal)x.SolutionValue());

	return (resultStatus, resultValues);
}
