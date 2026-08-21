namespace ApricotFramework.DataProtection.Dialects;

/// <summary>
/// The dialects the store understands.
/// </summary>
public static class ProtectionKeySqlDialects
{
    /// <summary>
    /// The dialects available without any registration.
    /// </summary>
    /// <returns>MySQL, PostgreSQL, SQL Server and SQLite.</returns>
    /// <remarks>
    /// These are always present, so no registration mistake can leave an existing key ring
    /// unreadable.
    /// </remarks>
    public static IReadOnlyList<IProtectionKeySqlDialect> BuiltIn()
    {
        return
        [
            new MySqlProtectionKeyDialect(),
            new PostgreSqlProtectionKeyDialect(),
            new SqlServerProtectionKeyDialect(),
            new SqliteProtectionKeyDialect(),
        ];
    }

    /// <summary>
    /// Builds the lookup the store resolves a configured dialect name against.
    /// </summary>
    /// <param name="additional">Extra dialects to support, or null for none.</param>
    /// <returns>The dialects keyed by name, matched case-insensitively.</returns>
    /// <remarks>
    /// An extra sharing a name with a built-in replaces it, which is the only way to change a
    /// built-in's SQL. Among the extras, the last one wins.
    /// </remarks>
    /// <exception cref="ArgumentException">An extra has a blank name.</exception>
    public static IReadOnlyDictionary<string, IProtectionKeySqlDialect> Compose(IEnumerable<IProtectionKeySqlDialect>? additional)
    {
        var map = new Dictionary<string, IProtectionKeySqlDialect>(StringComparer.OrdinalIgnoreCase);

        foreach (var dialect in BuiltIn())
        {
            map[dialect.GetName()] = dialect;
        }

        foreach (var dialect in additional ?? [])
        {
            ArgumentNullException.ThrowIfNull(dialect, nameof(additional));

            var name = dialect.GetName();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A SQL dialect must have a name.", nameof(additional));
            }

            map[name] = dialect;
        }

        return map;
    }
}
