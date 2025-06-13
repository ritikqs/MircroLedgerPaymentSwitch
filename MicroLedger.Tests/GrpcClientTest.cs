using Grpc.Core;
using Grpc.Net.Client;
using MicroLedger.Domain.Protos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Xunit;
using System.Runtime.CompilerServices;

namespace MicroLedger.Tests;

public static class GrpcExtensions
{
    public static async IAsyncEnumerable<T> ReadAllAsync<T>(this IAsyncStreamReader<T> stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await stream.MoveNext(cancellationToken))
        {
            yield return stream.Current;
        }
    }
}

public class GrpcClientTest : IClassFixture<TestFixture>
{
    private readonly ILogger<GrpcClientTest> _logger;
    private readonly TestFixture _fixture;
    private readonly string _serverAddress;

    public GrpcClientTest(TestFixture fixture)
    {
        _fixture = fixture;
        _logger = fixture.LoggerFactory.CreateLogger<GrpcClientTest>();
        // Get server address from environment variable or use default
        _serverAddress = Environment.GetEnvironmentVariable("GRPC_SERVER_ADDRESS") ?? "https://localhost:5001";
    }

    [Fact(Skip = "Requires running gRPC server")]
    public async Task StreamBalanceUpdates_ShouldReceiveUpdates()
    {
        // Skip test if server is not available
        if (!await IsServerAvailable())
        {
            _logger.LogWarning("gRPC server is not available at {ServerAddress}. Skipping test.", _serverAddress);
            return;
        }

        // Arrange
        using var channel = GrpcChannel.ForAddress(_serverAddress);
        var client = new MicroLedger.Domain.Protos.BalanceService.BalanceServiceClient(channel);
        var accountId = "test-account-1";
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act & Assert
        try
        {
            using var call = client.StreamBalanceUpdates(
                new BalanceRequest { AccountId = accountId },
                cancellationToken: cts.Token);

            var updates = new List<BalanceUpdate>();
            await foreach (var update in call.ResponseStream.ReadAllAsync(cts.Token))
            {
                updates.Add(update);
                _logger.LogInformation(
                    "Received balance update for account {AccountId}: {Balance} {Currency}",
                    update.AccountId,
                    update.Balance,
                    update.Currency);

                if (updates.Count >= 3)
                {
                    break;
                }
            }

            Assert.NotEmpty(updates);
            Assert.All(updates, update => Assert.Equal(accountId, update.AccountId));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogError(ex, "Failed to connect to gRPC server at {ServerAddress}", _serverAddress);
            // Skip the test by returning early
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in gRPC test");
            throw;
        }
    }

    private async Task<bool> IsServerAvailable()
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(_serverAddress);
            var client = new MicroLedger.Domain.Protos.BalanceService.BalanceServiceClient(channel);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            using var call = client.StreamBalanceUpdates(
                new BalanceRequest { AccountId = "test" },
                cancellationToken: cts.Token);

            // Try to read one message to verify connection
            await call.ResponseStream.MoveNext(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class TestFixture : IDisposable
{
    public ILoggerFactory LoggerFactory { get; }

    public TestFixture()
    {
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddConsole();
        });
    }

    public void Dispose()
    {
        LoggerFactory.Dispose();
    }
} 