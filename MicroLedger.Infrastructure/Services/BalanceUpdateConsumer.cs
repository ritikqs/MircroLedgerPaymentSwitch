using System.Threading.Tasks;
using MassTransit;
using MicroLedger.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MicroLedger.Infrastructure.Services;

public class BalanceUpdateConsumer : IConsumer<BalanceUpdated>
{
    private readonly ILogger<BalanceUpdateConsumer> _logger;

    public BalanceUpdateConsumer(ILogger<BalanceUpdateConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<BalanceUpdated> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Balance update received for account {AccountId}: {Balance} {Currency} (Transaction: {TransactionId})",
            message.AccountId,
            message.Balance,
            message.Currency,
            message.TransactionId
        );

        // The actual balance update will be handled by the gRPC service
        return Task.CompletedTask;
    }
} 