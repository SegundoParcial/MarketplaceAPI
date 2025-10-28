
using Microsoft.EntityFrameworkCore;
using MarketplaceAPI.Models;

namespace MarketplaceAPI.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Review> Reviews => Set<Review>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Order>().Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Entity<Review>().HasIndex(r => new { r.ProductId, r.CustomerUserId }).IsUnique();
        b.Entity<Product>().HasIndex(p => p.Price);
        b.Entity<Order>().Property(o => o.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Entity<Review>().Property(r => r.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        base.OnModelCreating(b);
    }
}
