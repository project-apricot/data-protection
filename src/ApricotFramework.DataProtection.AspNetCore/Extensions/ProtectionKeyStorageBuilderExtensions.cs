using System.Data;
using ApricotFramework.DataProtection.AspNetCore.Impl;
using ApricotFramework.DataProtection.AspNetCore.Options;
using ApricotFramework.DataProtection.AspNetCore.Validation;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Impl;
using ApricotFramework.DataProtection.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ApricotFramework.DataProtection.AspNetCore.Extensions;

/// <summary>
/// Points the key ring at a store.
/// </summary>
/// <remarks>
/// Naming and mechanism follow the framework's own storage providers, so these compose with
/// <c>ProtectKeysWith*</c>, <c>SetDefaultKeyLifetime</c> and the rest in any order.
/// </remarks>
public static class ProtectionKeyStorageBuilderExtensions
{
    /// <summary>
    /// The subsection, under the storage section, holding the relational store's settings.
    /// </summary>
    public const string RelationalSectionName = "Relational";

    /// <summary>
    /// Persists keys to a relational table, binding the store settings from a named configuration
    /// section.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <param name="sectionName">The root section; the store binds its <c>Storage:Relational</c> child.</param>
    /// <param name="connectionFactory">Supplies a connection per call.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// For a host that started data protection some other way. After <c>AddDataProtectionCore</c>
    /// the settings are already bound, so prefer the overload that cannot disagree about the root.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sectionName"/> is blank.</exception>
    public static IDataProtectionBuilder PersistKeysToRelationalStore(
        this IDataProtectionBuilder builder,
        IConfiguration configuration,
        string sectionName,
        Func<IServiceProvider, IDbConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        builder.Services.AddOptions<ProtectionKeyStoreOptions>().Bind(configuration.GetSection(ProtectionKeyServiceCollectionExtensions.RelationalSectionPath(sectionName)));

        return builder.PersistKeysToLeasedRelationalStore(
            services => new OwnedProtectionKeyConnection(connectionFactory(services)));
    }

    /// <summary>
    /// Persists keys to a relational table, taking the store settings already in the container.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <param name="connectionFactory">Supplies a connection per call.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IDataProtectionBuilder PersistKeysToRelationalStore(this IDataProtectionBuilder builder, Func<IServiceProvider, IDbConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        return builder.PersistKeysToLeasedRelationalStore(
            services => new OwnedProtectionKeyConnection(connectionFactory(services)));
    }

    /// <summary>
    /// Persists keys to a relational table, configuring the store settings in code.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <param name="connectionFactory">Supplies a connection per call.</param>
    /// <param name="configure">Configures the store settings.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IDataProtectionBuilder PersistKeysToRelationalStore(
        this IDataProtectionBuilder builder,
        Func<IServiceProvider, IDbConnection> connectionFactory,
        Action<ProtectionKeyStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<ProtectionKeyStoreOptions>().Configure(configure);

        return builder.PersistKeysToLeasedRelationalStore(
            services => new OwnedProtectionKeyConnection(connectionFactory(services)));
    }

    /// <summary>
    /// Registers the relational store over a factory that leases a connection per call.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <param name="connectionFactory">Supplies a lease per call.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// Private: two public overloads separated only by a delegate's return type cannot be told
    /// apart at a call site. A host that borrows connections builds a
    /// <see cref="RelationalProtectionKeyStore"/> and registers the instance instead.
    /// </remarks>
    private static IDataProtectionBuilder PersistKeysToLeasedRelationalStore(
        this IDataProtectionBuilder builder,
        Func<IServiceProvider, IProtectionKeyConnection> connectionFactory)
    {
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ProtectionKeyStoreOptions>, ProtectionKeyDialectRequiredValidator>());

        builder.AddProtectionKeyStoreValidation();

        builder.Services.TryAddSingleton<IProtectionKeyStore>(services =>
        {
            var options = services.GetRequiredService<IOptionsMonitor<ProtectionKeyStoreOptions>>().CurrentValue;
            var dialects = ProtectionKeySqlDialects.Compose(services.GetServices<IProtectionKeySqlDialect>());

            // Validation has already rejected a missing or unknown name by this point.
            var dialect = dialects[options.Dialect!];

            return new RelationalProtectionKeyStore(() => connectionFactory(services), dialect, options);
        });

        return builder.PersistKeysToStore();
    }

    /// <summary>
    /// Adds the store settings validator, so a bad table name fails at startup.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IDataProtectionBuilder AddProtectionKeyStoreValidation(this IDataProtectionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ProtectionKeyStoreOptions>, ProtectionKeyStoreOptionsValidator>());
        builder.Services.AddOptions<ProtectionKeyStoreOptions>().ValidateOnStart();

        return builder;
    }

    /// <summary>
    /// Persists keys to the <see cref="IProtectionKeyStore"/> registered in the container.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IDataProtectionBuilder PersistKeysToStore(this IDataProtectionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
            new ConfigureOptions<KeyManagementOptions>(options =>
                options.XmlRepository = new ProtectionKeyXmlRepository(
                    services.GetRequiredService<IProtectionKeyStore>())));

        // Setting a repository is what drops the framework's default at-rest encryption, so this
        // is the point that owes the host an answer about it.
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<KeyManagementOptions>, KeyProtectionRequiredValidator>());
        builder.Services.AddOptions<KeyManagementOptions>().ValidateOnStart();

        return builder;
    }

    /// <summary>
    /// Persists keys to a store of the caller's own.
    /// </summary>
    /// <typeparam name="TStore">The store to use. Its dependencies are injected.</typeparam>
    /// <param name="builder">The builder to modify.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// Registered before the built-in relational store, so calling this wins over a later
    /// <c>PersistKeysToRelationalStore</c> rather than fighting it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IDataProtectionBuilder PersistKeysToStore<TStore>(this IDataProtectionBuilder builder)
        where TStore : class, IProtectionKeyStore
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IProtectionKeyStore, TStore>();

        return builder.PersistKeysToStore();
    }

    /// <summary>
    /// Persists keys to an already-constructed store.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <param name="store">The store to use.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IDataProtectionBuilder PersistKeysToStore(this IDataProtectionBuilder builder, IProtectionKeyStore store)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(store);

        builder.Services.TryAddSingleton(store);

        return builder.PersistKeysToStore();
    }

    /// <summary>
    /// Adds a SQL dialect the store can be configured to use, alongside the built-in ones.
    /// </summary>
    /// <typeparam name="TDialect">The dialect to add. Its dependencies are injected.</typeparam>
    /// <param name="builder">The builder to modify.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IDataProtectionBuilder AddProtectionKeySqlDialect<TDialect>(this IDataProtectionBuilder builder)
        where TDialect : class, IProtectionKeySqlDialect
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IProtectionKeySqlDialect, TDialect>());

        return builder;
    }

    /// <summary>
    /// Adds an already-constructed SQL dialect, alongside the built-in ones.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <param name="dialect">The dialect to add.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IDataProtectionBuilder AddProtectionKeySqlDialect(this IDataProtectionBuilder builder, IProtectionKeySqlDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dialect);

        builder.Services.AddSingleton(dialect);

        return builder;
    }
}
