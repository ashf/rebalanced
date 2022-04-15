using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace ReBalanced.Infrastructure.MBoum;

public static class MBoumApiExtensions
{
    public static void AddMBoumApi(this IServiceCollection services, string baseAddress)
    {
        services.AddRefitClient<IRefitMBoumApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress));
        services.AddTransient<IMBoumApi, MBoumApi>();
    }
}