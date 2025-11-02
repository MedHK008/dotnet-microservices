using System;
using System.Collections.Generic;
using System.Linq;
using CartRedis.Models;

namespace CartRedis.Contracts;

public sealed class CartResponse
{
    public IReadOnlyList<CartItem> Items { get; init; } = Array.Empty<CartItem>();
    public decimal TotalPrice { get; init; }
    public int TotalQuantity { get; init; }

    public static CartResponse FromDocument(CartDocument document)
    {
        var items = document.Items;
        var totalPrice = items.Sum(i => i.Price * i.Quantity);
        var totalQuantity = items.Sum(i => i.Quantity);

        return new CartResponse
        {
            Items = items,
            TotalPrice = totalPrice,
            TotalQuantity = totalQuantity
        };
    }
}
