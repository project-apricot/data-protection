using System.Globalization;

namespace ApricotFramework.DataProtection.Validation;

/// <summary>
/// Checks that a table or schema name is safe to place in SQL text.
/// </summary>
/// <remarks>
/// Identifiers cannot be parameterized, so a configured name is concatenated into the statement.
/// Allowing only letters, digits and underscore is what keeps that from being an injection point.
/// </remarks>
public static class ProtectionKeyIdentifier
{
    /// <summary>
    /// The longest identifier accepted, which is the smallest limit among the supported engines.
    /// </summary>
    public const int MaximumLength = 63;

    /// <summary>
    /// Reports whether a name may be used as an identifier.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <returns>True if the name is usable.</returns>
    public static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaximumLength)
        {
            return false;
        }

        if (name[0] != '_' && !char.IsAsciiLetter(name[0]))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (character != '_' && !char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a name, or throws if it cannot be used as an identifier.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <param name="paramName">The parameter the name arrived through.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentException">The name is not a usable identifier.</exception>
    public static string Validate(string? name, string paramName)
    {
        if (!IsValid(name))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "'{0}' is not a usable SQL identifier. Use up to {1} characters of letters, digits and underscore, starting with a letter or underscore.",
                    name,
                    MaximumLength),
                paramName);
        }

        return name!;
    }
}
