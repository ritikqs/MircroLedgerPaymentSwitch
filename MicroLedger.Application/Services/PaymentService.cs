using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLedger.Application.Services.Interfaces;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain;
using MicroLedger.Domain.Events;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace MicroLedger.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly ILedgerDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        ILedgerDbContext dbContext,
        IOutboxService outboxService,
        ILogger<PaymentService> logger)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _logger = logger;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero", nameof(request.Amount));
        }

        var fromAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.FromAccountId);

        if (fromAccount == null)
        {
            throw new ArgumentException($"Account {request.FromAccountId} not found", nameof(request.FromAccountId));
        }

        var toAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.ToAccountId);

        if (toAccount == null)
        {
            throw new ArgumentException($"Account {request.ToAccountId} not found", nameof(request.ToAccountId));
        }

        if (fromAccount.Balance < request.Amount)
        {
            throw new InvalidOperationException("Insufficient funds");
        }

        // Create transaction
        var transaction = new Transaction
        {
            Reference = request.Reference
        };

        await _dbContext.Transactions.AddAsync(transaction);
        await _dbContext.SaveChangesAsync();

        // Create journal lines
        var debitLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = request.FromAccountId,
            Debit = request.Amount,
            Credit = 0.00m,
            Transaction = transaction
        };

        var creditLine = new JournalLine
        {
            TransactionId = transaction.Id,
            AccountId = request.ToAccountId,
            Debit = 0.00m,
            Credit = request.Amount,
            Transaction = transaction
        };

        await _dbContext.JournalLines.AddRangeAsync(debitLine, creditLine);

        // Update account balances
        fromAccount.Balance -= request.Amount;
        toAccount.Balance += request.Amount;

        await _dbContext.SaveChangesAsync();

        // Publish event
        var paymentEvent = new PaymentProcessed(
            transaction.Id,
            request.FromAccountId,
            request.ToAccountId,
            request.Amount,
            DateTime.UtcNow
        );

        await _outboxService.SaveEventAsync(paymentEvent);

        return new PaymentResponse(
            TransactionId: transaction.Id,
            Status: "Completed",
            Timestamp: DateTime.UtcNow
        );
    }

    public async Task<TransactionDto> GetTransactionAsync(string id)
    {
        var transaction = await _dbContext.Transactions
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
        var query = _dbContext.JournalLines
            .Where(j => j.AccountId == accountId);

        if (asOfDate.HasValue)
        {
            query = query.Where(j => j.Transaction.TimestampUtc <= asOfDate.Value);
        }

        var balance = await query.SumAsync(j => j.Credit - j.Debit);

        var account = await _dbContext.Accounts
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