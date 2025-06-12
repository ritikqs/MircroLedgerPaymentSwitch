using MicroLedger.Domain;

namespace MicroLedger.Domain.Events;

public record InterestAccrued(
    string TransactionId,
    string AccountId,
    decimal Amount,
    DateTime TimestampUtc,
    string EventType = "InterestAccrued"
); 