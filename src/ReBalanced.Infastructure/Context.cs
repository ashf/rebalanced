using Microsoft.EntityFrameworkCore;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.Entities.Aggregates;
using ReBalanced.Infastructure.EntityConfigurations;

namespace ReBalanced.Infastructure;

public class Context : DbContext
{
    public Context(DbContextOptions<Context> options) : base(options)
    {
    }

    public DbSet<Account> Account { get; set; } = null!;
    public DbSet<Holding> Holding { get; set; } = null!;
    public DbSet<Portfolio> Portfolio { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfiguration(new AccountEntityTypeConfiguration());
        builder.ApplyConfiguration(new HoldingEntityTypeConfiguration());
        builder.ApplyConfiguration(new PortfolioEntityTypeConfiguration());
    }
}