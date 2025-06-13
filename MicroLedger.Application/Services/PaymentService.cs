using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain;
using MicroLedger.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MicroLedger.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly ILedgerDbContext _db;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        ILedgerDbContext db,
        IOutboxService outboxService,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _outboxService = outboxService;
        _logger = logger;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        try
        {
            // Get source and destination accounts
            var sourceAccount = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.FromAccountId);

            var destinationAccount = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.ToAccountId);

            if (sourceAccount == null || destinationAccount == null)
            {
                throw new ArgumentException("One or both accounts not found");
            }

            // Calculate source account balance
            var sourceBalance = await _db.JournalLines
                .Where(j => j.AccountId == sourceAccount.Id)
                .SumAsync(j => j.Credit - j.Debit);

            // Check if source account has sufficient funds
            if (sourceBalance < request.Amount)
            {
                throw new InvalidOperationException("Insufficient funds");
            }

            // Create transaction
            var transaction = new Transaction
            {
                Reference = request.Reference
            };

            await _db.Transactions.AddAsync(transaction);
            await _db.SaveChangesAsync();

            // Create journal lines
            var sourceLine = new JournalLine
            {
                TransactionId = transaction.Id,
                AccountId = sourceAccount.Id,
                Debit = request.Amount,
                Credit = 0,
                Transaction = transaction
            };

            var destinationLine = new JournalLine
            {
                TransactionId = transaction.Id,
                AccountId = destinationAccount.Id,
                Debit = 0,
                Credit = request.Amount,
                Transaction = transaction
            };

            await _db.JournalLines.AddRangeAsync(sourceLine, destinationLine);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Payment of {Amount} processed from {SourceAccount} to {DestinationAccount}",
                request.Amount,
                sourceAccount.Id,
                destinationAccount.Id
            );

            return new PaymentResponse(
                TransactionId: transaction.Id,
                Status: "Completed",
                Timestamp: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment");
            throw;
        }
    }

    public async Task<TransactionDto> GetTransactionAsync(string id)
    {
        var transaction = await _db.Transactions
            .Include(t => t.JournalLines)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
        {
            return null;
        }

        return new TransactionDto(
            Id: transaction.Id,
            Reference: transaction.Reference,
            TimestampUtc: transaction.TimestampUtc,
            JournalLines: transaction.JournalLines.Select(j => new JournalLineDto(
                Id: j.Id,
                AccountId: j.AccountId,
                Debit: j.Debit,
                Credit: j.Credit
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

        return new AccountBalanceDto(
            AccountId: accountId,
            Balance: balance,
            Currency: account?.Currency ?? "USD",
            AsOfDate: asOfDate ?? DateTime.UtcNow
        );
    }
} 