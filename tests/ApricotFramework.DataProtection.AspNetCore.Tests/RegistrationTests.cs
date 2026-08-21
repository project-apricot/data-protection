using ApricotFramework.DataProtection.AspNetCore.Extensions;
using ApricotFramework.DataProtection.AspNetCore.Options;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.DataProtection.AspNetCore.Tests;

public class RegistrationTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
    }

    private static ServiceProvider Build(IServiceCollection services)
    {
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AddDataProtectionCore_AppliesTheConfiguredDiscriminator()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration(("DataProtection:Application", "/app")));

        using var provider = Build(services);

        Assert.Equal(
            "/app",
            provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }

    [Fact]
    public void AddDataProtectionCore_NoSection_LeavesTheFrameworkDefault()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration());

        using var provider = Build(services);

        Assert.Null(provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }

    [Fact]
    public void SetApplicationName_WithoutAnyConfiguration_AppliesTheDiscriminator()
    {
        // There is no code-configuring overload of our own: the framework already has one.
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration()).SetApplicationName("/coded");

        using var provider = Build(services);

        Assert.Equal(
            "/coded",
            provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }

    [Fact]
    public void AddDataProtectionCore_AlternateSectionName_IsBound()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(
            Configuration(("Security:Keys:Application", "/alt")),
            "Security:Keys");

        using var provider = Build(services);

        Assert.Equal(
            "/alt",
            provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }

    [Fact]
    public void PersistKeysToRelationalStore_BindsStoreOptionsFromTheSameSection()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration(
                ("DataProtection:Application", "/app"),
                ("DataProtection:Storage:Relational:TableName", "infra_data_protection_keys"),
                ("DataProtection:Storage:Relational:Dialect", "MySql")))
            .PersistKeysToRelationalStore(
                Configuration(
                    ("DataProtection:Storage:Relational:TableName", "infra_data_protection_keys"),
                    ("DataProtection:Storage:Relational:Dialect", "MySql")),
                "DataProtection",
                _ => throw new InvalidOperationException("The connection must not be built during registration."));

        using var provider = Build(services);
        var options = provider.GetRequiredService<IOptions<ProtectionKeyStoreOptions>>().Value;

        Assert.Equal("infra_data_protection_keys", options.TableName);
        Assert.Equal("MySql", options.Dialect);
    }

    [Fact]
    public void ProtectionKeyStoreOptions_DefaultTableName_IsSnakeCase()
    {
        Assert.Equal("DataProtectionKeys", new ProtectionKeyStoreOptions().TableName);
    }

    [Fact]
    public void PersistKeysToStore_HostRegisteredStore_Wins()
    {
        var store = new FakeProtectionKeyStore();

        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration())
            .PersistKeysToStore(store)
            .PersistKeysToRelationalStore(
                Configuration(("DataProtection:Storage:Relational:Dialect", "Sqlite")),
                "DataProtection",
                _ => throw new InvalidOperationException("The relational store must not be built."));

        using var provider = Build(services);

        Assert.Same(store, provider.GetRequiredService<IProtectionKeyStore>());
    }

    [Fact]
    public void PersistKeysToStore_Generic_RegistersTheStore()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration())
            .PersistKeysToStore<FakeProtectionKeyStore>();

        using var provider = Build(services);

        Assert.IsType<FakeProtectionKeyStore>(provider.GetRequiredService<IProtectionKeyStore>());
    }

    [Fact]
    public void PersistKeysToStore_InstallsTheRepositoryOnKeyManagementOptions()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration())
            .PersistKeysToStore(new FakeProtectionKeyStore())
            .AllowUnprotectedKeys();

        using var provider = Build(services);

        Assert.NotNull(provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
    }

    [Fact]
    public void PersistKeysToStore_CalledTwice_IsIdempotent()
    {
        var store = new FakeProtectionKeyStore();

        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration())
            .PersistKeysToStore(store)
            .PersistKeysToStore(store)
            .AllowUnprotectedKeys();

        using var provider = Build(services);

        Assert.Same(store, provider.GetRequiredService<IProtectionKeyStore>());
        Assert.NotNull(provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
    }

    [Fact]
    public void Builder_IsTheFrameworksOwn_SoOtherExtensionsStillCompose()
    {
        var services = new ServiceCollection();

        var builder = services.AddDataProtectionCore(Configuration())
            .PersistKeysToStore(new FakeProtectionKeyStore())
            .SetDefaultKeyLifetime(TimeSpan.FromDays(30))
            .DisableAutomaticKeyGeneration()
            .AllowUnprotectedKeys();

        using var provider = Build(services);
        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        Assert.NotNull(builder);
        Assert.Equal(TimeSpan.FromDays(30), options.NewKeyLifetime);
        Assert.False(options.AutoGenerateKeys);
        Assert.NotNull(options.XmlRepository);
    }

    [Fact]
    public void AddProtectionKeySqlDialect_MakesACustomDialectSelectable()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration())
            .AddProtectionKeySqlDialect(new CustomDialect())
            .PersistKeysToRelationalStore(
                Configuration(("DataProtection:Storage:Relational:Dialect", "Custom")),
                "DataProtection",
                _ => new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:"));

        using var provider = Build(services);

        // Resolving the store is what forces validation of the dialect name.
        Assert.NotNull(provider.GetRequiredService<IProtectionKeyStore>());
    }

    [Fact]
    public void MissingDialect_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration())
            .PersistKeysToRelationalStore(
                Configuration(),
                "DataProtection",
                _ => new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:"));

        using var provider = Build(services);

        var error = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IProtectionKeyStore>());
        Assert.Contains("No SQL dialect is configured", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownDialect_FailsValidationAndNamesWhatIsAvailable()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration())
            .PersistKeysToRelationalStore(
                Configuration(("DataProtection:Storage:Relational:Dialect", "Oracle")),
                "DataProtection",
                _ => new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:"));

        using var provider = Build(services);

        var error = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IProtectionKeyStore>());
        Assert.Contains("MySql", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnusableTableName_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration(
                ("DataProtection:Storage:Relational:Dialect", "Sqlite"),
                ("DataProtection:Storage:Relational:TableName", "keys; DROP TABLE users")))
            .PersistKeysToRelationalStore(_ => throw new InvalidOperationException("not built here"));

        using var provider = Build(services);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IProtectionKeyStore>());
    }

    [Fact]
    public void BlankSchemaName_PassesValidation_BecauseTheDialectTreatsItAsUnset()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration(
                ("DataProtection:Storage:Relational:Dialect", "Sqlite"),
                ("DataProtection:Storage:Relational:SchemaName", "")))
            .PersistKeysToRelationalStore(_ => throw new InvalidOperationException("not built here"))
            .AllowUnprotectedKeys();

        using var provider = Build(services);

        // An env var that is present but empty must not be a startup failure.
        Assert.Equal(string.Empty, provider.GetRequiredService<IOptions<ProtectionKeyStoreOptions>>().Value.SchemaName);
    }

    [Fact]
    public void NegativeCommandTimeout_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration())
            .PersistKeysToRelationalStore(
                _ => new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:"),
                options =>
                {
                    options.Dialect = "Sqlite";
                    options.CommandTimeout = TimeSpan.FromSeconds(-1);
                });

        using var provider = Build(services);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IProtectionKeyStore>());
    }

    [Fact]
    public void CustomSectionName_BindsTheStoreSettingsFromThatSameSection()
    {
        // The discriminator and the store settings share one section, so a custom section name has
        // to move both. Binding them in separate calls is what silently lost the table name.
        var configuration = Configuration(
            ("Security:Keys:Application", "/alt"),
            ("Security:Keys:Storage:Relational:Dialect", "MySql"),
            ("Security:Keys:Storage:Relational:TableName", "infra_data_protection_keys"),
            ("DataProtection:Storage:Relational:TableName", "wrong_table"));

        var services = new ServiceCollection();
        services.AddDataProtectionCore(configuration, "Security:Keys")
            .PersistKeysToRelationalStore(_ => throw new InvalidOperationException("not built here"));

        using var provider = Build(services);
        var store = provider.GetRequiredService<IOptions<ProtectionKeyStoreOptions>>().Value;

        Assert.Equal("/alt", provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
        Assert.Equal("MySql", store.Dialect);
        Assert.Equal("infra_data_protection_keys", store.TableName);
    }

    [Fact]
    public void DefaultSectionName_BindsBothTheDiscriminatorAndTheStoreSettings()
    {
        var configuration = Configuration(
            ("DataProtection:Application", "/app"),
            ("DataProtection:Storage:Relational:Dialect", "Sqlite"),
            ("DataProtection:Storage:Relational:TableName", "infra_data_protection_keys"));

        var services = new ServiceCollection();
        services.AddDataProtectionCore(configuration)
            .PersistKeysToRelationalStore(_ => throw new InvalidOperationException("not built here"));

        using var provider = Build(services);
        var store = provider.GetRequiredService<IOptions<ProtectionKeyStoreOptions>>().Value;

        Assert.Equal("Sqlite", store.Dialect);
        Assert.Equal("infra_data_protection_keys", store.TableName);
    }


    [Fact]
    public void SetApplicationName_AfterBinding_Wins()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration(("DataProtection:Application", "/from-config")))
            .SetApplicationName("/from-code")
            .PersistKeysToStore(new FakeProtectionKeyStore())
            .AllowUnprotectedKeys();

        using var provider = Build(services);

        Assert.Equal(
            "/from-code",
            provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }

    [Fact]
    public void SetApplicationName_WithNoConfiguredApplication_IsNotOverwrittenWithNull()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(Configuration())
            .SetApplicationName("/from-code")
            .PersistKeysToStore(new FakeProtectionKeyStore())
            .AllowUnprotectedKeys();

        using var provider = Build(services);

        Assert.Equal(
            "/from-code",
            provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }

    private sealed class CustomDialect : IProtectionKeySqlDialect
    {
        public string GetName() => "Custom";

        public string GetSelectAllSql(ProtectionKeyStoreOptions options) => "SELECT 1";

        public string GetInsertSql(ProtectionKeyStoreOptions options) => "SELECT 1";

        public string GetDeleteSql(ProtectionKeyStoreOptions options) => "SELECT 1";

        public string GetCreateTableScript(ProtectionKeyStoreOptions options) => "SELECT 1";
    }
}
