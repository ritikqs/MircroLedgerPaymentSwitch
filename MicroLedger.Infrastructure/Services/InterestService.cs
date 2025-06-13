using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public InterestService(
            ILedgerDbContext db,
            IOutboxService outboxService,
            ILogger<InterestService> logger)
        {
            _db = db;
            _outboxService = outboxService;
            _logger = logger;
        }

        public async Task AccrueInterestAsync(string accountId, DateTime asOfDate)
        {
            try
            {
                // Get the account
                var account = await _db.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId);

                if (account == null)
                {
                    _logger.LogWarning("Account {AccountId} not found for interest accrual", accountId);
                    return;
                }

                // Calculate balance as of the specified date
                var balance = await _db.JournalLines
                    .Where(j => j.AccountId == accountId && j.Transaction.TimestampUtc <= asOfDate)
                    .SumAsync(j => j.Credit - j.Debit);

                // Calculate daily interest rate (annual rate / 365)
                var dailyRate = account.InterestRate / 365;

                // Calculate interest amount
                var interestAmount = balance * dailyRate;

                if (interestAmount > 0)
                {
                    // Create a transaction for the interest accrual
                    var transaction = new Transaction
                    {
                        Reference = $"Interest Accrual for {asOfDate:yyyy-MM-dd}"
                    };

                    await _db.Transactions.AddAsync(transaction);
                    await _db.SaveChangesAsync();

                    // Create journal line for the interest accrual
                    var journalLine = new JournalLine
                    {
                        TransactionId = transaction.Id,
                        AccountId = accountId,
                        Credit = interestAmount,
                        Debit = 0,
                        Transaction = transaction
                    };

                    await _db.JournalLines.AddAsync(journalLine);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation(
                        "Interest of {InterestAmount} accrued for account {AccountId} as of {AsOfDate}",
                        interestAmount,
                        accountId,
                        asOfDate
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accruing interest for account {AccountId}", accountId);
                throw;
            }
        }
    }
} 