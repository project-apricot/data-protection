using ApricotFramework.DataProtection.AspNetCore.Extensions;
using ApricotFramework.DataProtection.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.DataProtection.AspNetCore.Tests;

/// <summary>
/// Whether the discriminator and the storage settings have to share one root section.
/// </summary>
public class SectionRootTests
{
    private static IConfiguration TwoRoots()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:Application"] = "/app",
                ["App:Storage:Relational:Dialect"] = "Sqlite",
                ["App:Storage:Relational:TableName"] = "from_app",
                ["Infra:Storage:Relational:TableName"] = "from_infra",
            })
            .Build();
    }

    private static ProtectionKeyStoreOptions Store(ServiceProvider provider)
    {
        return provider.GetRequiredService<IOptions<ProtectionKeyStoreOptions>>().Value;
    }

    [Fact]
    public void StorageCanComeFromADifferentRoot_ButTheTwoBindingsMerge()
    {
        var configuration = TwoRoots();

        var services = new ServiceCollection();
        services.AddDataProtectionCore(configuration, "App")
            .PersistKeysToRelationalStore(configuration, "Infra", _ => throw new InvalidOperationException())
            .AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = Store(provider);

        Assert.Equal("/app", provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);

        // The later binding wins for what it sets...
        Assert.Equal("from_infra", store.TableName);

        // ...but a key it does not set keeps the value the first root supplied.
        Assert.Equal("Sqlite", store.Dialect);
    }

    [Fact]
    public void StorageAloneFromItsOwnRoot_ReadsOnlyThatRoot()
    {
        var configuration = TwoRoots();

        // The framework starts data protection, so only the storage call reads configuration and
        // nothing merges in from another root — including the dialect, which Infra must supply.
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("/app")
            .PersistKeysToRelationalStore(configuration, "Infra", _ => throw new InvalidOperationException())
            .AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var error = Assert.Throws<OptionsValidationException>(() => Store(provider));
        Assert.Contains("Storage:Relational:Dialect", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomRoot_CarriesItsStorageSubsectionWithIt()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CustomProtection:Application"] = "/custom",
                ["CustomProtection:Storage:Relational:Dialect"] = "Sqlite",
                ["CustomProtection:Storage:Relational:TableName"] = "custom_keys",
                // The default root is populated too, and must be ignored entirely.
                ["DataProtection:Storage:Relational:TableName"] = "should_not_be_used",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDataProtectionCore(configuration, "CustomProtection")
            .PersistKeysToRelationalStore(_ => throw new InvalidOperationException("not built here"))
            .AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = Store(provider);

        Assert.Equal("/custom", provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
        Assert.Equal("Sqlite", store.Dialect);
        Assert.Equal("custom_keys", store.TableName);
    }

    [Fact]
    public void DifferentRootsForApplicationAndStorage_WorkWhenOnlyOneCarriesStorage()
    {
        // The merge is only visible when both roots populate Storage. Keep the discriminator's
        // root free of it and the two are genuinely independent.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:Application"] = "/app",
                ["Infra:Storage:Relational:Dialect"] = "Sqlite",
                ["Infra:Storage:Relational:TableName"] = "infra_keys",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDataProtectionCore(configuration, "App")
            .PersistKeysToRelationalStore(configuration, "Infra", _ => throw new InvalidOperationException())
            .AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = Store(provider);

        Assert.Equal("/app", provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
        Assert.Equal("Sqlite", store.Dialect);
        Assert.Equal("infra_keys", store.TableName);
    }

    [Fact]
    public void DialectRequiredMessage_NamesTheNestedPath()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(new ConfigurationBuilder().Build())
            .PersistKeysToRelationalStore(_ => throw new InvalidOperationException())
            .AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var error = Assert.Throws<OptionsValidationException>(() => Store(provider));
        Assert.Contains("DataProtection:Storage:Relational:Dialect", error.Message, StringComparison.Ordinal);
    }
}
