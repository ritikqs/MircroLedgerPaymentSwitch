using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain;

namespace MicroLedger.Domain.Interfaces;

public interface ILedgerDbContext
{
    DbSet<Account> Accounts { get; set; }
    DbSet<Transaction> Transactions { get; set; }
    DbSet<JournalLine> JournalLines { get; set; }
    DbSet<User> Users { get; set; }
    DbSet<OutboxEvent> OutboxEvents { get; set; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
} 