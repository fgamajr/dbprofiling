using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace DbConnect.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public IContainer Container { get; private set; } = default!;
    public string Host => "localhost";
    public int Port { get; private set; }
    public string User => "test_user";
    public string Pass => "test_password";
    public string Db   => "test_db";

    public async Task InitializeAsync()
    {
        var container = new ContainerBuilder()
            .WithImage("postgres:15-alpine")
            .WithEnvironment("POSTGRES_DB", Db)
            .WithEnvironment("POSTGRES_USER", User)
            .WithEnvironment("POSTGRES_PASSWORD", Pass)
            .WithPortBinding(0, true) // Bind to a random available port
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await container.StartAsync();
        Container = container;
        Port = container.GetMappedPublicPort(5432);
    }

    public async Task DisposeAsync() => await Container.DisposeAsync();
}
