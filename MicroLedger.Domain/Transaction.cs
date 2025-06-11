using System;
using System.Collections.Generic;

namespace MicroLedger.Domain;

public class Transaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Reference { get; set; }
    public List<JournalLine> JournalLines { get; set; } = new();
} 