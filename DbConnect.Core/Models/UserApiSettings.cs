namespace DbConnect.Core.Models;

public class UserApiSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = ""; // "openai", "claude", etc.
    public string ApiKeyEncrypted { get; set; } = ""; 
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastValidatedAtUtc { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}