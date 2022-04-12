using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Entities;

namespace ReBalanced.Infastructure.EntityConfigurations;

internal class HoldingEntityTypeConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.HasKey(x => x.Id);
    }
}