using FCG.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Catalog.Infrastructure.Mappings;

public class OrderMapping : BaseEntityMapping<Order>
{
    public override void Configure(EntityTypeBuilder<Order> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.UserEmail).HasMaxLength(150).IsRequired();
        builder.Property(c => c.GameName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Price).HasPrecision(18, 2);
        builder.Ignore(c => c.IsPending);

        builder.HasIndex(c => new { c.UserId, c.GameId, c.Status });

        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(c => c.GameId);
    }
}
