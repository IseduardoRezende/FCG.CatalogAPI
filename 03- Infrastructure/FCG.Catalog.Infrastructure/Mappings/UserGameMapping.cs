using FCG.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Catalog.Infrastructure.Mappings;

public class UserGameMapping : BaseEntityMapping<UserGame>
{
    public override void Configure(EntityTypeBuilder<UserGame> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.UserEmail).HasMaxLength(150).IsRequired();
        builder.Property(c => c.GameName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.PricePaid).HasPrecision(18, 2);

        builder.HasIndex(c => new { c.UserId, c.GameId }).IsUnique();

        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(c => c.GameId);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(c => c.OrderId);
    }
}
