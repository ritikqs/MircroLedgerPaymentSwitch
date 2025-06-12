using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Infrastructure;
using MicroLedger.Domain;
using System.Threading.Tasks;
using MicroLedger.Application;
using System.Security.Claims;

namespace MicroLedger.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize] // Require JWT authentication
public class AccountsController : ControllerBase
{
    private readonly LedgerDbContext _db;
    private readonly ILogger<AccountsController> _logger;
    
    public AccountsController(LedgerDbContext db, ILogger<AccountsController> logger)
    {
        _db = db;
        _logger = logger;
    }
    
    [HttpGet("{id}/balance")]
    [Authorize(Roles = "Customer,BankAdmin,Admin")]
    [Produces("application/json")] // Explicitly set the response type
    [ProducesResponseType(typeof(AccountBalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountBalanceDto>> GetBalance(string id)
    {
        _logger.LogInformation("GetBalance called for account {AccountId}", id);
        try
        {
            // Get the current user's name from the token
            var username = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
            _logger.LogInformation("User {Username} requesting balance", username);
            
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Invalid token: missing username");
                return Unauthorized("Invalid token: missing username");
            }

            // Get the account details first to check ownership
            var account = await _db.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found", id);
                return NotFound($"Account {id} not found");
            }

            // Check if user is admin or account owner
            var isAdmin = User.IsInRole("BankAdmin") || User.IsInRole("Admin");
            if (!isAdmin && account.OwnerId != username)
            {
                _logger.LogWarning("User {Username} not authorized to access account {AccountId}", username, id);
                return Forbid("You don't have permission to access this account");
            }

            var query = _db.JournalLines
                .Where(j => j.AccountId == id);

            var balance = await query.SumAsync(j => j.Credit - j.Debit);

            var response = new AccountBalanceDto(
                AccountId: id,
                Balance: balance,
                Currency: account.Currency,
                AsOfDate: DateTime.UtcNow
            );

            _logger.LogInformation("Successfully retrieved balance for account {AccountId}", id);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving balance for account {AccountId}", id);
            return StatusCode(500, "An error occurred while retrieving the balance");
        }
    }
} 