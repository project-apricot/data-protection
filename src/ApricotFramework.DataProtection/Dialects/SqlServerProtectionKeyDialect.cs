using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Dialects;

/// <summary>
/// Microsoft SQL Server.
/// </summary>
public sealed class SqlServerProtectionKeyDialect : ProtectionKeyDialectBase
{
    /// <summary>
    /// The name this dialect is selected by.
    /// </summary>
    public const string DialectName = "SqlServer";

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
              {this.Quote(IdColumn)} INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
              {this.Quote(FriendlyNameColumn)} NVARCHAR(MAX) NULL,
              {this.Quote(XmlColumn)} NVARCHAR(MAX) NULL
            );
            """;
    }

    /// <inheritdoc />
    protected override string Quote(string identifier)
    {
        return $"[{identifier}]";
    }
}
