using System;

namespace MicroLedger.Domain;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Username { get; set; }
    public required string Password { get; set; }
    public string Role { get; set; } = "Customer";
} 