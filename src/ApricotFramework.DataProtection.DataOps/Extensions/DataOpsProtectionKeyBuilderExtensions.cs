using ApricotFramework.DataOps;
using ApricotFramework.DataProtection.AspNetCore.Extensions;
using ApricotFramework.DataProtection.DataOps.Impl;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Impl;
using ApricotFramework.DataProtection.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ApricotFramework.DataProtection.DataOps.Extensions;

/// <summary>
/// Persists keys to a data source declared for data operations.
/// </summary>
/// <remarks>
/// The connection and the engine both come from the named data source, so the dialect needs no
/// separate configuration. Only the contracts package is referenced, so no execution engine and
/// no database driver come with it.
/// </remarks>
public static class DataOpsProtectionKeyBuilderExtensions
{
    /// <summary>
    /// Persists keys to a data source, binding store settings from a named configuration section.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <param name="sectionName">The root section; the store binds its <c>Storage:Relational</c> child.</param>
    /// <param name="dataSource">The data source name, or null for the default.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// For a host that started data protection some other way. After
    /// <c>AddDataProtectionCore</c> the settings are already bound, so use the
    /// overload that takes only a data source and cannot disagree with it about the section.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sectionName"/> is blank.</exception>
    public static IDataProtectionBuilder PersistKeysToDataOpsStore(
        this IDataProtectionBuilder builder,
        IConfiguration configuration,
        string sectionName,
        string? dataSource)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        builder.Services.AddOptions<ProtectionKeyStoreOptions>()
            .Bind(configuration.GetSection(ProtectionKeyServiceCollectionExtensions.StorageSectionPath(sectionName, ProtectionKeyStorageBuilderExtensions.RelationalSectionName)));

        return builder.PersistKeysToDataOpsStore(dataSource);
    }

    /// <summary>
    /// Persists keys to a data source, configuring store settings in code.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <param name="configure">Configures the store settings.</param>
    /// <param name="dataSource">The data source name, or null for the default.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IDataProtectionBuilder PersistKeysToDataOpsStore(
        this IDataProtectionBuilder builder,
        Action<ProtectionKeyStoreOptions> configure,
        string? dataSource)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<ProtectionKeyStoreOptions>().Configure(configure);

        return builder.PersistKeysToDataOpsStore(dataSource);
    }

    /// <summary>
    /// Persists keys to a data source, taking store settings from the container.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <param name="dataSource">The data source name, or null for the default.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IDataProtectionBuilder PersistKeysToDataOpsStore(
        this IDataProtectionBuilder builder,
        string? dataSource = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddProtectionKeyStoreValidation();

        builder.Services.TryAddSingleton<IProtectionKeyStore>(services =>
        {
            var registry = services.GetRequiredService<IConnectionRegistry>();
            var options = services.GetRequiredService<IOptionsMonitor<ProtectionKeyStoreOptions>>().CurrentValue;
            var dialects = ProtectionKeySqlDialects.Compose(services.GetServices<IProtectionKeySqlDialect>());

            // Creating a reference does not open a connection, so this only reads which engine the
            // data source declares — and fails here rather than at the first key read if the name
            // is not configured at all.
            using var probe = registry.CreateOrNull(dataSource)
                ?? throw new InvalidOperationException(
                    $"Data source '{dataSource ?? registry.GetDefaultDataSource()}' is not configured, so data protection keys have nowhere to go.");

            // An explicitly configured dialect wins, so a host can override what the data source
            // declares without abandoning this package.
            var dialect = string.IsNullOrWhiteSpace(options.Dialect)
                ? ResolveDialect(probe.Provider, dialects)
                : dialects[options.Dialect];

            return new RelationalProtectionKeyStore(
                () => new DataOpsProtectionKeyConnection(
                    registry.CreateOrNull(dataSource)
                        ?? throw new InvalidOperationException(
                            $"Data source '{dataSource}' is no longer configured.")),
                dialect,
                options);
        });

        return builder.PersistKeysToStore();
    }

    /// <summary>
    /// Finds the dialect for the engine a data source declares.
    /// </summary>
    /// <param name="provider">The engine the data source speaks to.</param>
    /// <param name="dialects">The dialects available, keyed by name.</param>
    /// <returns>The dialect.</returns>
    /// <exception cref="InvalidOperationException">No dialect covers the engine.</exception>
    private static IProtectionKeySqlDialect ResolveDialect(
        SqlProvider provider,
        IReadOnlyDictionary<string, IProtectionKeySqlDialect> dialects)
    {
        var name = provider switch
        {
            SqlProvider.MySql => MySqlProtectionKeyDialect.DialectName,
            SqlProvider.PostgreSql => PostgreSqlProtectionKeyDialect.DialectName,
            SqlProvider.SqlServer => SqlServerProtectionKeyDialect.DialectName,
            SqlProvider.Sqlite => SqliteProtectionKeyDialect.DialectName,
            _ => provider.ToString(),
        };

        if (!dialects.TryGetValue(name, out var dialect))
        {
            throw new InvalidOperationException(
                $"No data protection key dialect is registered for provider '{provider}'. Registered dialects: {string.Join(", ", dialects.Keys)}.");
        }

        return dialect;
    }
}
