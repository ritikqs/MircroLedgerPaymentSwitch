using System;
using System.Threading.Tasks;

namespace MicroLedger.Domain.Interfaces;

public interface IInterestService
{
    Task AccrueInterestAsync(string accountId, DateTime asOfDate);
} 