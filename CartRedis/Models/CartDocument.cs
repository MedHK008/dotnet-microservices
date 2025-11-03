using System.Collections.Generic;

namespace CartRedis.Models;

public class CartDocument
{
    public List<CartItem> Items { get; set; } = new();
}
