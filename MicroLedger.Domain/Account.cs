using System;

namespace MicroLedger.Domain;

public class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string OwnerName { get; set; }
    public required string OwnerId { get; set; }
    public required string Currency { get; set; }
    public AccountType Type { get; set; }
    public decimal Balance { get; set; }
    public decimal InterestRate { get; set; } // Annual interest rate as a decimal (e.g., 0.05 for 5%)
} 