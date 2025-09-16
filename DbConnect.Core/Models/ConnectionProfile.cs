namespace DbConnect.Core.Models;

public record ConnectionProfile(
    string Name,
    DbKind Kind,
    string HostOrFile,
    int? Port,
    string Database,
    string Username,
    string? Password,
    DateTime CreatedAtUtc
)
{
    public int Id { get; init; }
    public int UserId { get; init; }

    public string ConnectionString => Kind switch
    {
        DbKind.PostgreSql => $"Host={HostOrFile};Port={Port ?? 5432};Database={Database};Username={Username};Password={Password};",
        DbKind.SqlServer => $"Server={HostOrFile}{(Port.HasValue ? $",{Port}" : "")};Database={Database};User Id={Username};Password={Password};TrustServerCertificate=true;",
        DbKind.MySql => $"Server={HostOrFile};Port={Port ?? 3306};Database={Database};Uid={Username};Pwd={Password};",
        DbKind.Sqlite => $"Data Source={HostOrFile}",
        _ => throw new NotSupportedException($"Database kind {Kind} not supported")
    };
}
