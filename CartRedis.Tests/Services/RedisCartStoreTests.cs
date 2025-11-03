using CartRedis.Contracts;
using CartRedis.Models;
using CartRedis.Services;
using Moq;
using StackExchange.Redis;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;
using JsonSerializerOptions = System.Text.Json.JsonSerializerOptions;
using JsonSerializerDefaults = System.Text.Json.JsonSerializerDefaults;

namespace CartRedis.Tests.Services;

public class RedisCartStoreTests
{
    private readonly Mock<IDatabase> _databaseMock = new();
    private readonly RedisCartStore _sut;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public RedisCartStoreTests()
    {
        var connection = Mock.Of<IConnectionMultiplexer>(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()) == _databaseMock.Object);
        _sut = new RedisCartStore(connection);

    _databaseMock.Setup(db => db.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<bool>(),
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task GetCartAsync_ReturnsEmptyCart_WhenKeyMissing()
    {
        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.GetCartAsync("user@example.com");

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task AddOrIncrementItemAsync_PersistsMergedCart()
    {
        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var request = new CartItemRequest
        {
            ProductId = 1,
            ProductName = "Item",
            Quantity = 2,
            Price = 9.99m,
            ImageUrl = "https://example.com/item.jpg"
        };

        var result = await _sut.AddOrIncrementItemAsync("user@example.com", request);

        Assert.Single(result.Items);
        Assert.Equal(2, result.Items.First().Quantity);
        _databaseMock.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(key => key.ToString().Contains("cart:user@example.com")),
            It.IsAny<RedisValue>(),
            null,
            false,
            When.Always,
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_DeletesKey_WhenQuantityZero()
    {
        var existing = new CartDocument
        {
            Items =
            {
                new CartItem
                {
                    ProductId = 1,
                    ProductName = "Item",
                    Quantity = 3,
                    Price = 5m,
                    ImageUrl = "https://example.com/item.jpg"
                }
            }
        };

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonSerializer.Serialize(existing, _jsonOptions));

        _databaseMock.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(true)
            .Verifiable();

        _databaseMock.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, When.Always, CommandFlags.None))
            .ReturnsAsync(true);

        var result = await _sut.UpdateItemQuantityAsync("user@example.com", 1, 0);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        _databaseMock.Verify();
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesEntry_WhenProductExists()
    {
        var existing = new CartDocument
        {
            Items =
            {
                new CartItem { ProductId = 1, ProductName = "One", Quantity = 1, Price = 5, ImageUrl = "https://example.com/one.jpg" },
                new CartItem { ProductId = 2, ProductName = "Two", Quantity = 1, Price = 10, ImageUrl = "https://example.com/two.jpg" }
            }
        };

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonSerializer.Serialize(existing, _jsonOptions));

        var removed = await _sut.RemoveItemAsync("user@example.com", 1);

        Assert.True(removed);
        _databaseMock.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.Is<RedisValue>(value => value.ToString().Contains("\"productId\":2")),
            null,
            false,
            When.Always,
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ClearCartAsync_DeletesKey()
    {
        _databaseMock.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(true)
            .Verifiable();

        await _sut.ClearCartAsync("user@example.com");

        _databaseMock.Verify();
    }

    [Fact]
    public async Task AddOrIncrementItemAsync_UpdatesImageUrl_WhenProductExists()
    {
        var existing = new CartDocument
        {
            Items =
            {
                new CartItem
                {
                    ProductId = 1,
                    ProductName = "Item",
                    Quantity = 2,
                    Price = 9.99m,
                    ImageUrl = "https://example.com/old-image.jpg"
                }
            }
        };

        _databaseMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonSerializer.Serialize(existing, _jsonOptions));

        var request = new CartItemRequest
        {
            ProductId = 1,
            ProductName = "Item Updated",
            Quantity = 3,
            Price = 12.99m,
            ImageUrl = "https://example.com/new-image.jpg"
        };

        var result = await _sut.AddOrIncrementItemAsync("user@example.com", request);

        Assert.Single(result.Items);
        var item = result.Items.First();
        Assert.Equal(5, item.Quantity); // 2 + 3
        Assert.Equal(12.99m, item.Price);
        Assert.Equal("Item Updated", item.ProductName);
        Assert.Equal("https://example.com/new-image.jpg", item.ImageUrl);
    }
}
