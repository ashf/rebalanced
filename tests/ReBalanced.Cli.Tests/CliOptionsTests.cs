namespace ReBalanced.Cli.Tests;

public class CliOptionsTests
{
    [Fact]
    public void ParseDefaults()
    {
        var args = new[] {"plan.xlsx"};

        var options = CliOptions.Parse(args);

        Assert.Equal(new CliOptions("plan.xlsx", null, null, null, 0.01M, false), options);
    }

    [Theory]
    [InlineData("include", true)]
    [InlineData("ignore", false)]
    public void ParseCrypto(string value, bool expected)
    {
        var args = new[] {"plan.xlsx", "--crypto", value};

        var options = CliOptions.Parse(args);

        Assert.Equal(expected, options.IncludeCrypto);
    }

    [Theory]
    [InlineData("include", true)]
    [InlineData("ignore", false)]
    public void ParseCash(string value, bool expected)
    {
        var args = new[] {"plan.xlsx", "--cash", value};

        var options = CliOptions.Parse(args);

        Assert.Equal(expected, options.IncludeCash);
    }

    [Fact]
    public void ParseCashTarget()
    {
        var args = new[] {"plan.xlsx", "--cash-target", "750.5"};

        var options = CliOptions.Parse(args);

        Assert.Equal(750.5M, options.CashTarget);
    }

    [Fact]
    public void ParseBand()
    {
        var args = new[] {"plan.xlsx", "--band", "0.05"};

        var options = CliOptions.Parse(args);

        Assert.Equal(0.05M, options.Band);
    }

    [Fact]
    public void ParseLivePrices()
    {
        var args = new[] {"plan.xlsx", "--live-prices"};

        var options = CliOptions.Parse(args);

        Assert.True(options.LivePrices);
    }

    [Fact]
    public void ParsePathAfterOptions()
    {
        var args = new[] {"--live-prices", "plan.xlsx"};

        var options = CliOptions.Parse(args);

        Assert.Equal("plan.xlsx", options.Path);
    }

    [Fact]
    public void ParseInvalidCryptoThrows()
    {
        var args = new[] {"plan.xlsx", "--crypto", "maybe"};

        void Act() => CliOptions.Parse(args);

        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void ParseUnknownOptionThrows()
    {
        var args = new[] {"plan.xlsx", "--bogus"};

        void Act() => CliOptions.Parse(args);

        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void ParseMissingPathThrows()
    {
        var args = new[] {"--live-prices"};

        void Act() => CliOptions.Parse(args);

        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void ParseMissingValueThrows()
    {
        var args = new[] {"plan.xlsx", "--band"};

        void Act() => CliOptions.Parse(args);

        Assert.Throws<ArgumentException>(Act);
    }
}
