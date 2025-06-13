using System;

namespace MicroLedger.Domain;

public class OutboxEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public required string EventType { get; set; }
    public required string EventData { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
} 