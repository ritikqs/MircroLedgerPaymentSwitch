namespace MicroLedger.Api.DTOs;

public record JournalLineDto(
    string Id,
    string AccountId,
    decimal Debit,
    decimal Credit); 