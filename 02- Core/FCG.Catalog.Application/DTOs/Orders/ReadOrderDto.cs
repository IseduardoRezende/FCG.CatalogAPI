using FCG.Catalog.Domain.Enums;

namespace FCG.Catalog.Application.DTOs.Orders;

public class ReadOrderDto
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public long GameId { get; set; }

    public string GameName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public OrderStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
