using System;
using System.Linq;
using System.Text.Json;
using CartRedis.Contracts;
using CartRedis.Models;
using StackExchange.Redis;

namespace CartRedis.Services;

public class RedisCartStore : ICartStore
{
    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public RedisCartStore(IConnectionMultiplexer connection)
    {
        _database = connection.GetDatabase();
    }

    public async Task<CartDocument> GetCartAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stored = await _database.StringGetAsync(BuildKey(userId)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (stored.IsNullOrEmpty)
        {
            return new CartDocument();
        }

        return JsonSerializer.Deserialize<CartDocument>(stored!, _jsonOptions) ?? new CartDocument();
    }

    public async Task<CartDocument> AddOrIncrementItemAsync(string userId, CartItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        var cart = await GetCartAsync(userId, cancellationToken).ConfigureAwait(false);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);

        if (item is null)
        {
            cart.Items.Add(new CartItem
            {
                ProductId = request.ProductId,
                ProductName = request.ProductName,
                Price = request.Price,
                Quantity = request.Quantity,
                ImageUrl = request.ImageUrl
            });
        }
        else
        {
            item.Quantity += request.Quantity;
            item.Price = request.Price;
            item.ProductName = request.ProductName;
            item.ImageUrl = request.ImageUrl;
        }

        await PersistAsync(userId, cart, cancellationToken).ConfigureAwait(false);
        return cart;
    }

    public async Task<CartDocument?> UpdateItemQuantityAsync(string userId, int productId, int quantity, CancellationToken cancellationToken = default)
    {
        var cart = await GetCartAsync(userId, cancellationToken).ConfigureAwait(false);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
        {
            return null;
        }

        if (quantity <= 0)
        {
            cart.Items.Remove(item);
        }
        else
        {
            item.Quantity = quantity;
        }

        await PersistAsync(userId, cart, cancellationToken).ConfigureAwait(false);
        return cart;
    }

    public async Task<bool> RemoveItemAsync(string userId, int productId, CancellationToken cancellationToken = default)
    {
        var cart = await GetCartAsync(userId, cancellationToken).ConfigureAwait(false);
        var removed = cart.Items.RemoveAll(i => i.ProductId == productId) > 0;
        if (!removed)
        {
            return false;
        }

        await PersistAsync(userId, cart, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task ClearCartAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.KeyDeleteAsync(BuildKey(userId)).ConfigureAwait(false);
    }

    private static string BuildKey(string userId) => $"cart:{userId.Trim().ToLowerInvariant()}";

    private async Task PersistAsync(string userId, CartDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildKey(userId);

        if (document.Items.Count == 0)
        {
            await _database.KeyDeleteAsync(key).ConfigureAwait(false);
            return;
        }

        var payload = JsonSerializer.Serialize(document, _jsonOptions);
        await _database.StringSetAsync(key, payload).ConfigureAwait(false);
    }
}
