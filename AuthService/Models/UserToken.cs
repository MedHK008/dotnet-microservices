namespace AuthService.Models;

// Stores issued JWT tokens so they can be revoked on logout
public class UserToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int UserId { get; set; }
    public User? User { get; set; }
}
