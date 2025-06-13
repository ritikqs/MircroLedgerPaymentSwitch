using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain;
using System;
using System.Threading.Tasks;

namespace MicroLedger.Infrastructure.Services;

public class OutboxPublisherService : IOutboxService
{
    private readonly ILedgerDbContext _dbContext;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        ILedgerDbContext dbContext,
        ILogger<OutboxPublisherService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SaveEventAsync<T>(T @event) where T : class
    {
        var outboxEvent = new OutboxEvent
        {
            EventType = @event.GetType().Name,
            EventData = System.Text.Json.JsonSerializer.Serialize(@event),
            TimestampUtc = DateTime.UtcNow
        };

        await _dbContext.OutboxEvents.AddAsync(outboxEvent);
        await _dbContext.SaveChangesAsync();
    }

    public async Task PublishPendingEventsAsync()
    {
        var pendingEvents = await _dbContext.OutboxEvents
            .Where(e => e.PublishedAtUtc == null)
            .OrderBy(e => e.TimestampUtc)
            .ToListAsync();

        foreach (var @event in pendingEvents)
        {
            try
            {
                // TODO: Implement actual event publishing
                @event.PublishedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing event {EventId}", @event.Id);
            }
        }
    }
} 