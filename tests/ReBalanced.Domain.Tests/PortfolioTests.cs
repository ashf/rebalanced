using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using Xunit;

namespace ReBalanced.Domain.Tests;

public class AllocationDataGenerator : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new List<object[]>
    {
        new object[] {false, ("A", 1M)},
        new object[] {false, ("A", 0.5M), ("B", 0.5M)},
        new object[] {true, ("A", 1M), ("B", 1M)},
        new object[] {true}
    };

    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class PortfolioTests
{
    [Theory]
    [ClassData(typeof(AllocationDataGenerator))]
    public void SetAllocationsTest(bool exceptionThrown, params (string ticker, decimal percentage)[] allocations)
    {
        // arrange
        var portfolio = new Portfolio("Test Portfolio");
        
        var allocationsList = allocations
            .Select(allocation => new Allocation(allocation.ticker, allocation.percentage))
            .ToList();
        
        // act
        var exception = Record.Exception(() => portfolio.SetAllocation(allocationsList));
        
        // assert
        if (exceptionThrown)
        {
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }
        else
        {
            Assert.Null(exception);
        }
    }
}