using System;
using System.Threading.Tasks;
using DbConnect.Core.Models;
using DbConnect.Core.Services;
using Xunit;

namespace DbConnect.Tests;

public class ConnectionTesterTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public ConnectionTesterTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Connects_To_Postgres_Container()
    {
        var profile = new ConnectionProfile(
            Name: "pg-test",
            Kind: DbKind.PostgreSql,
            HostOrFile: _fx.Host,
            Port: _fx.Port,
            Database: _fx.Db,
            Username: _fx.User,
            Password: _fx.Pass,
            CreatedAtUtc: DateTime.UtcNow
        );

        var tester = new ConnectionTester();
        var (ok, msg) = await tester.TestAsync(profile);
        Assert.True(ok, msg);
    }
}
