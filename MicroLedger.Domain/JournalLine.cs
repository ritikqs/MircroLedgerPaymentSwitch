using System;

namespace MicroLedger.Domain;

public class JournalLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string TransactionId { get; set; }
    public required string AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public required Transaction Transaction { get; set; }
} 