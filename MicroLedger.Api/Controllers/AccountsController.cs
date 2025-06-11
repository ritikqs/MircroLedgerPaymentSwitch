using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Infrastructure;
using MicroLedger.Domain;
using System.Threading.Tasks;
using MicroLedger.Api.DTOs;

namespace MicroLedger.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize] // Require JWT authentication
public class AccountsController : ControllerBase
{
    private readonly LedgerDbContext _db;
    
    public AccountsController(LedgerDbContext db) => _db = db;
    
    [HttpGet("{accountId}/balance")]
    public async Task<ActionResult<AccountBalanceDto>> GetBalance(
        string accountId,
        [FromQuery] DateTime? asOfDate = null)
    {
        try
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
                return NotFound($"Account {accountId} not found");
            }

            return new AccountBalanceDto(
                AccountId: accountId,
                Balance: balance,
                Currency: account.Currency,
                AsOfDate: asOfDate ?? DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred while retrieving the balance");
        }
    }
} 