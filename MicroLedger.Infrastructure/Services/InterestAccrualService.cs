using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MicroLedger.Domain;
using MicroLedger.Domain.Interfaces;

namespace MicroLedger.Infrastructure.Services;

public class InterestAccrualService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<InterestAccrualService> _logger;
    private readonly decimal _annualRate;

    public InterestAccrualService(
        IServiceProvider services,
        ILogger<InterestAccrualService> logger,
        decimal annualRate)
    {
        _services = services;
        _logger = logger;
        _annualRate = annualRate;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1).AddHours(2); // Next 02:00 UTC
                var delay = nextRun - now;

                _logger.LogInformation("Next interest accrual scheduled for {NextRun}", nextRun);
                await Task.Delay(delay, stoppingToken);

                await ProcessInterestAccrualAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing interest accrual");
                // Wait 5 minutes before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task ProcessInterestAccrualAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ILedgerDbContext>();

        try
        {
            _logger.LogInformation("Starting daily interest accrual process");

            // Get all savings accounts
            var savingsAccounts = await db.Accounts
                .Where(a => a.Type == AccountType.Savings)
                .ToListAsync(stoppingToken);

            foreach (var account in savingsAccounts)
            {
                try
                {
                    // Calculate balance as of now
                    var balance = await db.JournalLines
                        .Where(j => j.AccountId == account.Id)
                        .SumAsync(j => j.Credit - j.Debit, stoppingToken);

                    // Calculate daily interest
                    var dailyRate = _annualRate / 365;
                    var interestAmount = balance * dailyRate;

                    if (interestAmount > 0)
                    {
                        // Create transaction for interest accrual
                        var transaction = new Transaction
                        {
                            Reference = $"Daily Interest Accrual {DateTime.UtcNow:yyyy-MM-dd}"
                        };

                        await db.Transactions.AddAsync(transaction, stoppingToken);
                        await db.SaveChangesAsync(stoppingToken);

                        // Create journal lines for the interest accrual
                        var customerJournalLine = new JournalLine
                        {
                            TransactionId = transaction.Id,
                            AccountId = account.Id,
                            Credit = interestAmount,
                            Debit = 0,
                            Transaction = transaction
                        };

                        var bankJournalLine = new JournalLine
                        {
                            TransactionId = transaction.Id,
                            AccountId = "BANK_CAPITAL", // System account for bank's capital
                            Credit = 0,
                            Debit = interestAmount,
                            Transaction = transaction
                        };

                        await db.JournalLines.AddRangeAsync(new[] { customerJournalLine, bankJournalLine }, stoppingToken);
                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation(
                            "Interest of {InterestAmount} accrued for account {AccountId}",
                            interestAmount,
                            account.Id
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing interest for account {AccountId}", account.Id);
                    // Continue with next account
                }
            }

            _logger.LogInformation("Completed daily interest accrual process");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in interest accrual process");
            throw;
        }
    }
} 