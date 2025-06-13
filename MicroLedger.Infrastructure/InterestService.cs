using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MicroLedger.Domain;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain.Events;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MicroLedger.Infrastructure;

public class InterestService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<InterestService> _logger;
    private readonly IConfiguration _config;
    private readonly InterestCalculator _calculator;
    
    public InterestService(
        IServiceProvider services,
        ILogger<InterestService> logger,
        IConfiguration config)
    {
        _services = services;
        _logger = logger;
        _config = config;
        _calculator = new InterestCalculator();
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate next run time (02:00 UTC)
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1).AddHours(2);
                var delay = nextRun - now;

                _logger.LogInformation("Next interest calculation scheduled for {NextRun}", nextRun);
                await Task.Delay(delay, stoppingToken);

                await CalculateAndPostInterest();
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in interest calculation service");
                // Wait 5 minutes before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task CalculateAndPostInterest()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ILedgerDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        try
        {
            _logger.LogInformation("Starting interest calculation process");

            // Get all savings accounts
            var savingsAccounts = await db.Accounts
                .Where(a => a.Type == AccountType.Savings)
                .ToListAsync();

            foreach (var account in savingsAccounts)
            {
                try
                {
                    // Calculate balance as of now
                    var balance = await db.JournalLines
                        .Where(j => j.AccountId == account.Id)
                        .SumAsync(j => j.Credit - j.Debit);

                    // Calculate daily interest
                    var dailyRate = account.InterestRate / 365;
                    var interestAmount = balance * dailyRate;

                    if (interestAmount > 0)
                    {
                        // Create transaction for interest accrual
                        var transaction = new Transaction
                        {
                            Reference = $"Daily Interest Accrual {DateTime.UtcNow:yyyy-MM-dd}"
                        };

                        await db.Transactions.AddAsync(transaction);
                        await db.SaveChangesAsync();

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

                        await db.JournalLines.AddRangeAsync(customerJournalLine, bankJournalLine);
                        await db.SaveChangesAsync();

                        // Publish event
                        var interestEvent = new InterestAccrued(
                            transaction.Id,
                            account.Id,
                            interestAmount,
                            DateTime.UtcNow
                        );

                        await outbox.SaveEventAsync(interestEvent);

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in interest calculation process");
            throw;
        }
    }
}

public class InterestCalculator
{
    public decimal GetDailyRate(decimal annualRate) => annualRate / 365;
    public decimal CalculateInterest(decimal balance, decimal dailyRate) => Math.Round(balance * dailyRate, 2);
} 