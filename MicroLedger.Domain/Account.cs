using System;

namespace MicroLedger.Domain;

public class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OwnerName { get; set; }
    public string Currency { get; set; } = "USD";
    public AccountType Type { get; set; }
    public string OwnerId { get; set; } // For authz
    public decimal InterestRate { get; set; } // Annual interest rate as a decimal (e.g., 0.05 for 5%)
} 