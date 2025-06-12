using System.Threading.Tasks;

namespace MicroLedger.Domain.Services
{
    public interface IInterestService
    {
        Task CalculateAndPostInterest();
    }
} 