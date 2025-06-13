using Grpc.Core;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain.Protos;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace MicroLedger.Infrastructure.Services
{
    public class BalanceGrpcService : Domain.Protos.BalanceService.BalanceServiceBase
    {
        private readonly ILedgerDbContext _db;
        private readonly ILogger<BalanceGrpcService> _logger;

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

                // Send initial balance
                await responseStream.WriteAsync(new BalanceUpdate
                {
                    AccountId = request.AccountId,
                    Balance = (double)balance,
                    Currency = account.Currency,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                });

                // Keep the stream open for future updates
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, context.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming balance updates for account {AccountId}", request.AccountId);
                throw new RpcException(new Status(StatusCode.Internal, "An error occurred while streaming balance updates"));
            }
        }
    }
} 