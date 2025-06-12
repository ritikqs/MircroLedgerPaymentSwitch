using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain;

namespace MicroLedger.Infrastructure;

public static class TestDataSeeder
{
    public static async Task SeedTestData(LedgerDbContext db)
    {
        // Check if we already have accounts
        if (await db.Accounts.AnyAsync())
        {
            return;
        }

        // Create test accounts
        var accounts = new[]
        {
            new Account
            {
                Id = "ACC001",
                Type = AccountType.Checking,
                Currency = "USD",
                OwnerId = "user1",
                OwnerName = "John Doe"
            },
            new Account
            {
                Id = "ACC002",
                Type = AccountType.Checking,
                Currency = "USD",
                OwnerId = "user2",
                OwnerName = "Jane Smith"
            },
            new Account
            {
                Id = "BANK_CAPITAL",
                Type = AccountType.System,
                Currency = "USD",
                OwnerId = "system",
                OwnerName = "Bank System"
            }
        };

        db.Accounts.AddRange(accounts);

        // Create initial balance transaction
        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            Reference = "Initial Balance",
            TimestampUtc = DateTime.UtcNow
        };

        db.Transactions.Add(transaction);

        // Create journal lines for initial balance
        db.JournalLines.AddRange(
            new JournalLine
            {
                Id = Guid.NewGuid().ToString(),
                TransactionId = transaction.Id,
                AccountId = "ACC001",
                Debit = 0,
                Credit = 1000.00m
            },
            new JournalLine
            {
                Id = Guid.NewGuid().ToString(),
                TransactionId = transaction.Id,
                AccountId = "BANK_CAPITAL",
                Debit = 1000.00m,
                Credit = 0
            }
        );

        await db.SaveChangesAsync();
    }
} 