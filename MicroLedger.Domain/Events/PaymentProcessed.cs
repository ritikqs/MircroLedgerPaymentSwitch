using MicroLedger.Domain;

namespace MicroLedger.Domain.Events;

public record PaymentProcessed(
    string TransactionId,
    string FromAccountId,
    string ToAccountId,
    decimal Amount,
    DateTime TimestampUtc,
    string EventType = "PaymentProcessed"
); 