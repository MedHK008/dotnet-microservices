using System.ComponentModel.DataAnnotations;

namespace CartRedis.Contracts;

public class UpdateCartItemRequest
{
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}
