using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MicroLedger.Domain;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MicroLedger.Infrastructure;

public class InterestService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly InterestCalculator _calculator;
    
    public InterestService(IServiceProvider services, IConfiguration config)
    {
        _services = services;
        _config = config;
        _calculator = new InterestCalculator();
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(now.TimeOfDay < TimeSpan.FromHours(2) ? 0 : 1).AddHours(2); // Next 2 AM UTC
            var delay = nextRun - now;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);
            await AccrueInterest();
            // Wait 24 hours for the next run
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
    
    private async Task AccrueInterest()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        var rate = _config.GetValue<decimal>("Interest:AnnualRate");
        var dailyRate = _calculator.GetDailyRate(rate);
        var savingsAccounts = await db.Accounts
            .Where(a => a.Type == AccountType.Savings)
            .ToListAsync();
        foreach (var account in savingsAccounts)
        {
            var balance = await db.JournalLines
                .Where(j => j.AccountId == account.Id)
                .SumAsync(j => j.Credit - j.Debit);
            if (balance <= 0) continue;
            var interest = _calculator.CalculateInterest(balance, dailyRate);
            var tx = new Transaction { Reference = "Daily Interest" };
            tx.JournalLines.Add(new JournalLine {
                AccountId = account.Id,
                Debit = 0,
                Credit = interest
            });
            tx.JournalLines.Add(new JournalLine {
                AccountId = "BANK_CAPITAL", // Special account
                Debit = interest,
                Credit = 0
            });
            db.Transactions.Add(tx);
        }
        await db.SaveChangesAsync();
    }
}

public class InterestCalculator
{
    public decimal GetDailyRate(decimal annualRate) => annualRate / 365;
    public decimal CalculateInterest(decimal balance, decimal dailyRate) => Math.Round(balance * dailyRate, 2);
} 