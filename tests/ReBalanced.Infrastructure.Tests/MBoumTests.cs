using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ReBalanced.Infrastructure.MBoum;
using Refit;
using Xunit;

namespace ReBalanced.Infrastructure.Tests;

public class MBoumTests
{
    [Theory]
    [InlineData("VTI")]
    public async Task CanGetStockQuote(string symbol)
    {
        // Arange
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.development.json")
            .Build();
        
        var mBoumApi = RestService.For<IMBoumApi>("https://mboum.com/api/v1");
        var apikey = config["MBOUM:APIKEY"];
        
        // Act
        var result = await mBoumApi.GetStockQuotes(apikey!, symbol);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data!);
        Assert.True(result.Data?[0].Symbol == symbol);
    }
    
    [Theory]
    [InlineData("VTI,VNQ")]
    [InlineData("VTI,VNQ,BND")]
    public async Task CanGetStockQuotes(string symbols)
    {
        // Arange
        var symbolsList = symbols.Split(',');
        
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.development.json")
            .Build();
        
        var mBoumApi = RestService.For<IMBoumApi>("https://mboum.com/api/v1");
        var apikey = config["MBOUM:APIKEY"];
        
        // Act
        var result = await mBoumApi.GetStockQuotes(apikey!, symbols);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data!);
        Assert.True(result.Data?.Count == symbolsList.Length);
        for (var i = 0; i < symbolsList.Length; i++)
        {
            Assert.True(result.Data?[i].Symbol == symbolsList[i]);
        }
    }
    
    [Theory]
    [InlineData("bitcoin")]
    public async Task CanGetCoinQuote(string key)
    {
        // Arange
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.development.json")
            .Build();
        
        var mBoumApi = RestService.For<IMBoumApi>("https://mboum.com/api/v1");
        var apikey = config["MBOUM:APIKEY"];
        
        // Act
        var result = await mBoumApi.GetCoinQuote(apikey!, key);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.True(result.Meta?.Key == key);
    }
}