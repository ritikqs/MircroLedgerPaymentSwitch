using MicroLedger.Domain;

namespace MicroLedger.Domain.Events;

public record BalanceUpdated(
    string AccountId,
    decimal Balance,
    string Currency,
    string TransactionId,
    string TransactionType,
    DateTime TimestampUtc,
    string EventType = "BalanceUpdated"
); 