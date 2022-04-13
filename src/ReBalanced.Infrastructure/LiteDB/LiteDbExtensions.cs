using Microsoft.Extensions.DependencyInjection;

namespace ReBalanced.Infrastructure.LiteDB;

public static class LiteDbServiceExtentions
{
    public static void AddLiteDb(this IServiceCollection services, string databasePath)
    {
        services.AddTransient<LiteDbContext, LiteDbContext>();
        services.Configure<LiteDbConfig>(options => options.DatabasePath = databasePath);
    }
}