using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLedger.Application.Services.Interfaces;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain;

namespace MicroLedger.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ILedgerDbContext _db;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(ILedgerDbContext db, ILogger<TransactionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TransactionDto> GetTransactionAsync(string id)
    {
        var transaction = await _db.Transactions
            .Include(t => t.JournalLines)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
        {
            throw new ArgumentException($"Transaction {id} not found");
        }

        return new TransactionDto(
            Id: transaction.Id,
            Reference: transaction.Reference,
            TimestampUtc: transaction.TimestampUtc,
            JournalLines: transaction.JournalLines.Select(jl => new JournalLineDto(
                Id: jl.Id,
                AccountId: jl.AccountId,
                Debit: jl.Debit,
                Credit: jl.Credit
            )).ToList()
        );
    }

    public async Task<AccountBalanceDto> GetAccountBalanceAsync(string accountId, DateTime? asOfDate = null)
    {
        var query = _db.JournalLines
            .Where(j => j.AccountId == accountId);

        if (asOfDate.HasValue)
        {
            query = query.Where(j => j.Transaction.TimestampUtc <= asOfDate.Value);
        }

        var balance = await query.SumAsync(j => j.Credit - j.Debit);

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null)
        {
            throw new ArgumentException($"Account {accountId} not found");
        }

        return new AccountBalanceDto(
            AccountId: accountId,
            Balance: balance,
            Currency: account.Currency,
            AsOfDate: asOfDate ?? DateTime.UtcNow
        );
    }
} 