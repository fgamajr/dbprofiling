namespace DbConnect.Core.Models;

public sealed class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ConnectionProfile> Profiles { get; set; } = new List<ConnectionProfile>();
    public ICollection<AnalysisReport> Reports { get; set; } = new List<AnalysisReport>();
}
