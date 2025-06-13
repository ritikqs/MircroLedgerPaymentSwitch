namespace MicroLedger.Domain.Interfaces;

public interface IOutboxService
{
    Task SaveEventAsync<T>(T @event) where T : class;
    Task PublishPendingEventsAsync();
} 