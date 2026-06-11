using Microsoft.EntityFrameworkCore;

namespace Exchange.Service.Data;

public class ExchangeDbContext : DbContext
{
    public ExchangeDbContext(DbContextOptions<ExchangeDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Balance> Balances => Set<Balance>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(64).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.PasswordSalt).IsRequired();
        });

        modelBuilder.Entity<Balance>(e =>
        {
            e.HasIndex(b => new { b.UserId, b.CurrencyCode }).IsUnique();
            e.Property(b => b.CurrencyCode).HasMaxLength(3).IsRequired();
            e.HasOne(b => b.User)
             .WithMany(u => u.Balances)
             .HasForeignKey(b => b.UserId);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.Property(t => t.CurrencyCode).HasMaxLength(3).IsRequired();
            e.Property(t => t.Type).HasConversion<string>().HasMaxLength(16);
            e.HasOne(t => t.User)
             .WithMany(u => u.Transactions)
             .HasForeignKey(t => t.UserId);
        });
    }
}
