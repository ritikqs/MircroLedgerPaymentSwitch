using System;
using System.Threading.Tasks;
using MicroLedger.Domain;
using MicroLedger.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace MicroLedger.Infrastructure.Services;

public class OutboxService : IOutboxService
{
    private readonly ILedgerDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public OutboxService(ILedgerDbContext dbContext, IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
    }

    public async Task SaveEventAsync<T>(T @event) where T : class
    {
        var outboxEvent = new OutboxEvent
        {
            EventType = typeof(T).Name,
            EventData = System.Text.Json.JsonSerializer.Serialize(@event),
            TimestampUtc = DateTime.UtcNow
        };

        await _dbContext.OutboxEvents.AddAsync(outboxEvent);
        await _dbContext.SaveChangesAsync();
    }

    public async Task PublishPendingEventsAsync()
    {
        var pendingEvents = await _dbContext.OutboxEvents
            .Where(e => !e.IsPublished)
            .OrderBy(e => e.TimestampUtc)
            .Take(100)
            .ToListAsync();

        foreach (var @event in pendingEvents)
        {
            try
            {
                var eventType = Type.GetType(@event.EventType);
                if (eventType != null)
                {
                    var eventData = System.Text.Json.JsonSerializer.Deserialize(@event.EventData, eventType);
                    if (eventData != null)
                    {
                        await _publishEndpoint.Publish(eventData);
                        @event.IsPublished = true;
                        @event.PublishedAtUtc = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                @event.RetryCount++;
                @event.Error = ex.Message;
            }
        }

        await _dbContext.SaveChangesAsync();
    }
} 