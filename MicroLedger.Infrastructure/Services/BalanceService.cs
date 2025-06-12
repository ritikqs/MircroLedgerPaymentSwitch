using Grpc.Core;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain.Protos;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using MicroLedger.Domain.Protos;
using Microsoft.EntityFrameworkCore;

namespace MicroLedger.Infrastructure.Services;

public class BalanceService : Domain.Protos.BalanceService.BalanceServiceBase
{
    private readonly ILogger<BalanceService> _logger;
    private readonly ILedgerDbContext _db;
    private readonly Channel<BalanceUpdate> _channel;

    public BalanceService(ILogger<BalanceService> logger, ILedgerDbContext db)
    {
        _logger = logger;
        _db = db;
        _channel = Channel.CreateUnbounded<BalanceUpdate>();
    }

    public override async Task StreamBalanceUpdates(
        BalanceRequest request,
        IServerStreamWriter<BalanceUpdate> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("Starting balance stream for account {AccountId}", request.AccountId);

        try
        {
            // Send initial balance
            var initialBalance = await GetAccountBalanceAsync(request.AccountId);
            if (initialBalance != null)
            {
                await responseStream.WriteAsync(initialBalance);
            }

            // Stream updates
            await foreach (var update in _channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                if (update.AccountId == request.AccountId)
                {
                    await responseStream.WriteAsync(update);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Balance stream cancelled for account {AccountId}", request.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in balance stream for account {AccountId}", request.AccountId);
            throw;
        }
    }

    public async Task PublishBalanceUpdateAsync(string accountId, string transactionId, string transactionType)
    {
        var update = await GetAccountBalanceAsync(accountId);
        if (update != null)
        {
            update.TransactionId = transactionId;
            update.TransactionType = transactionType;
            await _channel.Writer.WriteAsync(update);
        }
    }

    private async Task<BalanceUpdate?> GetAccountBalanceAsync(string accountId)
    {
        var account = await _db.Accounts.FindAsync(accountId);
        if (account == null)
        {
            return null;
        }

        var balance = await _db.JournalLines
            .Where(j => j.AccountId == accountId)
            .SumAsync(j => j.Credit - j.Debit);

        return new BalanceUpdate
        {
            AccountId = accountId,
            Balance = (double)balance,
            Currency = account.Currency,
            TimestampUtc = DateTime.UtcNow.ToString("o")
        };
    }
} 