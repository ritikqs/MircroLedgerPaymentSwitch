using MicroLedger.Application;
using MicroLedger.Domain;

namespace MicroLedger.Application.Services.Interfaces;

public interface ITransactionService
{
    Task<TransactionDto> GetTransactionAsync(string id);
    Task<AccountBalanceDto> GetAccountBalanceAsync(string accountId, DateTime? asOfDate = null);
} 