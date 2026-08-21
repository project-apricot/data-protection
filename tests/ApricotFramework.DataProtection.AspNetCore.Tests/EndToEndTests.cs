using ApricotFramework.DataProtection.AspNetCore.Extensions;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.DataProtection.AspNetCore.Tests;

/// <summary>
/// Exercises the real key manager against a real database.
/// </summary>
public sealed class EndToEndTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"apricot-dp-e2e-{Guid.NewGuid():N}.db");

    private readonly ProtectionKeyStoreOptions options = new() { Dialect = SqliteProtectionKeyDialect.DialectName };

    public EndToEndTests()
    {
        using var connection = this.Open();
        using var command = connection.CreateCommand();
        command.CommandText = new SqliteProtectionKeyDialect().GetCreateTableScript(this.options);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={this.path}");
        connection.Open();

        return connection;
    }

    private static IConfiguration EmptyConfiguration()
    {
        return new ConfigurationBuilder().Build();
    }

    private ServiceProvider BuildHost(string application)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName(application)
            .PersistKeysToRelationalStore(
                _ => this.Open(),
                store =>
                {
                    store.Dialect = SqliteProtectionKeyDialect.DialectName;
                    store.TableName = this.options.TableName;
                })
            .AllowUnprotectedKeys();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private int CountRows()
    {
        using var connection = this.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{this.options.TableName}\"";

        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Protect_ThenUnprotect_RoundTripsThroughTheDatabase()
    {
        using var host = this.BuildHost("/app");
        var protector = host.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose");

        var payload = protector.Protect("Hello, World!");

        Assert.Equal("Hello, World!", protector.Unprotect(payload));
        Assert.Equal(1, this.CountRows());
    }

    [Fact]
    public void Payload_SurvivesARestart_BecauseTheKeyRingIsPersisted()
    {
        string payload;

        using (var first = this.BuildHost("/app"))
        {
            payload = first.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("test-purpose")
                .Protect("durable");
        }

        // A brand new host, sharing only the database. This is the whole point of the library.
        using var second = this.BuildHost("/app");

        Assert.Equal(
            "durable",
            second.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("test-purpose")
                .Unprotect(payload));
        Assert.Equal(1, this.CountRows());
    }

    [Fact]
    public void DifferentApplicationDiscriminator_CannotUnprotect()
    {
        string payload;

        using (var mine = this.BuildHost("/app-one"))
        {
            payload = mine.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("test-purpose")
                .Protect("secret");
        }

        using var theirs = this.BuildHost("/app-two");
        var protector = theirs.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose");

        Assert.ThrowsAny<Exception>(() => protector.Unprotect(payload));
    }

    [Fact]
    public void DifferentPurpose_CannotUnprotect()
    {
        using var host = this.BuildHost("/app");
        var provider = host.GetRequiredService<IDataProtectionProvider>();

        var payload = provider.CreateProtector("purpose-one").Protect("secret");

        Assert.ThrowsAny<Exception>(() => provider.CreateProtector("purpose-two").Unprotect(payload));
    }

    [Fact]
    public void StoredKeyRing_UsesTheFixedColumns()
    {
        using var host = this.BuildHost("/app");
        host.GetRequiredService<IDataProtectionProvider>().CreateProtector("p").Protect("x");

        using var connection = this.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"FriendlyName\", \"Xml\" FROM \"{this.options.TableName}\"";

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(0)));
        Assert.StartsWith("<key", reader.GetString(1), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(this.path);
    }
}
