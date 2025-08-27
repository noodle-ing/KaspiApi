using Jetqor_kaspi_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jetqor_kaspi_api;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Storage> Storages { get; set; }
    public DbSet<OrderProduct> OrderProducts { get; set; }
    public DbSet<Entity> Entities { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .Property(o => o.kaspi_status)
            .HasConversion<string>();

        modelBuilder.Entity<Order>()
            .Property(o => o.status)
            .HasConversion<string>();
        
        modelBuilder.Entity<OrderProduct>()
            .HasKey(op => op.id);

        modelBuilder.Entity<OrderProduct>()
            .HasOne(op => op.Order)
            .WithMany(o => o.OrderProducts)
            .HasForeignKey(op => op.orderId);

        modelBuilder.Entity<OrderProduct>()
            .HasOne(op => op.Product)
            .WithMany(p => p.OrderProducts)
            .HasForeignKey(op => op.productId);
    }
}