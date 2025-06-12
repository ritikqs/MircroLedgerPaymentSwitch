using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MicroLedger.Domain.Services;

namespace MicroLedger.Infrastructure.Services;

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IServiceProvider services,
        ILogger<OutboxPublisherService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
                await outboxService.PublishPendingEventsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox publisher service");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
} 