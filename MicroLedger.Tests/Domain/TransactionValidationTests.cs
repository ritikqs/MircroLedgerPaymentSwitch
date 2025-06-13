using Xunit;
using MicroLedger.Domain;
using System;
using System.Linq;

namespace MicroLedger.Tests.Domain;

public class TransactionValidationTests
{
    [Fact]
    public void Transaction_WithValidReference_ShouldSucceed()
    {
        // Arrange & Act
        var transaction = new Transaction
        {
            Reference = "Valid Reference 123"
        };

        // Assert
        Assert.NotNull(transaction.Reference);
        Assert.NotEmpty(transaction.Reference);
    }

    [Fact]
    public void Transaction_WithBalancedJournalLines_ShouldSucceed()
    {
        // Arrange
        var transaction = new Transaction
        {
            Reference = "Balanced Transaction"
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
    public void Transaction_WithUnbalancedJournalLines_ShouldThrowException()
    {
        // Arrange
        var transaction = new Transaction
        {
            Reference = "Unbalanced Transaction"
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
        Assert.Throws<InvalidOperationException>(() => transaction.Validate());
    }

    [Fact]
    public void Transaction_WithMultipleJournalLines_ShouldMaintainBalance()
    {
        // Arrange
        var transaction = new Transaction
        {
            Reference = "Multiple Lines Transaction"
        };

        // Act
        transaction.JournalLines.Add(new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 100.00m,
            Credit = 0.00m,
            Transaction = transaction
        });

        transaction.JournalLines.Add(new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 50.00m,
            Credit = 0.00m,
            Transaction = transaction
        });

        transaction.JournalLines.Add(new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 0.00m,
            Credit = 150.00m,
            Transaction = transaction
        });

        // Assert
        var totalDebit = transaction.JournalLines.Sum(l => l.Debit);
        var totalCredit = transaction.JournalLines.Sum(l => l.Credit);
        Assert.Equal(totalDebit, totalCredit);
        Assert.Equal(150.00m, totalDebit);
        Assert.Equal(150.00m, totalCredit);
    }

    [Fact]
    public void Transaction_WithZeroAmountJournalLines_ShouldThrowException()
    {
        // Arrange
        var transaction = new Transaction
        {
            Reference = "Zero Amount Transaction"
        };

        // Act
        transaction.JournalLines.Add(new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 0.00m,
            Credit = 0.00m,
            Transaction = transaction
        });

        // Assert
        Assert.Throws<InvalidOperationException>(() => transaction.Validate());
    }

    [Fact]
    public void Transaction_WithNegativeAmounts_ShouldThrowException()
    {
        // Arrange
        var transaction = new Transaction
        {
            Reference = "Negative Amount Transaction"
        };

        // Act
        transaction.JournalLines.Add(new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = -100.00m,
            Credit = 0.00m,
            Transaction = transaction
        });

        // Assert
        Assert.Throws<InvalidOperationException>(() => transaction.Validate());
    }

    [Fact]
    public void Transaction_WithSameAccountDebitAndCredit_ShouldThrowException()
    {
        // Arrange
        var transaction = new Transaction
        {
            Reference = "Same Account Transaction"
        };

        var accountId = Guid.NewGuid().ToString();

        // Act
        transaction.JournalLines.Add(new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = accountId,
            Debit = 100.00m,
            Credit = 0.00m,
            Transaction = transaction
        });

        transaction.JournalLines.Add(new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = accountId,  // Same account!
            Debit = 0.00m,
            Credit = 100.00m,
            Transaction = transaction
        });

        // Assert
        Assert.Throws<InvalidOperationException>(() => transaction.Validate());
    }

    [Fact]
    public void Transaction_WithDecimalPrecision_ShouldMaintainAccuracy()
    {
        // Arrange
        var transaction = new Transaction
        {
            Reference = "Decimal Precision Transaction"
        };

        // Act
        transaction.JournalLines.Add(new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 100.123456789m,
            Credit = 0.00m,
            Transaction = transaction
        });

        transaction.JournalLines.Add(new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = Guid.NewGuid().ToString(),
            Debit = 0.00m,
            Credit = 100.123456789m,
            Transaction = transaction
        });

        // Assert
        var totalDebit = transaction.JournalLines.Sum(l => l.Debit);
        var totalCredit = transaction.JournalLines.Sum(l => l.Credit);
        Assert.Equal(totalDebit, totalCredit);
        Assert.Equal(100.123456789m, totalDebit);
        Assert.Equal(100.123456789m, totalCredit);
    }
} 