using System;

namespace MicroLedger.Domain;

public class JournalLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TransactionId { get; set; }
    public string AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Transaction Transaction { get; set; }
} 