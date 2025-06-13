namespace MicroLedger.Domain.Interfaces;

public interface IInterestService
{
    Task AccrueDailyInterestAsync(decimal annualRate);
} 