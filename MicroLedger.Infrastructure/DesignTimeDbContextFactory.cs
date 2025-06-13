using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MicroLedger.Infrastructure;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var builder = new DbContextOptionsBuilder<LedgerDbContext>();
        var connectionString = configuration.GetConnectionString("LedgerDb");
        builder.UseSqlServer(connectionString);

        return new LedgerDbContext(builder.Options);
    }
} 