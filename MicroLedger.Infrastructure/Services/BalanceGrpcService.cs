using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain.Protos;
using MicroLedger.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace MicroLedger.Infrastructure.Services
{
    public class BalanceGrpcService : Domain.Protos.BalanceService.BalanceServiceBase
    {
        private readonly ILedgerDbContext _db;
        private readonly ILogger<BalanceGrpcService> _logger;
        private static readonly ConcurrentDictionary<string, Channel<BalanceUpdate>> _balanceChannels = new();

        public BalanceGrpcService(ILedgerDbContext db, ILogger<BalanceGrpcService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public override async Task StreamBalanceUpdates(
            BalanceRequest request,
            IServerStreamWriter<BalanceUpdate> responseStream,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("Starting balance stream for account {AccountId}", request.AccountId);

                // Get initial balance
                var balance = await _db.JournalLines
                    .Where(j => j.AccountId == request.AccountId)
                    .SumAsync(j => j.Credit - j.Debit);

                var account = await _db.Accounts
                    .FirstOrDefaultAsync(a => a.Id == request.AccountId);

                if (account == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"Account {request.AccountId} not found"));
                }

                // Create a channel for this account's balance updates
                var channel = Channel.CreateUnbounded<BalanceUpdate>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

                _balanceChannels.TryAdd(request.AccountId, channel);

                try
                {
                    // Send initial balance
                    await responseStream.WriteAsync(new BalanceUpdate
                    {
                        AccountId = request.AccountId,
                        Balance = (double)balance,
                        Currency = account.Currency,
                        TimestampUtc = DateTime.UtcNow.ToString("o")
                    });

                    // Keep reading from the channel until the client disconnects
                    while (!context.CancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var update = await channel.Reader.ReadAsync(context.CancellationToken);
                            await responseStream.WriteAsync(update);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    // Clean up the channel when the client disconnects
                    _balanceChannels.TryRemove(request.AccountId, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming balance updates for account {AccountId}", request.AccountId);
                throw new RpcException(new Status(StatusCode.Internal, "An error occurred while streaming balance updates"));
            }
        }

        public static void PublishBalanceUpdate(BalanceUpdated @event)
        {
            if (_balanceChannels.TryGetValue(@event.AccountId, out var channel))
            {
                var update = new BalanceUpdate
                {
                    AccountId = @event.AccountId,
                    Balance = (double)@event.Balance,
                    Currency = @event.Currency,
                    TransactionId = @event.TransactionId,
                    TransactionType = @event.TransactionType,
                    TimestampUtc = @event.TimestampUtc.ToString("o")
                };

                channel.Writer.TryWrite(update);
            }
        }
    }
} 