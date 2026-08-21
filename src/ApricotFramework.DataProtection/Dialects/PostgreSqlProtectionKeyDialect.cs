using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Dialects;

/// <summary>
/// PostgreSQL.
/// </summary>
/// <remarks>
/// Identifiers are quoted, so the fixed column names keep their casing. An unquoted
/// <c>FriendlyName</c> would fold to lower case and not match.
/// </remarks>
public sealed class PostgreSqlProtectionKeyDialect : ProtectionKeyDialectBase
{
    /// <summary>
    /// The name this dialect is selected by.
    /// </summary>
    public const string DialectName = "PostgreSql";

    /// <inheritdoc />
    public override string GetName()
    {
        return DialectName;
    }

    /// <inheritdoc />
    public override string GetCreateTableScript(ProtectionKeyStoreOptions options)
    {
        return $"""
            CREATE TABLE {this.QualifiedTable(options)} (
              {this.Quote(IdColumn)} SERIAL PRIMARY KEY,
              {this.Quote(FriendlyNameColumn)} TEXT NULL,
              {this.Quote(XmlColumn)} TEXT NULL
            );
            """;
    }

    /// <inheritdoc />
    protected override string Quote(string identifier)
    {
        return $"\"{identifier}\"";
    }
}
