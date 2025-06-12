namespace MicroLedger.Domain.Services;

public interface IOutboxService
{
    Task AddEventAsync<T>(T eventData, string eventType) where T : class;
    Task PublishPendingEventsAsync();
} 