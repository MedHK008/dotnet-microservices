using Xunit;
using Microsoft.EntityFrameworkCore;
using AuthService.Data;
using AuthService.Services;
using AuthService.DTOs;
using AuthService.Models;
using Microsoft.Extensions.Configuration;
using Moq;

namespace AuthService.Tests;

public class AuthenticationServiceTests
{
    private readonly DbContextOptions<AuthDbContext> _dbContextOptions;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public AuthenticationServiceTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["Jwt:Key"]).Returns("this-is-a-very-long-secret-key-for-jwt-token-generation-and-validation");
        _mockConfiguration.Setup(x => x["Jwt:Issuer"]).Returns("MicroserviceIssuer");
        _mockConfiguration.Setup(x => x["Jwt:Audience"]).Returns("MicroserviceAudience");
    }

    private AuthDbContext CreateContext()
    {
        return new AuthDbContext(_dbContextOptions);
    }

    #region Register Tests

    [Fact]
    public async Task RegisterAsync_WithValidRequest_ShouldCreateUserAndReturnToken()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var request = new RegisterRequest 
        { 
            Email = "test@example.com", 
            Password = "password123" 
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Email);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ShouldReturnNull()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        
        var request1 = new RegisterRequest 
        { 
            Email = "test@example.com", 
            Password = "password123" 
        };

        var request2 = new RegisterRequest 
        { 
            Email = "test@example.com", 
            Password = "differentpassword" 
        };

        // Act
        var result1 = await service.RegisterAsync(request1);
        var result2 = await service.RegisterAsync(request2);

        // Assert
        Assert.NotNull(result1);
        Assert.Null(result2);
    }

    [Fact]
    public async Task RegisterAsync_ShouldPersistUserToDatabase()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var request = new RegisterRequest 
        { 
            Email = "persist@example.com", 
            Password = "password123" 
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        var persistedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "persist@example.com");
        Assert.NotNull(persistedUser);
        Assert.Equal("persist@example.com", persistedUser.Email);
    }

    [Theory]
    [InlineData("user1@test.com")]
    [InlineData("user2@test.com")]
    [InlineData("user3@test.com")]
    public async Task RegisterAsync_MultipleUsers_ShouldRegisterAllSuccessfully(string email)
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var request = new RegisterRequest 
        { 
            Email = email, 
            Password = "password123" 
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ShouldReturnToken()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        
        var email = "login@example.com";
        var password = "correct_password";
        
        var registerRequest = new RegisterRequest { Email = email, Password = password };
        await service.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest { Email = email, Password = password };

        // Act
        var result = await service.LoginAsync(loginRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ShouldReturnNull()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        
        var email = "login@example.com";
        var password = "correct_password";
        
        var registerRequest = new RegisterRequest { Email = email, Password = password };
        await service.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest { Email = email, Password = "wrong_password" };

        // Act
        var result = await service.LoginAsync(loginRequest);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithNonexistentUser_ShouldReturnNull()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        
        var loginRequest = new LoginRequest 
        { 
            Email = "nonexistent@example.com", 
            Password = "password123" 
        };

        // Act
        var result = await service.LoginAsync(loginRequest);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("user@test.com", "pass123")]
    [InlineData("another@test.com", "securepass")]
    public async Task LoginAsync_WithMultipleUsers_ShouldAuthenticateCorrectly(string email, string password)
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        
        var registerRequest = new RegisterRequest { Email = email, Password = password };
        await service.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest { Email = email, Password = password };

        // Act
        var result = await service.LoginAsync(loginRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
    }

    #endregion

    #region Password Hashing Tests

    [Fact]
    public async Task RegisterAsync_ShouldHashPassword()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var request = new RegisterRequest 
        { 
            Email = "hash@example.com", 
            Password = "mypassword" 
        };

        // Act
        await service.RegisterAsync(request);

        // Assert
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == "hash@example.com");
        Assert.NotNull(user);
        Assert.NotEqual("mypassword", user.PasswordHash);
    }

    [Fact]
    public async Task LoginAsync_ShouldVerifyPasswordCorrectly()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var password = "secure_password_123";
        
        var registerRequest = new RegisterRequest 
        { 
            Email = "verify@example.com", 
            Password = password 
        };
        await service.RegisterAsync(registerRequest);

        // Act - Login with correct password
        var loginRequest = new LoginRequest 
        { 
            Email = "verify@example.com", 
            Password = password 
        };
        var result = await service.LoginAsync(loginRequest);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region Token Tests

    [Fact]
    public async Task RegisterAsync_ShouldReturnValidJwtToken()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var request = new RegisterRequest 
        { 
            Email = "token@example.com", 
            Password = "password123" 
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        Assert.NotNull(result?.Token);
        Assert.NotEmpty(result.Token);
        // JWT tokens typically have 3 parts separated by dots
        Assert.Equal(3, result.Token.Split('.').Length);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnValidJwtToken()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        
        var registerRequest = new RegisterRequest 
        { 
            Email = "logintoken@example.com", 
            Password = "password123" 
        };
        await service.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest 
        { 
            Email = "logintoken@example.com", 
            Password = "password123" 
        };

        // Act
        var result = await service.LoginAsync(loginRequest);

        // Assert
        Assert.NotNull(result?.Token);
        Assert.NotEmpty(result.Token);
        Assert.Equal(3, result.Token.Split('.').Length);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithValidToken_ShouldReturnTrue()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        
        var registerRequest = new RegisterRequest 
        { 
            Email = "validate@example.com", 
            Password = "password123" 
        };
        var registerResult = await service.RegisterAsync(registerRequest);

        // Act
        var isValid = await service.ValidateTokenAsync(registerResult!.Token);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithInvalidToken_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var invalidToken = "invalid.token.here";

        // Act
        var isValid = await service.ValidateTokenAsync(invalidToken);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithEmptyToken_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);

        // Act
        var isValid = await service.ValidateTokenAsync(string.Empty);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task LogoutAsync_WithValidToken_ShouldRevokeToken()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var registerRequest = new RegisterRequest
        {
            Email = "logout@example.com",
            Password = "password123"
        };

        var authResponse = await service.RegisterAsync(registerRequest);

        // Act
        var logoutResult = await service.LogoutAsync(authResponse!.Token);
        var validateAfterLogout = await service.ValidateTokenAsync(authResponse.Token);

        // Assert
        Assert.True(logoutResult);
        Assert.False(validateAfterLogout);
    }

    [Fact]
    public async Task LogoutAsync_WithUnknownToken_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);

        // Act
        var result = await service.LogoutAsync("does-not-exist");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task RegisterAsync_WithEmptyEmail_ShouldStillCreateUser()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var request = new RegisterRequest 
        { 
            Email = "", 
            Password = "password123" 
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task LoginAsync_WithEmptyEmail_ShouldReturnNull()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var loginRequest = new LoginRequest 
        { 
            Email = "", 
            Password = "password123" 
        };

        // Act
        var result = await service.LoginAsync(loginRequest);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_WithLongPassword_ShouldSucceed()
    {
        // Arrange
        var context = CreateContext();
        var service = new AuthenticationService(context, _mockConfiguration.Object);
        var longPassword = new string('a', 1000);
        var request = new RegisterRequest 
        { 
            Email = "long@example.com", 
            Password = longPassword 
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("long@example.com", result.Email);
    }

    #endregion
}
