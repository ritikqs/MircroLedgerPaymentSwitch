using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain.Services;
using MicroLedger.Domain.Events;
using Microsoft.Extensions.Logging;
using MassTransit;

namespace MicroLedger.Application.Services
{
    public class BalanceService : IBalanceService
    {
        private readonly ILedgerDbContext _db;
        private readonly ILogger<BalanceService> _logger;
        private readonly IPublishEndpoint _publishEndpoint;

        public BalanceService(
            ILedgerDbContext db,
            ILogger<BalanceService> logger,
            IPublishEndpoint publishEndpoint)
        {
            _db = db;
            _logger = logger;
            _publishEndpoint = publishEndpoint;
        }

        public async Task PublishBalanceUpdateAsync(string accountId, string transactionId, string description)
        {
            try
            {
                var balance = await _db.JournalLines
                    .Where(j => j.AccountId == accountId)
                    .SumAsync(j => j.Credit - j.Debit);

                var account = await _db.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId);

                if (account == null)
                {
                    _logger.LogWarning("Account {AccountId} not found for balance update", accountId);
                    return;
                }

                var transaction = await _db.Transactions
                    .FirstOrDefaultAsync(t => t.Id == transactionId);

                var balanceUpdated = new BalanceUpdated(
                    AccountId: accountId,
                    Balance: balance,
                    Currency: account.Currency,
                    TransactionId: transactionId,
                    TransactionType: transaction?.Reference ?? "Unknown",
                    TimestampUtc: DateTime.UtcNow
                );

                await _publishEndpoint.Publish(balanceUpdated);

                _logger.LogInformation(
                    "Balance update published for account {AccountId}: {Balance} {Currency} ({Description})",
                    accountId,
                    balance,
                    account.Currency,
                    description
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing balance update for account {AccountId}", accountId);
                throw;
            }
        }
    }
} 