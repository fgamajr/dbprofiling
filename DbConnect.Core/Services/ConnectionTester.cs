using System;
using System.Data.Common;
using System.Threading.Tasks;
using DbConnect.Core.Abstractions;
using DbConnect.Core.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using System.Data.SqlClient;

namespace DbConnect.Core.Services;

public sealed class ConnectionTester : IConnectionTester
{
    public string BuildConnectionString(ConnectionProfile p) => p.Kind switch
    {
        DbKind.PostgreSql => new NpgsqlConnectionStringBuilder {
            Host = p.HostOrFile, Port = p.Port ?? 5432, Database = p.Database,
            Username = p.Username, Password = p.Password ?? ""
        }.ToString(),
        DbKind.SqlServer => new SqlConnectionStringBuilder {
            DataSource = $"{p.HostOrFile},{p.Port ?? 1433}",
            InitialCatalog = p.Database, UserID = p.Username, Password = p.Password ?? "",
            TrustServerCertificate = true
        }.ToString(),
        DbKind.MySql => new MySqlConnectionStringBuilder {
            Server = p.HostOrFile, Port = (uint)(p.Port ?? 3306), Database = p.Database,
            UserID = p.Username, Password = p.Password ?? ""
        }.ToString(),
        DbKind.Sqlite => new SqliteConnectionStringBuilder {
            DataSource = string.IsNullOrWhiteSpace(p.HostOrFile) ? ":memory:" : p.HostOrFile
        }.ToString(),
        _ => throw new NotSupportedException()
    };

    public async Task<(bool ok, string message)> TestAsync(ConnectionProfile profile)
    {
        try
        {
            using var conn = CreateConnection(profile);
            await conn.OpenAsync();
            var _ = await conn.ExecuteScalarAsync<object>("SELECT 1");
            await conn.CloseAsync();
            return (true, $"Conexão OK ({profile.Kind})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private DbConnection CreateConnection(ConnectionProfile p) => p.Kind switch
    {
        DbKind.PostgreSql => new NpgsqlConnection(BuildConnectionString(p)),
        DbKind.SqlServer  => new SqlConnection(BuildConnectionString(p)),
        DbKind.MySql      => new MySqlConnection(BuildConnectionString(p)),
        DbKind.Sqlite     => new SqliteConnection(BuildConnectionString(p)),
        _ => throw new NotSupportedException()
    };
}
