using ApricotFramework.DataProtection.AspNetCore.Impl;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;

namespace ApricotFramework.DataProtection.AspNetCore.Validation;

/// <summary>
/// Fails startup when keys would be written unencrypted and nothing has said that is intended.
/// </summary>
/// <remarks>
/// Checks the outcome rather than the configuration, so it also catches a
/// <c>ProtectKeysWith</c> call that did not take effect.
/// </remarks>
internal sealed class KeyProtectionRequiredValidator : IValidateOptions<KeyManagementOptions>
{
    /// <summary>
    /// Whether the host has accepted unencrypted keys.
    /// </summary>
    private readonly bool allowed;

    /// <summary>
    /// Creates a validator.
    /// </summary>
    /// <param name="acknowledgements">Present when the host called <c>AllowUnprotectedKeys</c>.</param>
    public KeyProtectionRequiredValidator(IEnumerable<UnprotectedKeysAllowed> acknowledgements)
    {
        ArgumentNullException.ThrowIfNull(acknowledgements);

        this.allowed = acknowledgements.Any();
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, KeyManagementOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (this.allowed || options.XmlEncryptor is not null)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            "Data protection keys would be written without encryption. Choosing an explicit key storage location makes the framework drop its default at-rest encryption, so add a ProtectKeysWith call — or call AllowUnprotectedKeys() to record that this is intended, which it is when the storage encrypts on your behalf.");
    }
}
