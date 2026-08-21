using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Dialects;

/// <summary>
/// SQLite.
/// </summary>
public sealed class SqliteProtectionKeyDialect : ProtectionKeyDialectBase
{
    /// <summary>
    /// The name this dialect is selected by.
    /// </summary>
    public const string DialectName = "Sqlite";

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
              {this.Quote(IdColumn)} INTEGER PRIMARY KEY AUTOINCREMENT,
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

    /// <inheritdoc />
    protected override bool SupportsSchema()
    {
        return false;
    }
}
