using ApricotFramework.DataProtection.AspNetCore.Impl;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApricotFramework.DataProtection.AspNetCore.Extensions;

/// <summary>
/// Answers for encryption of the keys at rest.
/// </summary>
/// <remarks>
/// A mechanism ships in the package that implements it, each owning one subsection of
/// <c>Protection</c>, exactly as a storage backend owns one under <c>Storage</c>.
/// </remarks>
public static class KeyProtectionBuilderExtensions
{
    /// <summary>
    /// Records that keys may be written without an encryptor.
    /// </summary>
    /// <param name="builder">The builder to modify.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// Configures nothing; it answers for the store registered without an encryptor, which
    /// otherwise fails at startup. Use it where the storage encrypts on your behalf, or in
    /// development.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IDataProtectionBuilder AllowUnprotectedKeys(this IDataProtectionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<UnprotectedKeysAllowed>();

        return builder;
    }
}
