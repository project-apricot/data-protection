using ApricotFramework.DataOps;
using ApricotFramework.DataProtection.AspNetCore.Extensions;
using ApricotFramework.DataProtection.DataOps.Extensions;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.DataProtection.DataOps.Tests;

public sealed class DataOpsProtectionKeyStoreTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"apricot-dp-dataops-{Guid.NewGuid():N}.db");

    private readonly ProtectionKeyStoreOptions options = new();

    public DataOpsProtectionKeyStoreTests()
    {
        using var connection = new SqliteConnection($"Data Source={this.path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = new SqliteProtectionKeyDialect().GetCreateTableScript(this.options);
        command.ExecuteNonQuery();
    }

    private static IConfiguration EmptyConfiguration()
    {
        return new ConfigurationBuilder().Build();
    }

    private static ServiceProvider BuildHost(StubConnectionRegistry registry, string? dataSource = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConnectionRegistry>(registry);
        services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName("/app")
            .PersistKeysToDataOpsStore(_ => { }, dataSource)
            .AllowUnprotectedKeys();

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void Store_TakesItsDialectFromTheDataSourceProvider()
    {
        var registry = new StubConnectionRegistry(this.path);
        using var host = BuildHost(registry);

        var store = host.GetRequiredService<IProtectionKeyStore>();
        store.Add("key-1", "<key />");

        Assert.Equal("<key />", Assert.Single(store.GetAll()).Xml);
    }

    [Fact]
    public void Store_ReleasesEveryReferenceItTakes()
    {
        var registry = new StubConnectionRegistry(this.path);
        using var host = BuildHost(registry);

        var store = host.GetRequiredService<IProtectionKeyStore>();
        store.Add("key-1", "<key />");
        store.GetAll();

        // The probe taken at construction counts too, and disposing the reference is the
        // documented way to release a registry connection.
        Assert.Equal(registry.ReferencesCreated, registry.ReferencesDisposed);
    }

    [Fact]
    public void Protect_ThenUnprotect_RoundTripsThroughTheDataSource()
    {
        var registry = new StubConnectionRegistry(this.path);
        using var host = BuildHost(registry);

        var protector = host.GetRequiredService<IDataProtectionProvider>().CreateProtector("purpose");
        var payload = protector.Protect("secret");

        Assert.Equal("secret", protector.Unprotect(payload));
    }

    [Fact]
    public void UnknownDataSource_FailsWhenTheStoreIsBuilt()
    {
        var registry = new StubConnectionRegistry(this.path);
        using var host = BuildHost(registry, "Missing");

        var error = Assert.Throws<InvalidOperationException>(
            () => host.GetRequiredService<IProtectionKeyStore>());
        Assert.Contains("Missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderWithoutADialect_IsReportedClearly()
    {
        var registry = new StubConnectionRegistry(this.path, (SqlProvider)999);
        using var host = BuildHost(registry);

        var error = Assert.Throws<InvalidOperationException>(
            () => host.GetRequiredService<IProtectionKeyStore>());
        Assert.Contains("No data protection key dialect", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitDialect_OverridesTheDataSourceProvider()
    {
        // The data source claims MySQL; the host says SQLite, which is what actually runs.
        var registry = new StubConnectionRegistry(this.path, SqlProvider.MySql);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConnectionRegistry>(registry);
        services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName("/app")
            .PersistKeysToDataOpsStore(
                options => options.Dialect = SqliteProtectionKeyDialect.DialectName,
                dataSource: null);

        using var host = services.BuildServiceProvider(validateScopes: true);
        var store = host.GetRequiredService<IProtectionKeyStore>();

        store.Add("key-1", "<key />");

        Assert.Single(store.GetAll());
    }

    [Fact]
    public void HostRegisteredStore_StillWins()
    {
        var registry = new StubConnectionRegistry(this.path);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConnectionRegistry>(registry);
        services.AddSingleton<IProtectionKeyStore>(new ThrowingStore());
        services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName("/app")
            .PersistKeysToDataOpsStore(_ => { }, dataSource: null);

        using var host = services.BuildServiceProvider(validateScopes: true);

        Assert.IsType<ThrowingStore>(host.GetRequiredService<IProtectionKeyStore>());
    }

    [Fact]
    public void DataOpsStore_NeedsNoDialect_BecauseTheDataSourceDeclaresTheEngine()
    {
        var registry = new StubConnectionRegistry(this.path);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConnectionRegistry>(registry);
        services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName("/app")
            .PersistKeysToDataOpsStore();

        using var host = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(host.GetRequiredService<IProtectionKeyStore>());
    }

    [Fact]
    public void RelationalStoreRegisteredAlongside_MakesTheDialectRequiredForBoth()
    {
        // Registering two stores is a mistake: they share one settings instance, and the first
        // registered wins. The dialect requirement the relational store adds then applies to the
        // one that won, so the failure is loud rather than a silently wrong store.
        var registry = new StubConnectionRegistry(this.path);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConnectionRegistry>(registry);
        services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName("/app")
            .PersistKeysToDataOpsStore()
            .PersistKeysToRelationalStore(_ => new SqliteConnection("Data Source=:memory:"));

        using var host = services.BuildServiceProvider(validateScopes: true);

        var error = Record.Exception(() => host.GetRequiredService<IProtectionKeyStore>());

        Assert.NotNull(error);
        Assert.Contains("relational key store", error.Message, StringComparison.Ordinal);
    }

    private sealed class ThrowingStore : IProtectionKeyStore
    {
        public IReadOnlyList<ProtectionKeyRecord> GetAll() => throw new NotSupportedException();

        public void Add(string friendlyName, string? xml) => throw new NotSupportedException();

        public bool Delete(IReadOnlyList<int> orderedIds) => throw new NotSupportedException();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(this.path);
    }

    [Fact]
    public void Configuration_AppliesTheDiscriminatorAndTheStoreSettingsTogether()
    {
        var registry = new StubConnectionRegistry(this.path);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Application"] = "/app",
                ["DataProtection:Storage:Relational:TableName"] = "DataProtectionKeys",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConnectionRegistry>(registry);
        services.AddDataProtectionCore(configuration)
            .PersistKeysToDataOpsStore();

        using var host = services.BuildServiceProvider(validateScopes: true);

        Assert.Equal(
            "/app",
            host.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
        Assert.Equal(
            "DataProtectionKeys",
            host.GetRequiredService<IOptions<ProtectionKeyStoreOptions>>().Value.TableName);
    }

    [Fact]
    public void CustomSectionName_MovesTheDiscriminatorAndTheStoreSettingsTogether()
    {
        var registry = new StubConnectionRegistry(this.path);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Keys:Application"] = "/alt",
                ["Security:Keys:Storage:Relational:TableName"] = "infra_data_protection_keys",
                ["DataProtection:Storage:Relational:TableName"] = "wrong_table",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConnectionRegistry>(registry);
        services.AddDataProtectionCore(configuration, "Security:Keys")
            .PersistKeysToDataOpsStore();

        using var host = services.BuildServiceProvider(validateScopes: true);

        Assert.Equal(
            "/alt",
            host.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
        Assert.Equal(
            "infra_data_protection_keys",
            host.GetRequiredService<IOptions<ProtectionKeyStoreOptions>>().Value.TableName);
    }

    [Fact]
    public void CustomRoot_CarriesItsStorageSubsectionThroughTheDataOpsPath()
    {
        var registry = new StubConnectionRegistry(this.path);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CustomProtection:Application"] = "/custom",
                ["CustomProtection:Storage:Relational:TableName"] = "DataProtectionKeys",
                ["DataProtection:Storage:Relational:TableName"] = "should_not_be_used",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConnectionRegistry>(registry);
        services.AddDataProtectionCore(configuration, "CustomProtection")
            .PersistKeysToDataOpsStore()
            .AllowUnprotectedKeys();

        using var host = services.BuildServiceProvider(validateScopes: true);
        var store = host.GetRequiredService<IProtectionKeyStore>();

        // Reaching the seeded table at all proves the custom root's table name was used.
        store.Add("key-1", "<key />");

        Assert.Equal(
            "/custom",
            host.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
        Assert.Single(store.GetAll());
    }

    [Fact]
    public void Discriminator_ScopesPayloadsWhileTheDataSourceIsShared()
    {
        var registry = new StubConnectionRegistry(this.path);

        ServiceProvider HostFor(string application)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionRegistry>(registry);
            services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName(application)
                .PersistKeysToDataOpsStore(_ => { }, dataSource: null)
                .AllowUnprotectedKeys();

            return services.BuildServiceProvider(validateScopes: true);
        }

        string payload;

        using (var mine = HostFor("/app-one"))
        {
            payload = mine.GetRequiredService<IDataProtectionProvider>().CreateProtector("p").Protect("secret");
        }

        using var theirs = HostFor("/app-two");
        var protector = theirs.GetRequiredService<IDataProtectionProvider>().CreateProtector("p");

        Assert.ThrowsAny<Exception>(() => protector.Unprotect(payload));
    }
}
