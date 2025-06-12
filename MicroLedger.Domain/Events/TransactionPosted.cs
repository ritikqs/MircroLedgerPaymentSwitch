using MicroLedger.Domain;

namespace MicroLedger.Domain.Events;

public record TransactionPosted(
    string TransactionId,
    string Reference,
    DateTime TimestampUtc,
    List<JournalLineDto> JournalLines,
    string EventType = "TransactionPosted"
); 