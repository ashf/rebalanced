using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        var config = new ConfigurationBuilder()
            .AddJsonFile("api.appsettings.Development.json", true)
            .AddJsonFile("appsettings.development.json", true)
            .Build();
        var client = new HttpClient {BaseAddress = new Uri("https://api.twelvedata.com/")};
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("apikey", config["TwelveData:ApiKey"]);
        var api = new TwelveDataApi(client);

        var prices = await api.GetPrices(new[] {"VTI", "BTC/USD"});

        Assert.Equal(new[] {"BTC/USD", "VTI"}, new SortedSet<string>(prices.Keys));
    }

    [Fact]
    public void ResolvesFromDependencyInjection()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new TwelveDataOptions {ApiKey = "test"}));
        services.AddTwelveDataApi();
        using var provider = services.BuildServiceProvider();

        var api = provider.GetRequiredService<IPriceApi>();

        Assert.IsType<TwelveDataApi>(api);
    }

    [Fact]
    public async Task WaitsAndRetriesOnceWhenRateLimited()
    {
        var handler = new StubHandler(
            (HttpStatusCode.TooManyRequests, """{"code":429,"status":"error"}"""),
            (HttpStatusCode.OK, """{"price":"378.05"}"""));
        var waits = new List<TimeSpan>();
        var api = new TwelveDataApi(new HttpClient(handler) {BaseAddress = new Uri("https://api.twelvedata.com/")},
            wait =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            });

        var prices = await api.GetPrices(new[] {"VTI"});

        Assert.Equal((378.05M, 1), (prices["VTI"], waits.Count));
    }

    [Fact]
    public async Task SplitsSymbolsIntoBatchesOfEight()
    {
        var handler = new StubHandler((HttpStatusCode.OK, """{}"""), (HttpStatusCode.OK, """{}"""));
        var api = new TwelveDataApi(new HttpClient(handler) {BaseAddress = new Uri("https://api.twelvedata.com/")});
        var symbols = new[] {"A", "B", "C", "D", "E", "F", "G", "H", "I", "J"};

        await api.GetPrices(symbols);

        Assert.Equal(2, handler.Requests);
    }

    private class StubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Json)> _responses;

        public StubHandler(string json) : this((HttpStatusCode.OK, json))
        {
        }

        public StubHandler(params (HttpStatusCode Status, string Json)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            var (status, json) = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
            return Task.FromResult(new HttpResponseMessage(status) {Content = new StringContent(json)});
        }
    }
}
