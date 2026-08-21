using ApricotFramework.DataProtection.AspNetCore.Extensions;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;
using Microsoft.Extensions.Options;

namespace ApricotFramework.DataProtection.AspNetCore.Validation;

/// <summary>
/// Requires a dialect, for a store that selects its SQL by name.
/// </summary>
/// <remarks>
/// <para>Separate from <see cref="ProtectionKeyStoreOptionsValidator"/> because a store that learns
/// the engine from the connection it is given does not need the setting, and should not be failed
/// for leaving it unset.</para>
/// <para>Registering a relational store adds this for every store, since they share one settings
/// instance. Registering two stores is already a mistake — the first one registered wins — so the
/// message says which call wants the setting.</para>
/// </remarks>
internal sealed class ProtectionKeyDialectRequiredValidator : IValidateOptions<ProtectionKeyStoreOptions>
{
    /// <summary>
    /// The dialects a configured name may select.
    /// </summary>
    private readonly IReadOnlyDictionary<string, IProtectionKeySqlDialect> dialects;

    /// <summary>
    /// Creates a validator.
    /// </summary>
    /// <param name="additionalDialects">Any dialects registered in the container.</param>
    public ProtectionKeyDialectRequiredValidator(IEnumerable<IProtectionKeySqlDialect>? additionalDialects)
    {
        this.dialects = ProtectionKeySqlDialects.Compose(additionalDialects);
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ProtectionKeyStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Dialect))
        {
            // Composed rather than written out, so the nesting cannot drift from what is bound.
            var path = ProtectionKeyServiceCollectionExtensions.StorageSectionPath(
                ProtectionKeyServiceCollectionExtensions.ConfigurationSectionName,
                ProtectionKeyStorageBuilderExtensions.RelationalSectionName);

            return ValidateOptionsResult.Fail(
                $"No SQL dialect is configured. A relational key store selects its SQL by name, so set {path}:Dialect to one of: {string.Join(", ", this.dialects.Keys)}. A store that learns the engine from its connection does not need this, but registering a relational store alongside it still requires the setting.");
        }

        return ValidateOptionsResult.Success;
    }
}
