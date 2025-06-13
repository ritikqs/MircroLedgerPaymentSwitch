using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain;
using MicroLedger.Domain.Interfaces;


namespace MicroLedger.Infrastructure;

public class LedgerDbContext : DbContext, ILedgerDbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<JournalLine> JournalLines { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<OutboxEvent> OutboxEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.OwnerName).IsRequired();
            entity.Property(e => e.OwnerId).IsRequired();
            entity.Property(e => e.Currency).IsRequired();
            entity.Property(e => e.InterestRate).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.Reference).IsRequired();
            entity.HasMany(e => e.JournalLines)
                .WithOne(e => e.Transaction)
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JournalLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.TransactionId).IsRequired();
            entity.Property(e => e.AccountId).IsRequired();
            entity.Property(e => e.Debit).HasPrecision(18, 2);
            entity.Property(e => e.Credit).HasPrecision(18, 2);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Username).IsRequired();
            entity.Property(e => e.Password).IsRequired();
            entity.Property(e => e.Role).IsRequired();
        });

        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.EventType).IsRequired();
            entity.Property(e => e.EventData).IsRequired();
            entity.Property(e => e.TimestampUtc).IsRequired();
        });
    }
} 