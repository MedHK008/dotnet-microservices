namespace AuthService.Models;

// User model for storing user credentials
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UserToken> Tokens { get; set; } = new List<UserToken>();
}
