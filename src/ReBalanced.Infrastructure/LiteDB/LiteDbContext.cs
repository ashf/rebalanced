using LiteDB;
using Microsoft.Extensions.Options;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Infrastructure.LiteDB;

public class LiteDbContext
{
    public readonly LiteDatabase Context;

    public LiteDbContext(IOptions<LiteDbConfig> configs)
    {
        LiteDatabase? db;
        try
        {
            db = new LiteDatabase(configs.Value.DatabasePath);
        }
        catch (Exception ex)
        {
            throw new Exception("Can find or create LiteDb database.", ex);
        }

        Context = db;

        SetMappings();
    }

    private void SetMappings()
    {
        var mapper = BsonMapper.Global;

        mapper.Entity<Asset>()
            .Id(x => x.Ticker);

        mapper.Entity<Portfolio>()
            .Id(x => x.Id);
    }
}