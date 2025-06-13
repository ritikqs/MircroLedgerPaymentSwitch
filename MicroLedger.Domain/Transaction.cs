using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroLedger.Domain;

public class Transaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public required string Reference { get; set; }
    public virtual ICollection<JournalLine> JournalLines { get; set; } = new List<JournalLine>();

    public void Validate()
    {
        // Validate reference
        if (string.IsNullOrEmpty(Reference))
        {
            throw new ArgumentException("Transaction reference cannot be null or empty.");
        }

        // Validate journal lines exist
        if (!JournalLines.Any())
        {
            throw new InvalidOperationException("Transaction must have at least one journal line.");
        }

        // Validate no zero amounts
        if (JournalLines.Any(l => l.Debit == 0 && l.Credit == 0))
        {
            throw new InvalidOperationException("Transaction cannot have zero amount journal lines.");
        }

        // Validate no negative amounts
        if (JournalLines.Any(l => l.Debit < 0 || l.Credit < 0))
        {
            throw new InvalidOperationException("Transaction cannot have negative amounts.");
        }

        // Validate double-entry balance
        var totalDebit = JournalLines.Sum(l => l.Debit);
        var totalCredit = JournalLines.Sum(l => l.Credit);
        if (totalDebit != totalCredit)
        {
            throw new InvalidOperationException($"Transaction is not balanced. Total Debit: {totalDebit}, Total Credit: {totalCredit}");
        }

        // Validate no same account debit and credit
        var accountIds = JournalLines.Select(l => l.AccountId).Distinct();
        foreach (var accountId in accountIds)
        {
            var accountLines = JournalLines.Where(l => l.AccountId == accountId);
            if (accountLines.Any(l => l.Debit > 0) && accountLines.Any(l => l.Credit > 0))
            {
                throw new InvalidOperationException($"Account {accountId} cannot be both debited and credited in the same transaction.");
            }
        }
    }
} 