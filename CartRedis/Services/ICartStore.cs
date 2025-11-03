using CartRedis.Contracts;
using CartRedis.Models;

namespace CartRedis.Services;

public interface ICartStore
{
    Task<CartDocument> GetCartAsync(string userId, CancellationToken cancellationToken = default);
    Task<CartDocument> AddOrIncrementItemAsync(string userId, CartItemRequest request, CancellationToken cancellationToken = default);
    Task<CartDocument?> UpdateItemQuantityAsync(string userId, int productId, int quantity, CancellationToken cancellationToken = default);
    Task<bool> RemoveItemAsync(string userId, int productId, CancellationToken cancellationToken = default);
    Task ClearCartAsync(string userId, CancellationToken cancellationToken = default);
}
