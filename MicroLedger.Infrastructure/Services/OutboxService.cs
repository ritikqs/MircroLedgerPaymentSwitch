using System;
using System.Threading.Tasks;
using MicroLedger.Domain;
using MicroLedger.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MicroLedger.Infrastructure.Services;

public class OutboxService : IOutboxService
{
    private readonly ILedgerDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OutboxService> _logger;

    public OutboxService(
        ILedgerDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<OutboxService> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task SaveEventAsync<T>(T @event) where T : class
    {
        try
        {
            var outboxEvent = new OutboxEvent
            {
                EventType = typeof(T).FullName,
                EventData = System.Text.Json.JsonSerializer.Serialize(@event),
                TimestampUtc = DateTime.UtcNow
            };

            await _dbContext.OutboxEvents.AddAsync(outboxEvent);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Event {EventType} saved to outbox with ID {EventId}",
                outboxEvent.EventType,
                outboxEvent.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving event {EventType} to outbox", typeof(T).Name);
            throw;
        }
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
                _logger.LogInformation(
                    "Publishing event {EventId} of type {EventType}",
                    @event.Id,
                    @event.EventType
                );

                var eventType = Type.GetType(@event.EventType);
                if (eventType != null)
                {
                    var eventData = System.Text.Json.JsonSerializer.Deserialize(@event.EventData, eventType);
                    if (eventData != null)
                    {
                        await _publishEndpoint.Publish(eventData);
                        @event.IsPublished = true;
                        @event.PublishedAtUtc = DateTime.UtcNow;
                        @event.Error = null; // Clear any previous errors

                        _logger.LogInformation(
                            "Successfully published event {EventId}",
                            @event.Id
                        );
                    }
                    else
                    {
                        throw new InvalidOperationException($"Failed to deserialize event data for {@event.EventType}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Event type {@event.EventType} not found");
                }
            }
            catch (Exception ex)
            {
                @event.RetryCount++;
                @event.Error = ex.Message;

                _logger.LogError(
                    ex,
                    "Error publishing event {EventId} (Retry {RetryCount}): {Error}",
                    @event.Id,
                    @event.RetryCount,
                    ex.Message
                );

                // If we've retried too many times, mark as failed
                if (@event.RetryCount >= 3)
                {
                    _logger.LogWarning(
                        "Event {EventId} failed after {RetryCount} retries",
                        @event.Id,
                        @event.RetryCount
                    );
                }
            }
        }

        await _dbContext.SaveChangesAsync();
    }
} 