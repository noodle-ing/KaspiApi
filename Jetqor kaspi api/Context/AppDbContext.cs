using Microsoft.EntityFrameworkCore;

namespace Jetqor_kaspi_api;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    public DbSet<Order> Orders { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .Property(o => o.kaspi_status)
            .HasConversion<string>();

        modelBuilder.Entity<Order>()
            .Property(o => o.status)
            .HasConversion<string>();
    }
}