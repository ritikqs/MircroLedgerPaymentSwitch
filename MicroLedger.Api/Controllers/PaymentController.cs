using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Infrastructure;
using MicroLedger.Domain;
using System.Threading.Tasks;

namespace MicroLedger.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentController : ControllerBase
{
    private readonly LedgerDbContext _db;
    
    public PaymentController(LedgerDbContext db) => _db = db;
    
    [HttpPost]
    public async Task<IActionResult> PostPayment([FromBody] PaymentRequest request)
    {
        // Basic validation
        if (request.Amount <= 0) return BadRequest("Amount must be positive");
        
        var fromAccount = await _db.Accounts.FindAsync(request.FromAccountId);
        var toAccount = await _db.Accounts.FindAsync(request.ToAccountId);
        
        if (fromAccount == null || toAccount == null) return NotFound();
        if (fromAccount.Currency != toAccount.Currency) return BadRequest("Currency mismatch");
        
        // Check balance (simplified)
        var fromBalance = await GetBalance(request.FromAccountId);
        if (fromBalance < request.Amount) return BadRequest("Insufficient funds");
        
        // Create transaction
        var tx = new Transaction { Reference = request.Reference };
        
        tx.JournalLines.Add(new JournalLine {
            AccountId = request.FromAccountId,
            Debit = 0,
            Credit = request.Amount
        });
        
        tx.JournalLines.Add(new JournalLine {
            AccountId = request.ToAccountId,
            Debit = request.Amount,
            Credit = 0
        });
        
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetTransaction), new { id = tx.Id }, tx);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Transaction>> GetTransaction(string id)
    {
        var transaction = await _db.Transactions
            .Include(t => t.JournalLines)
            .FirstOrDefaultAsync(t => t.Id == id);
            
        if (transaction == null) return NotFound();
        
        return transaction;
    }
    
    private async Task<decimal> GetBalance(string accountId)
    {
        var credits = await _db.JournalLines
            .Where(j => j.AccountId == accountId)
            .SumAsync(j => j.Credit);
            
        var debits = await _db.JournalLines
            .Where(j => j.AccountId == accountId)
            .SumAsync(j => j.Debit);
            
        return credits - debits;
    }
}

public record PaymentRequest(
    string FromAccountId, 
    string ToAccountId, 
    decimal Amount, 
    string Currency, 
    string Reference); 