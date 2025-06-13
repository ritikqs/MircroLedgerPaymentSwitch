using System.Threading.Tasks;

namespace MicroLedger.Domain.Services
{
    public interface IBalanceService
    {
        Task PublishBalanceUpdateAsync(string accountId, string transactionId, string description);
    }
} 