using System.Text.Json;
using System.Text.Json.Serialization;
using CartRedis.Contracts;
using CartRedis.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Redis");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Redis connection string is not configured.");
    }

    var options = ConfigurationOptions.Parse(connectionString, true);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});

builder.Services.AddSingleton<ICartStore, RedisCartStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.MapGet("/api/cart/{userId}", async (string userId, ICartStore store, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "User identifier is required." });
    }

    var cart = await store.GetCartAsync(userId, cancellationToken);
    return Results.Ok(CartResponse.FromDocument(cart));
})
.WithName("GetCart")
.WithOpenApi();

app.MapPost("/api/cart/{userId}/items", async (string userId, CartItemRequest request, ICartStore store, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "User identifier is required." });
    }

    if (request is null)
    {
        return Results.BadRequest(new { message = "Request payload is required." });
    }

    if (request.Quantity <= 0)
    {
        return Results.BadRequest(new { message = "Quantity must be greater than zero." });
    }

    var cart = await store.AddOrIncrementItemAsync(userId, request, cancellationToken);
    return Results.Ok(CartResponse.FromDocument(cart));
})
.WithName("AddCartItem")
.WithOpenApi();

app.MapPut("/api/cart/{userId}/items/{productId:int}", async (string userId, int productId, UpdateCartItemRequest request, ICartStore store, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "User identifier is required." });
    }

    if (request is null)
    {
        return Results.BadRequest(new { message = "Request payload is required." });
    }

    if (request.Quantity < 0)
    {
        return Results.BadRequest(new { message = "Quantity cannot be negative." });
    }

    var cart = await store.UpdateItemQuantityAsync(userId, productId, request.Quantity, cancellationToken);
    if (cart is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(CartResponse.FromDocument(cart));
})
.WithName("UpdateCartItem")
.WithOpenApi();

app.MapDelete("/api/cart/{userId}/items/{productId:int}", async (string userId, int productId, ICartStore store, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "User identifier is required." });
    }

    var removed = await store.RemoveItemAsync(userId, productId, cancellationToken);
    if (!removed)
    {
        return Results.NotFound();
    }

    var cart = await store.GetCartAsync(userId, cancellationToken);
    return Results.Ok(CartResponse.FromDocument(cart));
})
.WithName("RemoveCartItem")
.WithOpenApi();

app.MapDelete("/api/cart/{userId}", async (string userId, ICartStore store, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "User identifier is required." });
    }

    await store.ClearCartAsync(userId, cancellationToken);
    return Results.NoContent();
})
.WithName("ClearCart")
.WithOpenApi();

app.MapGet("/health", () => Results.Ok())
.WithName("HealthCheck");

app.Run();
