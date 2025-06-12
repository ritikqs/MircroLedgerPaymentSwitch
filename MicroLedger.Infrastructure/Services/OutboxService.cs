using System.Text.Json;
using MicroLedger.Domain.Services;
using MicroLedger.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MassTransit;
using MicroLedger.Domain;

namespace MicroLedger.Infrastructure.Services;

public class OutboxService : IOutboxService
{
    private readonly ILedgerDbContext _db;
    private readonly ILogger<OutboxService> _logger;
    private readonly IBus _bus;

    public OutboxService(
        ILedgerDbContext db,
        ILogger<OutboxService> logger,
        IBus bus)
    {
        _db = db;
        _logger = logger;
        _bus = bus;
    }

    public async Task AddEventAsync<T>(T eventData, string eventType) where T : class
    {
        var outboxEvent = new OutboxEvent
        {
            EventType = eventType,
            EventData = JsonSerializer.Serialize(eventData),
            CreatedAt = DateTime.UtcNow,
            IsPublished = false,
            RetryCount = 0
        };

        _db.OutboxEvents.Add(outboxEvent);
        await _db.SaveChangesAsync();
    }

    public async Task PublishPendingEventsAsync()
    {
        var unpublishedEvents = await _db.OutboxEvents
            .Where(e => !e.IsPublished && e.RetryCount < 3)
            .OrderBy(e => e.CreatedAt)
            .Take(100)
            .ToListAsync();

        foreach (var evt in unpublishedEvents)
        {
            try
            {
                var eventData = JsonSerializer.Deserialize<object>(evt.EventData);
                await _bus.Publish(eventData, context => context.MessageId = Guid.Parse(evt.Id));

                evt.IsPublished = true;
                evt.PublishedAt = DateTime.UtcNow;
                evt.Error = null;
            }
            catch (Exception ex)
            {
                evt.RetryCount++;
                evt.Error = ex.Message;
                _logger.LogError(ex, "Error publishing event {EventId}", evt.Id);
            }
        }

        await _db.SaveChangesAsync();
    }
} 