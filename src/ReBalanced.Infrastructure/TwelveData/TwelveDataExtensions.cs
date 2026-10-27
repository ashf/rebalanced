using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ReBalanced.Infrastructure.TwelveData;

public static class TwelveDataExtensions
{
    public static void AddTwelveDataApi(this IServiceCollection services)
    {
        services.AddHttpClient<IPriceApi, TwelveDataApi>((provider, client) =>
            Configure(client, provider.GetRequiredService<IOptions<TwelveDataOptions>>().Value.ApiKey));
    }

    public static HttpClient Configure(HttpClient client, string apiKey)
    {
        client.BaseAddress = new Uri("https://api.twelvedata.com/");
        // the header keeps the key out of request URLs and logs
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("apikey", apiKey);
        return client;
    }
}
