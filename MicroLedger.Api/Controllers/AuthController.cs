using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MicroLedger.Infrastructure;
using MicroLedger.Domain;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MicroLedger.Api.Models;

namespace MicroLedger.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly LedgerDbContext _db;
    private readonly JwtSettings _jwtSettings;

    public AuthController(LedgerDbContext db, JwtSettings jwtSettings)
    {
        _db = db;
        _jwtSettings = jwtSettings ?? throw new ArgumentNullException(nameof(jwtSettings));

        // Early validation of JWT settings
        if (string.IsNullOrWhiteSpace(_jwtSettings.Key))
            throw new ArgumentException("JWT Key is not configured");
        if (string.IsNullOrWhiteSpace(_jwtSettings.Issuer))
            throw new ArgumentException("JWT Issuer is not configured");
        if (string.IsNullOrWhiteSpace(_jwtSettings.Audience))
            throw new ArgumentException("JWT Audience is not configured");
    }

    [HttpPost("login")]
    public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Password == request.Password);

        if (user == null)
        {
            return Unauthorized("Invalid username or password");
        }

        try
        {
            var token = GenerateJwtToken(user);
            return Ok(new
            {
                token,
                expiresIn = 3600, // 1 hour in seconds
                role = user.Role
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Token generation failed: {ex.Message}");
        }
    }

    private string GenerateJwtToken(User user)
    {
        // Validate inputs
        if (user == null) throw new ArgumentNullException(nameof(user));
        if (string.IsNullOrWhiteSpace(user.Username)) throw new ArgumentException("Username is required");
        if (string.IsNullOrWhiteSpace(user.Role)) throw new ArgumentException("User role is required");

        // These checks are redundant since we validate in constructor,
        // but provide extra safety during token generation
        if (string.IsNullOrWhiteSpace(_jwtSettings.Key))
            throw new InvalidOperationException("JWT Key is not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            // Add any additional claims here
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}