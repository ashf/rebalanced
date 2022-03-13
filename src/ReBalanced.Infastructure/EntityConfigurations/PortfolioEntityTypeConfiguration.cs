using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReBalanced.Domain.Entities;

namespace ReBalanced.Infastructure.EntityConfigurations;

internal class PortfolioEntityTypeConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.HasKey(x => x.Id);
    }
}