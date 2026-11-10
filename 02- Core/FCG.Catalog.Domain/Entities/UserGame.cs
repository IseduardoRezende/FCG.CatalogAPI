namespace FCG.Catalog.Domain.Entities;

public class UserGame : BaseEntity
{
    public long UserId { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public long GameId { get; set; }

    public string GameName { get; set; } = string.Empty;

    public decimal PricePaid { get; set; }

    public long OrderId { get; set; }

    public DateTime PurchasedAt { get; set; }
}
