using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain.Services;
using MicroLedger.Domain;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MicroLedger.Infrastructure.Services
{
    public class InterestService : IInterestService
    {
        private readonly ILedgerDbContext _db;
        private readonly IOutboxService _outboxService;
        private readonly ILogger<InterestService> _logger;
        private readonly IBalanceService _balanceService;

        public InterestService(
            ILedgerDbContext db,
            IOutboxService outboxService,
            ILogger<InterestService> logger,
            IBalanceService balanceService)
        {
            _db = db;
            _outboxService = outboxService;
            _logger = logger;
            _balanceService = balanceService;
        }

        public async Task CalculateAndPostInterest()
        {
            _logger.LogInformation("Starting interest calculation");

            var accounts = await _db.Accounts
                .Where(a => a.InterestRate > 0)
                .ToListAsync();

            foreach (var account in accounts)
            {
                var balance = await _db.JournalLines
                    .Where(j => j.AccountId == account.Id)
                    .SumAsync(j => j.Credit - j.Debit);

                if (balance <= 0) continue;

                var interestAmount = balance * (account.InterestRate / 365); // Daily interest

                var transaction = new Transaction
                {
                    Id = Guid.NewGuid().ToString(),
                    Reference = $"Interest for {account.Id}",
                    TimestampUtc = DateTime.UtcNow
                };

                var interestLine = new JournalLine
                {
                    Id = Guid.NewGuid().ToString(),
                    TransactionId = transaction.Id,
                    AccountId = account.Id,
                    Debit = 0,
                    Credit = interestAmount
                };

                _db.JournalLines.Add(interestLine);
                await _db.SaveChangesAsync();

                // Publish interest event
                var @event = new InterestAccrued(
                    TransactionId: transaction.Id,
                    AccountId: account.Id,
                    Amount: interestAmount,
                    TimestampUtc: transaction.TimestampUtc
                );

                await _outboxService.AddEventAsync(@event, @event.EventType);

                // Publish balance update
                await _balanceService.PublishBalanceUpdateAsync(
                    account.Id,
                    transaction.Id,
                    "Interest"
                );
            }

            _logger.LogInformation("Interest calculation completed");
        }
    }
} 