using ApricotFramework.DataProtection.AspNetCore.Extensions;
using ApricotFramework.DataProtection.Dialects;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.DataProtection.AspNetCore.Tests;

/// <summary>
/// The connection factory receives the service provider, so the connection string can come from
/// anywhere the container can reach rather than having to be known at registration.
/// </summary>
public sealed class ConnectionFactoryTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"apricot-dp-cf-{Guid.NewGuid():N}.db");

    public ConnectionFactoryTests()
    {
        using var connection = new SqliteConnection($"Data Source={this.path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = new SqliteProtectionKeyDialect()
            .GetCreateTableScript(new ApricotFramework.DataProtection.Options.ProtectionKeyStoreOptions());
        command.ExecuteNonQuery();
    }

    private IConfiguration Configuration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Keys"] = $"Data Source={this.path}",
                ["DataProtection:Storage:Relational:Dialect"] = "Sqlite",
            })
            .Build();
    }

    [Fact]
    public void ConnectionString_ResolvedFromConfigurationThroughTheProvider_Works()
    {
        var configuration = this.Configuration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddDataProtectionCore(configuration)
            .PersistKeysToRelationalStore(sp =>
                new SqliteConnection(sp.GetRequiredService<IConfiguration>().GetConnectionString("Keys")))
            .AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = provider.GetRequiredService<IProtectionKeyStore>();

        store.Add("from-provider", "<key />");

        Assert.Equal("from-provider", Assert.Single(store.GetAll()).FriendlyName);
    }

    [Fact]
    public void ConnectionString_CapturedInAClosure_AlsoWorks()
    {
        var configuration = this.Configuration();
        var connectionString = configuration.GetConnectionString("Keys");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtectionCore(configuration)
            .PersistKeysToRelationalStore(_ => new SqliteConnection(connectionString))
            .AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = provider.GetRequiredService<IProtectionKeyStore>();

        store.Add("from-closure", "<key />");

        Assert.Equal("from-closure", Assert.Single(store.GetAll()).FriendlyName);
    }

    [Fact]
    public void ScopedServiceFromTheFactory_Throws_BecauseTheStoreIsASingleton()
    {
        var configuration = this.Configuration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddScoped<ScopedConnectionString>();
        services.AddDataProtectionCore(configuration)
            .PersistKeysToRelationalStore(sp =>
                new SqliteConnection(sp.GetRequiredService<ScopedConnectionString>().Value))
            .AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = provider.GetRequiredService<IProtectionKeyStore>();

        // The key ring is read outside any request, so the factory runs on the root provider.
        Assert.ThrowsAny<InvalidOperationException>(() => store.GetAll());
    }

    private sealed class ScopedConnectionString
    {
        public string Value { get; } = "Data Source=:memory:";
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(this.path);
    }
}
