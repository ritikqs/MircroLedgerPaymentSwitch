using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MicroLedger.Api;
using MicroLedger.Api.Models;

namespace MicroLedger.Tests.Authentication;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestKey = "your-256-bit-secret-key-here-for-testing-purposes-only-12345678901234567890123456789012";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Jwt:Key", TestKey },
                { "Jwt:Issuer", "MicroLedger" },
                { "Jwt:Audience", "MicroLedgerApi" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove any existing JWT settings
            var jwtSettings = services.FirstOrDefault(d => d.ServiceType == typeof(JwtSettings));
            if (jwtSettings != null)
            {
                services.Remove(jwtSettings);
            }

            // Add test JWT settings
            services.AddSingleton(new JwtSettings
            {
                Key = TestKey,
                Issuer = "MicroLedger",
                Audience = "MicroLedgerApi"
            });
        });
    }
} 