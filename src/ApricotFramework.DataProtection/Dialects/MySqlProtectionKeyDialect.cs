using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Dialects;

/// <summary>
/// MySQL, and wire-compatible engines such as MariaDB.
/// </summary>
public sealed class MySqlProtectionKeyDialect : ProtectionKeyDialectBase
{
    /// <summary>
    /// The name this dialect is selected by.
    /// </summary>
    public const string DialectName = "MySql";

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
              {this.Quote(IdColumn)} INT NOT NULL AUTO_INCREMENT,
              {this.Quote(FriendlyNameColumn)} TEXT NULL,
              {this.Quote(XmlColumn)} LONGTEXT NULL,
              PRIMARY KEY ({this.Quote(IdColumn)})
            );
            """;
    }

    /// <inheritdoc />
    protected override string Quote(string identifier)
    {
        return $"`{identifier}`";
    }
}
