using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain;

namespace MicroLedger.Infrastructure;

public class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<JournalLine> JournalLines { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed test users for JWT auth
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Username = "admin", Password = "admin123", Role = "BankAdmin" },
            new User { Id = 2, Username = "customer", Password = "customer123", Role = "Customer" }
        );

        // Configure Transaction
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reference).IsRequired();
            entity.Property(e => e.TimestampUtc).IsRequired();
        });

        // Configure JournalLine
        modelBuilder.Entity<JournalLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Debit).HasPrecision(18, 2);
            entity.Property(e => e.Credit).HasPrecision(18, 2);
            entity.Property(e => e.AccountId).IsRequired();
            entity.Property(e => e.TransactionId).IsRequired();

            entity.HasOne(j => j.Transaction)
                .WithMany(t => t.JournalLines)
                .HasForeignKey(j => j.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
} 