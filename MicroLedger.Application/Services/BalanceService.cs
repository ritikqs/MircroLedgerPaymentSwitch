using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain.Services;
using Microsoft.Extensions.Logging;

namespace MicroLedger.Application.Services
{
    public class BalanceService : IBalanceService
    {
        private readonly ILedgerDbContext _db;
        private readonly ILogger<BalanceService> _logger;

        public BalanceService(ILedgerDbContext db, ILogger<BalanceService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task PublishBalanceUpdateAsync(string accountId, string transactionId, string description)
        {
            try
            {
                var balance = await _db.JournalLines
                    .Where(j => j.AccountId == accountId)
                    .SumAsync(j => j.Credit - j.Debit);

                _logger.LogInformation(
                    "Balance update for account {AccountId}: {Balance} ({Description})",
                    accountId,
                    balance,
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