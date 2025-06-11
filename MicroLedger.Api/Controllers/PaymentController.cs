using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MicroLedger.Infrastructure;
using MicroLedger.Domain;
using System.Threading.Tasks;
using MicroLedger.Api.DTOs;

namespace MicroLedger.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly LedgerDbContext _db;
    
    public PaymentController(LedgerDbContext db) => _db = db;
    
    [HttpGet("accounts/{accountId}/balance")]
    public async Task<ActionResult<AccountBalanceDto>> GetAccountBalance(
        string accountId,
        [FromQuery] DateTime? asOfDate = null)
    {
        try
        {
            var balance = await GetBalance(accountId, asOfDate);
            return Ok(balance);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> ProcessPayment([FromBody] PaymentRequest request)
    {
        // Validate request
        if (request.Amount <= 0)
            return BadRequest("Amount must be positive");
            
        // Get accounts
        var fromAccount = await _db.Accounts.FindAsync(request.FromAccountId);
        var toAccount = await _db.Accounts.FindAsync(request.ToAccountId);
        
        if (fromAccount == null || toAccount == null)
            return NotFound("One or both accounts not found");
            
        if (fromAccount.Currency != toAccount.Currency)
            return BadRequest("Accounts must have same currency");
            
        // Check sufficient funds
        var fromBalance = await _db.JournalLines
            .Where(j => j.AccountId == fromAccount.Id)
            .SumAsync(j => j.Credit - j.Debit);
            
        if (fromBalance < request.Amount)
            return BadRequest("Insufficient funds");
            
        // Create transaction
        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            TimestampUtc = DateTime.UtcNow,
            Reference = request.Reference
        };
        
        _db.Transactions.Add(transaction);
        
        // Create journal lines
        _db.JournalLines.Add(new JournalLine
        {
            Id = Guid.NewGuid().ToString(),
            TransactionId = transaction.Id,
            AccountId = fromAccount.Id,
            Debit = request.Amount,
            Credit = 0
        });
        
        _db.JournalLines.Add(new JournalLine
        {
            Id = Guid.NewGuid().ToString(),
            TransactionId = transaction.Id,
            AccountId = toAccount.Id,
            Debit = 0,
            Credit = request.Amount
        });
        
        await _db.SaveChangesAsync();
        
        return CreatedAtAction(
            nameof(GetTransaction),
            new { id = transaction.Id },
            new PaymentResponse(transaction.Id)
        );
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Transaction>> GetTransaction(string id)
    {
        var transaction = await _db.Transactions
            .Include(t => t.JournalLines)
            .FirstOrDefaultAsync(t => t.Id == id);
            
        if (transaction == null)
            return NotFound();
            
        return transaction;
    }
    
    private async Task<AccountBalanceDto> GetBalance(string accountId, DateTime? asOfDate = null)
    {
        var query = _db.JournalLines
            .Where(j => j.AccountId == accountId);

        // If asOfDate is provided, only consider transactions up to that date
        if (asOfDate.HasValue)
        {
            query = query.Where(j => j.Transaction.TimestampUtc <= asOfDate.Value);
        }

        var balance = await query.SumAsync(j => j.Credit - j.Debit);

        // Get the account details
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

public record PaymentRequest(
    string FromAccountId, 
    string ToAccountId, 
    decimal Amount, 
    string Reference); 