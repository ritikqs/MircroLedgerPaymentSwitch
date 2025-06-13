using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using MicroLedger.Api;
using MicroLedger.Api.Models;

namespace MicroLedger.Tests.Authentication;

[Trait("Category", "Authentication")]
public class JwtAuthenticationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JwtSettings _jwtSettings;
    private const string TestKey = "your-256-bit-secret-key-here-for-testing-purposes-only-12345678901234567890123456789012";

    public JwtAuthenticationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jwtSettings = new JwtSettings
        {
            Key = TestKey,
            Issuer = "MicroLedger",
            Audience = "MicroLedgerApi"
        };
    }

    [Fact]
    [Trait("Category", "JWT")]
    public async Task Unauthorized_WhenNoTokenProvided()
    {
        // Act
        var response = await _client.GetAsync("/api/test/secure-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "JWT")]
    public async Task Unauthorized_WhenInvalidTokenProvided()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _client.GetAsync("/api/test/secure-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "JWT")]
    public async Task Unauthorized_WhenTokenWithInvalidSignature()
    {
        // Arrange
        var token = GenerateJwtToken("invalid-key-that-is-long-enough-for-hs256-algorithm-12345678901234567890123456789012");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/test/secure-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "JWT")]
    public async Task Unauthorized_WhenTokenExpired()
    {
        // Arrange
        var notBefore = DateTime.UtcNow.AddHours(-2);
        var expires = DateTime.UtcNow.AddHours(-1);
        var token = GenerateJwtToken(_jwtSettings.Key, notBefore: notBefore, expires: expires);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/test/secure-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "JWT")]
    public async Task Unauthorized_WhenTokenWithInvalidIssuer()
    {
        // Arrange
        var token = GenerateJwtToken(_jwtSettings.Key, issuer: "InvalidIssuer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/test/secure-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "JWT")]
    public async Task Unauthorized_WhenTokenWithInvalidAudience()
    {
        // Arrange
        var token = GenerateJwtToken(_jwtSettings.Key, audience: "InvalidAudience");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/test/secure-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "JWT")]
    public async Task Authorized_WhenValidTokenProvided()
    {
        // Arrange
        var token = GenerateValidToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/test/secure-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private string GenerateValidToken()
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "User")
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateJwtToken(string key, DateTime? notBefore = null, DateTime? expires = null, string issuer = null, string audience = null)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "User")
        };

        var token = new JwtSecurityToken(
            issuer: issuer ?? _jwtSettings.Issuer,
            audience: audience ?? _jwtSettings.Audience,
            claims: claims,
            notBefore: notBefore ?? DateTime.UtcNow,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
} 