using System;

namespace MicroLedger.Domain;

public class OutboxEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; }
    public string EventData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
} 