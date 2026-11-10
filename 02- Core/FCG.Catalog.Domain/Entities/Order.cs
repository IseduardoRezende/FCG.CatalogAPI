using FCG.Catalog.Domain.Enums;

namespace FCG.Catalog.Domain.Entities;

public class Order : BaseEntity
{
    public long UserId { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public long GameId { get; set; }

    public string GameName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public bool IsPending => Status == OrderStatus.Pending;

    public bool ApplyPaymentResult(bool approved, DateTime processedAt)
    {
        if (!IsPending)
            return false;

        Status = approved ? OrderStatus.Approved : OrderStatus.Rejected;
        ProcessedAt = processedAt;
        return true;
    }
}
