namespace AuthService.DTOs;

// Request model for user registration
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Request model for user login
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Response model after successful authentication
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// Request model for validating an existing JWT token
public class TokenValidationRequest
{
    public string Token { get; set; } = string.Empty;
}

// Request model for logging out a user and revoking their token
public class LogoutRequest
{
    public string Token { get; set; } = string.Empty;
}
