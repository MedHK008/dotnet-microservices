using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using AuthService.Data;
using AuthService.Services;
using AuthService.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Add database context with SQL Server
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register authentication service
builder.Services.AddScoped<AuthenticationService>();

// Add CORS to allow frontend to call this service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// Apply migrations and create database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.Migrate();
}

app.UseCors("AllowAll");
app.UseAuthorization();

// Register endpoint - creates new user account
app.MapPost("/api/auth/register", async (RegisterRequest request, AuthenticationService authService) =>
{
    var result = await authService.RegisterAsync(request);
    return result != null ? Results.Ok(result) : Results.BadRequest("User already exists");
});

// Login endpoint - authenticates user and returns JWT token
app.MapPost("/api/auth/login", async (LoginRequest request, AuthenticationService authService) =>
{
    var result = await authService.LoginAsync(request);
    return result != null ? Results.Ok(result) : Results.Unauthorized();
});

// Validate token endpoint - checks if JWT token is valid
app.MapPost("/api/auth/validate", async ([FromBody] TokenValidationRequest request, AuthenticationService authService) =>
{
    var isValid = await authService.ValidateTokenAsync(request.Token);
    return isValid ? Results.Ok(new { valid = true }) : Results.Unauthorized();
});

// Logout endpoint - revokes token by removing it from the database
app.MapPost("/api/auth/logout", async ([FromBody] LogoutRequest request, AuthenticationService authService) =>
{
    var loggedOut = await authService.LogoutAsync(request.Token);
    return loggedOut ? Results.Ok(new { loggedOut = true }) : Results.NotFound("Token not found");
});

app.Run();
