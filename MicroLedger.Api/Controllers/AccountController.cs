using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Infrastructure;
using System.Threading.Tasks;

namespace MicroLedger.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountController : ControllerBase
{
    private readonly LedgerDbContext _db;
    
    public AccountController(LedgerDbContext db) => _db = db;
    
    [HttpGet("{id}/balance")]
    public async Task<ActionResult<decimal>> GetBalance(string id)
    {
        var credits = await _db.JournalLines
            .Where(j => j.AccountId == id)
            .SumAsync(j => j.Credit);
            
        var debits = await _db.JournalLines
            .Where(j => j.AccountId == id)
            .SumAsync(j => j.Debit);
            
        return credits - debits;
    }
} 