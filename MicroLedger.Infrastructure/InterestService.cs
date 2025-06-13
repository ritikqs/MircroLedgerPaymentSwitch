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
                _logger.LogError(ex, "Error occurred while calculating interest");
                // Wait 5 minutes before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
    
    private async Task CalculateAndPostInterest()
    {
        _logger.LogInformation("Starting daily interest calculation");
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        
        var rate = _config.GetValue<decimal>("Interest:AnnualRate");
        var dailyRate = _calculator.GetDailyRate(rate);
        
        var savingsAccounts = await db.Accounts
            .Where(a => a.Type == AccountType.Savings)
            .ToListAsync();

        foreach (var account in savingsAccounts)
        {
            try
            {
                var balance = await db.JournalLines
                    .Where(j => j.AccountId == account.Id)
                    .SumAsync(j => j.Credit - j.Debit);

                if (balance <= 0) continue;

                var interest = _calculator.CalculateInterest(balance, dailyRate);
                _logger.LogInformation("Calculated interest {Interest} for account {AccountId}", interest, account.Id);

                // Create transaction for interest
                var transaction = new Transaction
                {
                    Reference = "Daily Interest",
                    TimestampUtc = DateTime.UtcNow
                };

                db.Transactions.Add(transaction);
                await db.SaveChangesAsync();

                // Create journal lines
                var debitLine = new JournalLine
                {
                    TransactionId = transaction.Id,
                    AccountId = "BANK_CAPITAL", // System account for bank's capital
                    Debit = interest,
                    Credit = 0.00m,
                    Transaction = transaction
                };

                var creditLine = new JournalLine
                {
                    TransactionId = transaction.Id,
                    AccountId = account.Id,
                    Debit = 0.00m,
                    Credit = interest,
                    Transaction = transaction
                };

                db.JournalLines.AddRange(debitLine, creditLine);
                await db.SaveChangesAsync();

                // Update account balance
                account.Balance += interest;
                await db.SaveChangesAsync();

                // Publish event
                var interestEvent = new InterestAccrued(
                    transaction.Id,
                    account.Id,
                    interest,
                    DateTime.UtcNow
                );

                await outboxService.SaveEventAsync(interestEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing interest for account {AccountId}", account.Id);
            }
        }
    }
}

public class InterestCalculator
{
    public decimal GetDailyRate(decimal annualRate) => annualRate / 365;
    public decimal CalculateInterest(decimal balance, decimal dailyRate) => Math.Round(balance * dailyRate, 2);
} 