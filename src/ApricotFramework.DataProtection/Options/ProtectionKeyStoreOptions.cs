namespace ApricotFramework.DataProtection.Options;

/// <summary>
/// Where the key ring is stored and how it is reached.
/// </summary>
public sealed class ProtectionKeyStoreOptions
{
    /// <summary>
    /// The table used when none is configured.
    /// </summary>
    public const string DefaultTableName = "DataProtectionKeys";

    /// <summary>
    /// The table holding the key ring.
    /// </summary>
    /// <remarks>
    /// Only the table is configurable; the columns are fixed, and match the entity the official
    /// Entity Framework Core provider uses, so one table can serve either.
    /// </remarks>
    public string TableName { get; set; } = DefaultTableName;

    /// <summary>
    /// The schema the table lives in, or null for the connection's default.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// The dialect whose SQL to use, named as in <see cref="Dialects.ProtectionKeySqlDialects"/>.
    /// </summary>
    /// <remarks>
    /// Required, and deliberately without a default. Guessing an engine produces SQL that reaches
    /// the server and fails there; demanding it produces one clear error at startup.
    /// </remarks>
    public string? Dialect { get; set; }

    /// <summary>
    /// How long a statement may run, or null for the driver's own default.
    /// </summary>
    public TimeSpan? CommandTimeout { get; set; }
}
