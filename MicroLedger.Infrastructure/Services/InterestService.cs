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
        private readonly ILedgerDbContext _dbContext;
        private readonly IOutboxService _outboxService;
        private readonly ILogger<InterestService> _logger;

        public InterestService(
            ILedgerDbContext dbContext,
            IOutboxService outboxService,
            ILogger<InterestService> logger)
        {
            _dbContext = dbContext;
            _outboxService = outboxService;
            _logger = logger;
        }

        public async Task AccrueDailyInterestAsync(decimal annualRate)
        {
            var dailyRate = annualRate / 365;
            var savingsAccounts = await _dbContext.Accounts
                .Where(a => a.Type == AccountType.Savings)
                .ToListAsync();

            foreach (var account in savingsAccounts)
            {
                try
                {
                    var interestAmount = Math.Round(account.Balance * dailyRate, 2);
                    if (interestAmount <= 0)
                    {
                        continue;
                    }

                    // Create transaction for interest accrual
                    var transaction = new Transaction
                    {
                        Reference = $"Daily Interest Accrual - {DateTime.UtcNow:yyyy-MM-dd}"
                    };

                    await _dbContext.Transactions.AddAsync(transaction);
                    await _dbContext.SaveChangesAsync();

                    // Create journal lines
                    var debitLine = new JournalLine
                    {
                        TransactionId = transaction.Id,
                        AccountId = "BANK_CAPITAL", // System account for bank's capital
                        Debit = interestAmount,
                        Credit = 0.00m,
                        Transaction = transaction
                    };

                    var creditLine = new JournalLine
                    {
                        TransactionId = transaction.Id,
                        AccountId = account.Id,
                        Debit = 0.00m,
                        Credit = interestAmount,
                        Transaction = transaction
                    };

                    await _dbContext.JournalLines.AddRangeAsync(debitLine, creditLine);
                    await _dbContext.SaveChangesAsync();

                    // Update account balance
                    account.Balance += interestAmount;
                    await _dbContext.SaveChangesAsync();

                    // Publish event
                    var interestEvent = new InterestAccrued(
                        transaction.Id,
                        account.Id,
                        interestAmount,
                        DateTime.UtcNow
                    );

                    await _outboxService.SaveEventAsync(interestEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accruing interest for account {AccountId}", account.Id);
                }
            }
        }
    }
} 