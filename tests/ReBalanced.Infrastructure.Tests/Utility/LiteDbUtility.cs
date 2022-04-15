using System;
using System.IO;
using Microsoft.Extensions.Options;
using ReBalanced.Infrastructure.LiteDB;

namespace ReBalanced.Infrastructure.Tests.Utility;

public static class LiteDbUtility
{
    public static LiteDbContext GetTestLiteDb()
    {
        const Environment.SpecialFolder folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        var dbPath = Path.Join(path, "rebalanced_test.db");
        var liteDbOptions = Options.Create(new LiteDbConfig{DatabasePath = dbPath});
        return new LiteDbContext(liteDbOptions);
    }
}