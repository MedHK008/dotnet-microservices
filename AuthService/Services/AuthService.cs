using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;

namespace AuthService.Services;

public class AuthenticationService
{
    private readonly AuthDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthenticationService(AuthDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // Register a new user
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        // Check if user already exists
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return null;
        }

        // Hash the password
        var passwordHash = HashPassword(request.Password);

        // Create new user
        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash
        };

        // Save user to database
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Issue and persist JWT token
        var token = await IssueTokenAsync(user);

        // return response
        return new AuthResponse
        {
            Token = token,
            Email = user.Email
        };
    }

    // Login an existing user
    // TODO: Implement single session for the same user authenticated via multiple devices
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        // Find user by email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        // Verify password
        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            return null;
        }

        // Issue and persist JWT token
        var token = await IssueTokenAsync(user);
        // return response
        return new AuthResponse
        {
            Token = token,
            Email = user.Email
        };
    }

    // Validate JWT token
    public async Task<bool> ValidateTokenAsync(string token)
    {
        // Check if token is null or empty
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }
        // Check if token exists in the database
        if (!await _context.UserTokens.AsNoTracking().AnyAsync(t => t.Token == token))
        {
            return false;
        }

        try
        {
            // Validate token
            var tokenHandler = new JwtSecurityTokenHandler();
            // Get the secret key
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
            // Validate the token
            // Validate the JWT token using the specified validation parameters
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                // Ensure the signing key is valid
                ValidateIssuerSigningKey = true,
                // Specify the key used to sign the token
                IssuerSigningKey = new SymmetricSecurityKey(key),
                // Validate the issuer of the token
                ValidateIssuer = true,
                // Set the expected issuer value
                ValidIssuer = _configuration["Jwt:Issuer"],
                // Validate the audience of the token
                ValidateAudience = true,
                // Set the expected audience value
                ValidAudience = _configuration["Jwt:Audience"],
                // Ensure the token has not expired
                ValidateLifetime = true,
                // No clock skew allowed for token expiration
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken); // Output the validated token if successful

            return true;
        }
        catch
        {
            return false;
        }
    }

    // Logout user by removing token from the database
    public async Task<bool> LogoutAsync(string token)
    {
        // check if the token in null or empty
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }
        // retrieve the token from the db
        var storedToken = await _context.UserTokens.FirstOrDefaultAsync(t => t.Token == token);
        // check if the retrieved value is null
        if (storedToken == null)
        {
            return false;
        }
        // remove the token from the db
        _context.UserTokens.Remove(storedToken);
        // apply change
        await _context.SaveChangesAsync();
        return true;
    }

    // Generate JWT token for authenticated user
    private string GenerateJwtToken(User user)
    {
        
        // Get the secret key from configuration and convert it to bytes
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
        
        // Create a token descriptor that defines the token's properties
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            // Define the claims (user information) to include in the token
            Subject = new ClaimsIdentity(new[]
            {
            // Add user ID claim
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            // Add email claim
            new Claim(ClaimTypes.Email, user.Email)
            }),
            // Set token expiration to 7 days from now
            Expires = DateTime.UtcNow.AddDays(7),
            // Set the token issuer from configuration
            Issuer = _configuration["Jwt:Issuer"],
            // Set the token audience from configuration
            Audience = _configuration["Jwt:Audience"],
            // Configure the signing credentials using HMAC SHA256 algorithm
            SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature)
        };

        // Create a token handler to generate the JWT token
        var tokenHandler = new JwtSecurityTokenHandler();
        // Generate the token using the descriptor
        var token = tokenHandler.CreateToken(tokenDescriptor);
        // Convert the token to a string and return it
        return tokenHandler.WriteToken(token);
    }

    //  generates a JWT token and saves it to the database
    private async Task<string> IssueTokenAsync(User user)
    {
        // 1. Generate a new JWT token for the user
        var token = GenerateJwtToken(user);

        // 2. Find all existing tokens for this user (but doesn't execute query yet)
        var existingTokens = _context.UserTokens.Where(t => t.UserId == user.Id);
        
        // 3. Delete all existing tokens (enforces single active session)
        _context.UserTokens.RemoveRange(existingTokens);

        // 4. Add the new token to the database
        _context.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Token = token,
            CreatedAt = DateTime.UtcNow
        });

        // 5. Save all changes to the database
        await _context.SaveChangesAsync();

        // 6. Return the token string
        return token;
    }

    // Hash password using SHA256
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    // Verify password against stored hash
    private bool VerifyPassword(string password, string storedHash)
    {
        // hash the value since the password persisted is hashed too
        var hash = HashPassword(password);
        return hash == storedHash;
    }
}
