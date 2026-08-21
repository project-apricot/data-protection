using ApricotFramework.DataProtection.AspNetCore.Options;
using ApricotFramework.DataProtection.Options;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.DataProtection.AspNetCore.Extensions;

/// <summary>
/// Starts data protection with its settings bound from configuration.
/// </summary>
/// <remarks>
/// Named <c>Core</c> in the sense <c>AddMvcCore</c> uses: the foundation a host composes storage
/// and key protection onto, leaving the unadorned name free for a composition root of its own.
/// </remarks>
public static class ProtectionKeyServiceCollectionExtensions
{
    /// <summary>
    /// The root configuration section bound when no other is named.
    /// </summary>
    public const string ConfigurationSectionName = "DataProtection";

    /// <summary>
    /// The child section grouping the storage backends, one subsection each.
    /// </summary>
    /// <remarks>
    /// Which backend is used is chosen by the <c>PersistKeysTo</c> call, not by which subsections
    /// are present. A backend needs a connection or a credential that configuration cannot carry,
    /// so the call has to exist either way, and the section only supplies its settings.
    /// </remarks>
    public const string StorageSectionName = "Storage";

    /// <summary>
    /// The child section grouping the key protection mechanisms, one subsection each.
    /// </summary>
    /// <remarks>
    /// Reserved. Nothing binds it yet; a <c>ProtectKeysWith</c> call will own its own subsection,
    /// exactly as a storage backend owns one under <see cref="StorageSectionName"/>.
    /// </remarks>
    public const string ProtectionSectionName = "Protection";

    /// <summary>
    /// Adds data protection, binding its settings from the
    /// <see cref="ConfigurationSectionName"/> configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The builder, so a caller can choose storage and key protection.</returns>
    /// <remarks>
    /// Binds both the discriminator and the key store settings from that one section, then adds
    /// no storage. Follow it with a <c>PersistKeysTo</c> call, or the key ring stays wherever the
    /// framework puts it by default.
    /// </remarks>
    public static IDataProtectionBuilder AddDataProtectionCore(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddDataProtectionCore(configuration, ConfigurationSectionName);
    }

    /// <summary>
    /// Adds data protection, binding its settings from a named configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <param name="sectionName">The section to bind.</param>
    /// <returns>The builder, so a caller can choose storage and key protection.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sectionName"/> is blank.</exception>
    public static IDataProtectionBuilder AddDataProtectionCore(this IServiceCollection services, IConfiguration configuration, string sectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        // Bound together, from the one root, because binding them in separate calls is how a
        // custom root ends up applying to one and not the other.
        services.AddOptions<DataProtectionApplicationOptions>().Bind(configuration.GetSection(sectionName));
        services.AddOptions<ProtectionKeyStoreOptions>().Bind(configuration.GetSection(RelationalSectionPath(sectionName)));

        return ApplyConfiguredApplicationName(services.AddDataProtection());
    }

    /// <summary>
    /// Builds the path of a storage backend's settings under a root section.
    /// </summary>
    /// <param name="sectionName">The root section.</param>
    /// <param name="backendSectionName">The backend's own subsection name.</param>
    /// <returns>The path, such as <c>DataProtection:Storage:Relational</c>.</returns>
    /// <remarks>
    /// A storage package uses this so its settings land beside every other backend's, under the
    /// same root the discriminator was bound from.
    /// </remarks>
    /// <exception cref="ArgumentException">Either name is blank.</exception>
    public static string StorageSectionPath(string sectionName, string backendSectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(backendSectionName);

        return ConfigurationPath.Combine(sectionName, StorageSectionName, backendSectionName);
    }

    /// <summary>
    /// Builds the path of the relational store's settings under a root section.
    /// </summary>
    /// <param name="sectionName">The root section.</param>
    /// <returns>The path, such as <c>DataProtection:Storage:Relational</c>.</returns>
    internal static string RelationalSectionPath(string sectionName)
    {
        return StorageSectionPath(sectionName, ProtectionKeyStorageBuilderExtensions.RelationalSectionName);
    }

    /// <summary>
    /// Copies the bound application setting onto the framework's discriminator.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// A host setting the discriminator itself uses the framework's own <c>SetApplicationName</c>,
    /// which composes with this and wins.
    /// </remarks>
    private static IDataProtectionBuilder ApplyConfiguredApplicationName(IDataProtectionBuilder builder)
    {
        builder.Services
            .AddOptions<DataProtectionOptions>()
            .Configure<IOptionsMonitor<DataProtectionApplicationOptions>>((options, application) =>
            {
                options.ApplicationDiscriminator = application.CurrentValue.Application;
            });

        return builder;
    }
}
