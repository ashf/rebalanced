using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ReBalanced.Infrastructure.TwelveData;
using Xunit;

namespace ReBalanced.Infrastructure.Tests;

public class TwelveDataApiTests
{
    private static TwelveDataApi ApiReturning(string json)
    {
        var handler = new StubHandler(json);
        return new TwelveDataApi(new HttpClient(handler) {BaseAddress = new Uri("https://api.twelvedata.com/")});
    }

    [Fact]
    public async Task ParsesSingleSymbolResponse()
    {
        var api = ApiReturning("""{"price":"378.05000"}""");

        var prices = await api.GetPrices(new[] {"VTI"});

        Assert.Equal(378.05M, prices["VTI"]);
    }

    [Fact]
    public async Task ParsesBatchResponseAndSkipsPerSymbolErrors()
    {
        var api = ApiReturning(
            """{"VTI":{"price":"378.05"},"BTC/USD":{"price":"80537.76"},"NOPE":{"code":400,"status":"error"}}""");

        var prices = await api.GetPrices(new[] {"VTI", "BTC/USD", "NOPE"});

        Assert.Equal(new Dictionary<string, decimal> {["VTI"] = 378.05M, ["BTC/USD"] = 80537.76M}, prices);
    }

    [Fact]
    public async Task MatchesBatchSymbolsCaseInsensitively()
    {
        var api = ApiReturning("""{"vti":{"price":"378.05"},"BND":{"price":"71.30"}}""");

        var prices = await api.GetPrices(new[] {"VTI", "BND"});

        Assert.Equal(378.05M, prices["VTI"]);
    }

    [Fact]
    public async Task ThrowsOnTopLevelError()
    {
        var api = ApiReturning("""{"code":401,"message":"invalid api key","status":"error"}""");

        var exception = await Record.ExceptionAsync(() => api.GetPrices(new[] {"VTI"}));

        Assert.IsType<HttpRequestException>(exception);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CanGetLivePrices()
    {
        var config = new ConfigurationBuilder().AddJsonFile("appsettings.development.json").Build();
        var client = new HttpClient {BaseAddress = new Uri("https://api.twelvedata.com/")};
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("apikey", config["TwelveData:ApiKey"]);
        var api = new TwelveDataApi(client);

        var prices = await api.GetPrices(new[] {"VTI", "BTC/USD"});

        Assert.Equal(new[] {"BTC/USD", "VTI"}, new SortedSet<string>(prices.Keys));
    }

    private class StubHandler : HttpMessageHandler
    {
        private readonly string _json;

        public StubHandler(string json)
        {
            _json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent(_json)});
        }
    }
}
