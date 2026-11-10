using System.Text.Json.Serialization;
using FCG.Catalog.Domain.Enums;

namespace FCG.Catalog.Domain.Filters;

public class OrderFilter : BaseFilter
{
    [JsonPropertyName("userId")]
    public long? UserId { get; set; }

    [JsonPropertyName("gameId")]
    public long? GameId { get; set; }

    [JsonPropertyName("status")]
    public OrderStatus? Status { get; set; }
}
