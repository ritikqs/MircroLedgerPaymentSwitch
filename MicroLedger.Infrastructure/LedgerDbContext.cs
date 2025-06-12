using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain;
using MicroLedger.Domain.Interfaces;

namespace MicroLedger.Infrastructure;

public class LedgerDbContext : DbContext, ILedgerDbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<JournalLine> JournalLines { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<OutboxEvent> OutboxEvents { get; set; }

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

        // Configure OutboxEvent
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired();
            entity.Property(e => e.EventData).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.IsPublished).IsRequired();
            entity.Property(e => e.RetryCount).IsRequired();
        });
    }
} 