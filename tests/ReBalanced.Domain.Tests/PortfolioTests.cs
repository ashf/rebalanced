using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using Xunit;

namespace ReBalanced.Domain.Tests;

public class ValidAllocationData : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new()
    {
        new object[] {("A", 1M)},
        new object[] {("A", 0.5M), ("B", 0.5M)}
    };

    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class InvalidAllocationData : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new()
    {
        new object[] {("A", 1M), ("B", 1M)},
        new object[] { }
    };

    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class PortfolioTests
{
    [Theory]
    [ClassData(typeof(ValidAllocationData))]
    public void SetAllocationAcceptsAllocationsSummingToOne(params (string ticker, decimal percentage)[] allocations)
    {
        var portfolio = new Portfolio("Test Portfolio");
        var allocationsList = allocations.Select(x => new Allocation(x.ticker, x.percentage)).ToList();

        var exception = Record.Exception(() => portfolio.SetAllocation(allocationsList));

        Assert.Null(exception);
    }

    [Theory]
    [ClassData(typeof(InvalidAllocationData))]
    public void SetAllocationRejectsAllocationsNotSummingToOne(params (string ticker, decimal percentage)[] allocations)
    {
        var portfolio = new Portfolio("Test Portfolio");
        var allocationsList = allocations.Select(x => new Allocation(x.ticker, x.percentage)).ToList();

        var exception = Record.Exception(() => portfolio.SetAllocation(allocationsList));

        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void NewPortfolioAllocatesAllToCash()
    {
        var portfolio = new Portfolio("Test Portfolio");

        var allocations = portfolio.Allocations;

        Assert.Equal(1M, allocations["CASH"].Percentage);
    }

    [Fact]
    public void AddAccountRejectsDuplicateName()
    {
        var portfolio = new Portfolio("Test Portfolio");
        portfolio.AddAccount(new Account("INV", AccountType.Taxable, HoldingType.Quantity, false,
            new HashSet<string> {"CASH"}));
        var duplicate = new Account("INV", AccountType.Roth, HoldingType.Quantity, false,
            new HashSet<string> {"CASH"});

        var exception = Record.Exception(() => portfolio.AddAccount(duplicate));

        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void AccountDoesNotMutateCallersPermissibleAssets()
    {
        var permissibleAssets = new HashSet<string> {"CASH", "bitcoin"};

        _ = new Account("INV", AccountType.Taxable, HoldingType.Quantity, false, permissibleAssets);

        Assert.Contains("bitcoin", permissibleAssets);
    }
}
