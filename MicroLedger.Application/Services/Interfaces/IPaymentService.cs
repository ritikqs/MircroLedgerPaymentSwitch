using MicroLedger.Application;

namespace MicroLedger.Application.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request);
    Task<TransactionDto> GetTransactionAsync(string id);
    Task<AccountBalanceDto> GetAccountBalanceAsync(string accountId, DateTime? asOfDate = null);
} 