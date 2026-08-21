using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;
using ApricotFramework.DataProtection.Validation;
using Microsoft.Extensions.Options;

namespace ApricotFramework.DataProtection.AspNetCore.Validation;

/// <summary>
/// Rejects store settings that cannot work, at startup rather than at the first key read.
/// </summary>
/// <remarks>
/// A bad table name or an unknown dialect otherwise surfaces as a SQL error the first time the
/// key ring is touched, which can be long after deployment.
/// </remarks>
internal sealed class ProtectionKeyStoreOptionsValidator : IValidateOptions<ProtectionKeyStoreOptions>
{
    /// <summary>
    /// The dialects a configured name may select.
    /// </summary>
    private readonly IReadOnlyDictionary<string, IProtectionKeySqlDialect> dialects;

    /// <summary>
    /// Creates a validator.
    /// </summary>
    /// <param name="additionalDialects">
    /// Any dialects registered in the container, composed with the built-in ones exactly as the
    /// store composes them.
    /// </param>
    public ProtectionKeyStoreOptionsValidator(IEnumerable<IProtectionKeySqlDialect>? additionalDialects)
    {
        this.dialects = ProtectionKeySqlDialects.Compose(additionalDialects);
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ProtectionKeyStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Dialect is not null && !this.dialects.ContainsKey(options.Dialect))
        {
            return ValidateOptionsResult.Fail(
                $"SQL dialect '{options.Dialect}' is not registered. Registered dialects: {string.Join(", ", this.dialects.Keys)}.");
        }

        if (!ProtectionKeyIdentifier.IsValid(options.TableName))
        {
            return ValidateOptionsResult.Fail(
                $"Table name '{options.TableName}' is not a usable SQL identifier. Use up to {ProtectionKeyIdentifier.MaximumLength} characters of letters, digits and underscore, starting with a letter or underscore.");
        }

        // Blank means unset, which is how the dialect reads it too. A templated environment
        // variable that is present but empty must not be a startup failure.
        if (!string.IsNullOrEmpty(options.SchemaName) && !ProtectionKeyIdentifier.IsValid(options.SchemaName))
        {
            return ValidateOptionsResult.Fail(
                $"Schema name '{options.SchemaName}' is not a usable SQL identifier.");
        }

        if (options.CommandTimeout is { } timeout && (timeout < TimeSpan.Zero || timeout.TotalSeconds > int.MaxValue))
        {
            return ValidateOptionsResult.Fail(
                $"Command timeout '{timeout}' must be a non-negative span of at most {int.MaxValue} seconds.");
        }

        return ValidateOptionsResult.Success;
    }
}
