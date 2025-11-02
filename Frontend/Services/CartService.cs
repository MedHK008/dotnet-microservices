using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Frontend.Models;

namespace Frontend.Services;

public class CartService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public CartService(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;

        var baseAddress = configuration["Services:CartService"];
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            throw new InvalidOperationException("Cart service base address is not configured.");
        }

        _httpClient.BaseAddress = new Uri(baseAddress);
    }

    public async Task<CartState> GetCartAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdentifier();
        if (userId is null)
        {
            return CartState.Empty;
        }

        var response = await _httpClient.GetAsync($"/api/cart/{Uri.EscapeDataString(userId)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return CartState.Empty;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CartResponse>(_serializerOptions, cancellationToken);
        return payload?.ToState() ?? CartState.Empty;
    }

    public async Task<CartState> AddToCartAsync(Product product, int quantity = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        var userId = EnsureUserIdentifier();
        var request = new CartItemRequest
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Price = product.Price,
            Quantity = quantity
        };

        var response = await _httpClient.PostAsJsonAsync($"/api/cart/{Uri.EscapeDataString(userId)}/items", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CartResponse>(_serializerOptions, cancellationToken);
        return payload?.ToState() ?? CartState.Empty;
    }

    public async Task<CartState> RemoveFromCartAsync(int productId, CancellationToken cancellationToken = default)
    {
        var userId = EnsureUserIdentifier();
        var response = await _httpClient.DeleteAsync($"/api/cart/{Uri.EscapeDataString(userId)}/items/{productId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return CartState.Empty;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CartResponse>(_serializerOptions, cancellationToken);
        return payload?.ToState() ?? CartState.Empty;
    }

    public async Task<CartState> UpdateQuantityAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        var userId = EnsureUserIdentifier();
        var updateRequest = new UpdateCartItemRequest { Quantity = quantity };
        var response = await _httpClient.PutAsJsonAsync($"/api/cart/{Uri.EscapeDataString(userId)}/items/{productId}", updateRequest, _serializerOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return CartState.Empty;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CartResponse>(_serializerOptions, cancellationToken);
        return payload?.ToState() ?? CartState.Empty;
    }

    public async Task ClearCartAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdentifier();
        if (userId is null)
        {
            return;
        }

        var response = await _httpClient.DeleteAsync($"/api/cart/{Uri.EscapeDataString(userId)}", cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private string? GetUserIdentifier()
    {
        var email = _httpContextAccessor.HttpContext?.Request.Cookies["UserEmail"];
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    private string EnsureUserIdentifier()
    {
        var userId = GetUserIdentifier();
        if (userId is null)
        {
            throw new InvalidOperationException("User identifier is not available.");
        }

        return userId;
    }

    private sealed class CartItemRequest
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    private sealed class UpdateCartItemRequest
    {
        public int Quantity { get; set; }
    }

    private sealed class CartResponse
    {
        public List<CartItem> Items { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public int TotalQuantity { get; set; }

        public CartState ToState()
        {
            var items = Items.Select(item => new CartItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Price = item.Price,
                Quantity = item.Quantity
            }).ToList();

            return new CartState(items, TotalPrice, TotalQuantity);
        }
    }

    public sealed class CartState
    {
        public static CartState Empty => new(new List<CartItem>(), 0m, 0);

        public CartState(List<CartItem> items, decimal total, int totalQuantity)
        {
            Items = items;
            Total = total;
            TotalQuantity = totalQuantity;
        }

        public List<CartItem> Items { get; }
        public decimal Total { get; }
        public int TotalQuantity { get; }
    }
}
