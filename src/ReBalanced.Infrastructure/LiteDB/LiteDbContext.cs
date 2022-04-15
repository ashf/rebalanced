using LiteDB;
using LiteDB.Async;
using Microsoft.Extensions.Options;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.ValueTypes;

namespace ReBalanced.Infrastructure.LiteDB;

public class LiteDbContext
{
    public readonly LiteDatabaseAsync Context;

    public LiteDbContext(IOptions<LiteDbConfig> configs)
    {
        LiteDatabaseAsync? db;
        try
        {
            db = new LiteDatabaseAsync(configs.Value.DatabasePath);
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

        // https://stackoverflow.com/a/66497628
        // DateTimeOffset truncates milliseconds by default
        mapper.RegisterType
        (
            obj => new BsonDocument
            {
                ["DateTime"] = obj.DateTime.Ticks,
                ["Offset"] = obj.Offset.Ticks
            },
            doc => new DateTimeOffset(doc["DateTime"].AsInt64, new TimeSpan(doc["Offset"].AsInt64)));
        
        mapper.Entity<Asset>()
            .Id(x => x.Ticker);

        mapper.Entity<Portfolio>()
            .Id(x => x.Id);
    }
}