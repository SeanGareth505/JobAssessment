using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLineItem> OrderLineItems => Set<OrderLineItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.CountryCode).HasMaxLength(2).IsUnicode(false);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CurrencyCode).HasMaxLength(3).IsUnicode(false);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.CustomerId, x.Status, x.CreatedAt }).HasDatabaseName("IX_Orders_CustomerId_Status_CreatedAt");
            e.HasOne(x => x.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderLineItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ProductSku).HasMaxLength(200);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasOne(x => x.Order)
                .WithMany(o => o.LineItems)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(200);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.Sku).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AggregateType).HasMaxLength(128);
            e.Property(x => x.Type).HasMaxLength(128);
            e.Property(x => x.Payload).HasMaxLength(int.MaxValue);
        });
    }
}
