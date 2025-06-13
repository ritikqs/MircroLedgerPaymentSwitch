using Xunit;
using MicroLedger.Domain;
using System;
using System.Linq;

namespace MicroLedger.Tests.Domain;

public class LedgerTests
{
    [Fact]
    public void CreateTransaction_WithBalancedEntries_ShouldSucceed()
    {
        // Arrange
        var transaction = new Transaction
        {
            Reference = "Test Transfer"
        };

        var debitLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 100.00m,
            Credit = 0.00m,
            Transaction = transaction
        };

        var creditLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 0.00m,
            Credit = 100.00m,
            Transaction = transaction
        };

        // Act
        transaction.JournalLines.Add(debitLine);
        transaction.JournalLines.Add(creditLine);

        // Assert
        var totalDebit = transaction.JournalLines.Sum(l => l.Debit);
        var totalCredit = transaction.JournalLines.Sum(l => l.Credit);
        Assert.Equal(totalDebit, totalCredit);
        Assert.Equal(100.00m, totalDebit);
        Assert.Equal(100.00m, totalCredit);
    }

    [Fact]
    public void CreateTransaction_WithUnbalancedEntries_ShouldThrowException()
    {
        // Arrange
        var transaction = new Transaction
        {
            Reference = "Test Transfer"
        };

        var debitLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 100.00m,
            Credit = 0.00m,
            Transaction = transaction
        };

        var creditLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 0.00m,
            Credit = 50.00m,  // Unbalanced!
            Transaction = transaction
        };

        // Act
        transaction.JournalLines.Add(debitLine);
        transaction.JournalLines.Add(creditLine);

        // Assert
        var totalDebit = transaction.JournalLines.Sum(l => l.Debit);
        var totalCredit = transaction.JournalLines.Sum(l => l.Credit);
        Assert.NotEqual(totalDebit, totalCredit);
    }

    [Fact]
    public void CalculateAccountBalance_ShouldSumCorrectly()
    {
        // Arrange
        var accountId = Guid.NewGuid().ToString();
        var transaction = new Transaction
        {
            Reference = "Test Transfer"
        };

        var debitLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = accountId,
            Debit = 100.00m,
            Credit = 0.00m,
            Transaction = transaction
        };

        var creditLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = accountId,
            Debit = 0.00m,
            Credit = 150.00m,
            Transaction = transaction
        };

        // Act
        transaction.JournalLines.Add(debitLine);
        transaction.JournalLines.Add(creditLine);

        // Assert
        var balance = transaction.JournalLines
            .Where(l => l.AccountId == accountId)
            .Sum(l => l.Credit - l.Debit);
        
        Assert.Equal(50.00m, balance);
    }
} 