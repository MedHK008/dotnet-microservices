// Import base System namespace for core runtime types and helpers.
using System;
// Bring in LINQ extensions such as FirstOrDefault used for collection queries.
using System.Linq;
// Provide JSON serialization support with configurable serializer options.
using System.Text.Json;
// Reference DTO contracts for cart operations coming from higher layers.
using CartRedis.Contracts;
// Access the domain models representing cart documents and items.
using CartRedis.Models;
// Use StackExchange.Redis for interacting with the Redis database.
using StackExchange.Redis;

// Declare the namespace containing Redis-backed cart services.
namespace CartRedis.Services;

// Define a cart store implementation that persists carts in Redis via ICartStore interface.
public class RedisCartStore : ICartStore
{
    // Hold a reference to the Redis database abstraction returned by the multiplexer.
    private readonly IDatabase _database;
    // Configure JSON serialization with web-friendly defaults for consistent payloads.
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    // Construct the store by requesting a database instance from the Redis connection multiplexer.
    public RedisCartStore(IConnectionMultiplexer connection)
    {
        // Resolve the default logical database used to store cart documents.
        _database = connection.GetDatabase();
    }

    // Retrieve a user's cart from Redis or return a new empty cart when none is stored.
    public async Task<CartDocument> GetCartAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Ensure the operation halts promptly if the caller cancels it.
        cancellationToken.ThrowIfCancellationRequested();
        // Fetch the raw cart JSON string from Redis using the computed key.
        var stored = await _database.StringGetAsync(BuildKey(userId)).ConfigureAwait(false);
        // Respect cancellation that might occur after the read completes.
        cancellationToken.ThrowIfCancellationRequested();

        // If Redis has no entry for this user then return a fresh empty cart document.
        if (stored.IsNullOrEmpty)
        {
            return new CartDocument();
        }

        // Deserialize the JSON back into a CartDocument, falling back to an empty instance if deserialization fails.
        return JsonSerializer.Deserialize<CartDocument>(stored!, _jsonOptions) ?? new CartDocument();
    }

    // Add a new item to the cart or increment an existing item's quantity and then persist the change.
    public async Task<CartDocument> AddOrIncrementItemAsync(string userId, CartItemRequest request, CancellationToken cancellationToken = default)
    {
        // Guard against invalid requests that specify zero or negative quantities.
        if (request.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        // Load the current cart state for the specified user.
        var cart = await GetCartAsync(userId, cancellationToken).ConfigureAwait(false);
        // Attempt to find an existing item with the same product identifier.
        var item = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);

        // If the item does not already exist, create and append a new cart item entry.
        if (item is null)
        {
            cart.Items.Add(new CartItem
            {
                // Copy the product identifier provided in the request.
                ProductId = request.ProductId,
                // Capture the user-facing product name for display.
                ProductName = request.ProductName,
                // Store the unit price for pricing calculations.
                Price = request.Price,
                // Initialize the quantity to the requested amount.
                Quantity = request.Quantity,
                // Persist the image URL for UI rendering.
                ImageUrl = request.ImageUrl
            });
        }
        else
        {
            // Increase the existing quantity by the amount requested.
            item.Quantity += request.Quantity;
            // Refresh the price in case it changed since the item was added.
            item.Price = request.Price;
            // Update the stored product name in case it has been modified.
            item.ProductName = request.ProductName;
            // Replace the image URL with the latest value supplied.
            item.ImageUrl = request.ImageUrl;
        }

        // Persist the updated cart back into Redis to make the change durable.
        await PersistAsync(userId, cart, cancellationToken).ConfigureAwait(false);
        // Return the updated cart to the caller.
        return cart;
    }

    // Update an existing cart item's quantity or remove it entirely if the new quantity is non-positive.
    public async Task<CartDocument?> UpdateItemQuantityAsync(string userId, int productId, int quantity, CancellationToken cancellationToken = default)
    {
        // Pull the current cart document for the user.
        var cart = await GetCartAsync(userId, cancellationToken).ConfigureAwait(false);
        // Locate the item whose quantity needs to be adjusted.
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        // If the item is absent, report that no update occurred by returning null.
        if (item is null)
        {
            return null;
        }

        // When the new quantity is zero or negative, remove the item entirely from the cart.
        if (quantity <= 0)
        {
            cart.Items.Remove(item);
        }
        else
        {
            // Otherwise, set the item's quantity to the requested value.
            item.Quantity = quantity;
        }

        // Save the modified cart back to Redis to reflect the quantity change.
        await PersistAsync(userId, cart, cancellationToken).ConfigureAwait(false);
        // Return the updated cart snapshot so callers see the final state.
        return cart;
    }

    // Remove the specified item from the user's cart and indicate whether anything was deleted.
    public async Task<bool> RemoveItemAsync(string userId, int productId, CancellationToken cancellationToken = default)
    {
        // Retrieve the user's cart data before attempting removal.
        var cart = await GetCartAsync(userId, cancellationToken).ConfigureAwait(false);
        // Attempt to remove any items with the matching product ID, capturing whether something changed.
        var removed = cart.Items.RemoveAll(i => i.ProductId == productId) > 0;
        // If no items were removed, report failure without persisting.
        if (!removed)
        {
            return false;
        }

        // Persist the updated cart so the deletion is reflected in Redis.
        await PersistAsync(userId, cart, cancellationToken).ConfigureAwait(false);
        // Confirm to the caller that the item was successfully removed.
        return true;
    }

    // Delete the entire cart entry for a user by wiping the Redis key.
    public async Task ClearCartAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Honor cancellation before contacting Redis.
        cancellationToken.ThrowIfCancellationRequested();
        // Remove the cart key entirely so subsequent reads return an empty cart.
        await _database.KeyDeleteAsync(BuildKey(userId)).ConfigureAwait(false);
    }

    // Build the canonical Redis key for a user by normalizing the user identifier.
    private static string BuildKey(string userId) => $"cart:{userId.Trim().ToLowerInvariant()}";

    // Serialize and store the cart in Redis or delete the key when the cart is empty.
    private async Task PersistAsync(string userId, CartDocument document, CancellationToken cancellationToken)
    {
        // Respect cancellation to avoid unnecessary Redis operations when aborted.
        cancellationToken.ThrowIfCancellationRequested();
        // Derive the Redis key used to store this user's cart.
        var key = BuildKey(userId);

        // If the cart no longer has items, delete the key so Redis does not hold empty data.
        if (document.Items.Count == 0)
        {
            await _database.KeyDeleteAsync(key).ConfigureAwait(false);
            return;
        }

        // Serialize the cart document to JSON using the configured options.
        var payload = JsonSerializer.Serialize(document, _jsonOptions);
        // Write the JSON payload back into Redis so the cart is up to date.
        await _database.StringSetAsync(key, payload).ConfigureAwait(false);
    }
}
