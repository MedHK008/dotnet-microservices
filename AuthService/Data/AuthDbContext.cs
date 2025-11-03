using Microsoft.EntityFrameworkCore;
using AuthService.Models;

namespace AuthService.Data;

// Database context for Auth service
public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserToken> UserTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserToken>(builder =>
        {
            builder.Property(token => token.Token)
                .HasMaxLength(450);

            builder.HasIndex(token => token.Token)
                .IsUnique();

            builder.HasOne(token => token.User)
                .WithMany(user => user.Tokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
