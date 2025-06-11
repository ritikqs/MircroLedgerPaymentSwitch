using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain;

namespace MicroLedger.Infrastructure;

public class LedgerDbContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<JournalLine> JournalLines { get; set; }
    
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JournalLine>()
            .HasOne(j => j.Transaction)
            .WithMany(t => t.JournalLines)
            .HasForeignKey(j => j.TransactionId);
    }
} 