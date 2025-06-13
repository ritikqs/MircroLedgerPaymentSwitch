using System;
using System.Threading.Tasks;
using MicroLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace MicroLedger.Infrastructure;

public static class TestDataSeeder
{
    public static async Task SeedTestDataAsync(LedgerDbContext dbContext)
    {
        if (await dbContext.Users.AnyAsync())
        {
            return; // Data already seeded
        }

        // Create test users
        var adminUser = new User
        {
            Username = "admin",
            Password = "admin123", // In production, this should be hashed
            Role = "BankAdmin"
        };

        var customerUser = new User
        {
            Username = "customer",
            Password = "customer123", // In production, this should be hashed
            Role = "Customer"
        };

        await dbContext.Users.AddRangeAsync(adminUser, customerUser);
        await dbContext.SaveChangesAsync();

        // Create test accounts
        var checkingAccount = new Account
        {
            OwnerName = "John Doe",
            OwnerId = customerUser.Id,
            Currency = "USD",
            Type = AccountType.Checking,
            Balance = 1000.00m
        };

        var savingsAccount = new Account
        {
            OwnerName = "John Doe",
            OwnerId = customerUser.Id,
            Currency = "USD",
            Type = AccountType.Savings,
            Balance = 5000.00m,
            InterestRate = 0.05m // 5% annual interest rate
        };

        await dbContext.Accounts.AddRangeAsync(checkingAccount, savingsAccount);
        await dbContext.SaveChangesAsync();

        // Create a test transaction
        var transaction = new Transaction
        {
            Reference = "Initial Deposit"
        };

        await dbContext.Transactions.AddAsync(transaction);
        await dbContext.SaveChangesAsync();

        // Create journal lines
        var debitLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = "BANK_CAPITAL", // System account for bank's capital
            Debit = 6000.00m,
            Credit = 0.00m,
            Transaction = transaction
        };

        var creditLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = checkingAccount.Id,
            Debit = 0.00m,
            Credit = 1000.00m,
            Transaction = transaction
        };

        var creditLine2 = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = savingsAccount.Id,
            Debit = 0.00m,
            Credit = 5000.00m,
            Transaction = transaction
        };

        await dbContext.JournalLines.AddRangeAsync(debitLine, creditLine, creditLine2);
        await dbContext.SaveChangesAsync();
    }
} 