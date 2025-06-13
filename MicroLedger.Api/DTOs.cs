namespace MicroLedger.Api.DTOs;

public record PaymentRequest(
    string FromAccountId,
    string ToAccountId,
    decimal Amount,
    string Currency,
    string Reference
);

public record PaymentResponse(
    string TransactionId,
    string Status,
    DateTime Timestamp
);

public record AccountBalanceDto(
    string AccountId,
    decimal Balance,
    string Currency,
    DateTime AsOfDate
);

public record TransactionDto(
    string Id,
    string Reference,
    DateTime TimestampUtc,
    List<JournalLineDto> JournalLines
);

public record JournalLineDto(
    string Id,
    string AccountId,
    decimal Debit,
    decimal Credit
); 