using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLedger.Application.Services.Interfaces;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain;
using MicroLedger.Domain.Services;
using MicroLedger.Domain.Events;
using MicroLedger.Infrastructure.Services;
using System;
using System.Threading.Tasks;

namespace MicroLedger.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly ILedgerDbContext _db;
    private readonly ILogger<PaymentService> _logger;
    private readonly IOutboxService _outboxService;
    private readonly IBalanceService _balanceService;

    public PaymentService(
        ILedgerDbContext db,
        ILogger<PaymentService> logger,
        IOutboxService outboxService,
        IBalanceService balanceService)
    {
        _db = db;
        _logger = logger;
        _outboxService = outboxService;
        _balanceService = balanceService;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        // Validate request
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be positive");

        // Get accounts
        var fromAccount = await _db.Accounts.FindAsync(request.FromAccountId);
        var toAccount = await _db.Accounts.FindAsync(request.ToAccountId);

        if (fromAccount == null || toAccount == null)
            throw new ArgumentException("One or both accounts not found");

        if (fromAccount.Currency != toAccount.Currency)
            throw new ArgumentException("Accounts must have same currency");

        // Check sufficient funds
        var fromBalance = await _db.JournalLines
            .Where(j => j.AccountId == fromAccount.Id)
            .SumAsync(j => j.Credit - j.Debit);

        if (fromBalance < request.Amount)
            throw new ArgumentException("Insufficient funds");

        // Create transaction
        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            TimestampUtc = DateTime.UtcNow,
            Reference = request.Reference
        };

        _db.Transactions.Add(transaction);

        // Create journal lines
        var debitLine = new JournalLine
        {
            Id = Guid.NewGuid().ToString(),
            TransactionId = transaction.Id,
            AccountId = fromAccount.Id,
            Debit = request.Amount,
            Credit = 0
        };

        var creditLine = new JournalLine
        {
            Id = Guid.NewGuid().ToString(),
            TransactionId = transaction.Id,
            AccountId = toAccount.Id,
            Debit = 0,
            Credit = request.Amount
        };

        _db.JournalLines.Add(debitLine);
        _db.JournalLines.Add(creditLine);
        await _db.SaveChangesAsync();

        // Publish event
        var @event = new TransactionPosted(
            TransactionId: transaction.Id,
            Reference: transaction.Reference,
            TimestampUtc: transaction.TimestampUtc,
            JournalLines: new List<JournalLineDto>
            {
                new(Id: debitLine.Id, AccountId: debitLine.AccountId, Debit: debitLine.Debit, Credit: debitLine.Credit),
                new(Id: creditLine.Id, AccountId: creditLine.AccountId, Debit: creditLine.Debit, Credit: creditLine.Credit)
            }
        );

        await _outboxService.AddEventAsync(@event, @event.EventType);

        // Publish balance updates for both accounts
        await _balanceService.PublishBalanceUpdateAsync(
            fromAccount.Id,
            transaction.Id,
            "Payment"
        );

        await _balanceService.PublishBalanceUpdateAsync(
            toAccount.Id,
            transaction.Id,
            "Payment"
        );

        return new PaymentResponse(
            TransactionId: transaction.Id,
            Status: "Completed",
            Timestamp: transaction.TimestampUtc
        );
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