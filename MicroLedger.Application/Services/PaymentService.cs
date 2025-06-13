using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Domain.Interfaces;
using MicroLedger.Domain;
using MicroLedger.Application.Services.Interfaces;
using MicroLedger.Domain.Services;
using MicroLedger.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MicroLedger.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly ILedgerDbContext _db;
    private readonly IOutboxService _outboxService;
    private readonly IBalanceService _balanceService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        ILedgerDbContext db,
        IOutboxService outboxService,
        IBalanceService balanceService,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _outboxService = outboxService;
        _balanceService = balanceService;
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
            var sourceJournalLine = new JournalLine
            {
                TransactionId = transaction.Id,
                AccountId = sourceAccount.Id,
                Debit = request.Amount,
                Credit = 0,
                Transaction = transaction
            };

            var destinationJournalLine = new JournalLine
            {
                TransactionId = transaction.Id,
                AccountId = destinationAccount.Id,
                Debit = 0,
                Credit = request.Amount,
                Transaction = transaction
            };

            await _db.JournalLines.AddRangeAsync(new[] { sourceJournalLine, destinationJournalLine });
            await _db.SaveChangesAsync();

            // Publish events
            var paymentEvent = new PaymentProcessed(
                transaction.Id,
                sourceAccount.Id,
                destinationAccount.Id,
                request.Amount,
                DateTime.UtcNow
            );

            await _outboxService.SaveEventAsync(paymentEvent);

            // Publish balance updates
            await _balanceService.PublishBalanceUpdateAsync(sourceAccount.Id, transaction.Id, "Payment sent");
            await _balanceService.PublishBalanceUpdateAsync(destinationAccount.Id, transaction.Id, "Payment received");

            _logger.LogInformation(
                "Payment of {Amount} processed from {FromAccount} to {ToAccount}",
                request.Amount,
                request.FromAccountId,
                request.ToAccountId
            );

            return new PaymentResponse(
                TransactionId: transaction.Id,
                Status: "Completed",
                Timestamp: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment from {FromAccount} to {ToAccount}", request.FromAccountId, request.ToAccountId);
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
            throw new ArgumentException($"Transaction {id} not found");
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