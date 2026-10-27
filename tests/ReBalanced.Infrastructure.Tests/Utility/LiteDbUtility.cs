using Microsoft.Extensions.Options;
using ReBalanced.Infrastructure.LiteDB;

namespace ReBalanced.Infrastructure.Tests.Utility;

public static class LiteDbUtility
{
    public static LiteDbContext GetTestLiteDb()
    {
        var liteDbOptions = Options.Create(new LiteDbConfig {DatabasePath = ":memory:"});
        return new LiteDbContext(liteDbOptions);
    }
}