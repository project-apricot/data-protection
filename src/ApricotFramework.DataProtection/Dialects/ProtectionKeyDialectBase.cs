using ApricotFramework.DataProtection.Options;
using ApricotFramework.DataProtection.Validation;

namespace ApricotFramework.DataProtection.Dialects;

/// <summary>
/// The statements every supported engine shares, given a way to quote an identifier.
/// </summary>
/// <remarks>
/// The column names are constants rather than settings. That keeps one table readable by both
/// this store and the official Entity Framework Core provider, and keeps the statements out of
/// reach of configuration.
/// </remarks>
public abstract class ProtectionKeyDialectBase : IProtectionKeySqlDialect
{
    /// <summary>
    /// The identity column.
    /// </summary>
    protected const string IdColumn = ProtectionKeyColumns.Id;

    /// <summary>
    /// The column holding the name the key manager gave an element.
    /// </summary>
    protected const string FriendlyNameColumn = ProtectionKeyColumns.FriendlyName;

    /// <summary>
    /// The column holding the serialized element.
    /// </summary>
    protected const string XmlColumn = ProtectionKeyColumns.Xml;

    /// <inheritdoc />
    public abstract string GetName();

    /// <inheritdoc />
    public string GetSelectAllSql(ProtectionKeyStoreOptions options)
    {
        return $"SELECT {this.Quote(IdColumn)}, {this.Quote(FriendlyNameColumn)}, {this.Quote(XmlColumn)} FROM {this.QualifiedTable(options)}";
    }

    /// <inheritdoc />
    public string GetInsertSql(ProtectionKeyStoreOptions options)
    {
        return $"INSERT INTO {this.QualifiedTable(options)} ({this.Quote(FriendlyNameColumn)}, {this.Quote(XmlColumn)}) VALUES (@FriendlyName, @Xml)";
    }

    /// <inheritdoc />
    public string GetDeleteSql(ProtectionKeyStoreOptions options)
    {
        return $"DELETE FROM {this.QualifiedTable(options)} WHERE {this.Quote(IdColumn)} = @Id";
    }

    /// <inheritdoc />
    public abstract string GetCreateTableScript(ProtectionKeyStoreOptions options);

    /// <summary>
    /// Quotes one identifier for this engine.
    /// </summary>
    /// <param name="identifier">The identifier, already validated.</param>
    /// <returns>The quoted identifier.</returns>
    protected abstract string Quote(string identifier);

    /// <summary>
    /// Reports whether this engine understands a schema-qualified name.
    /// </summary>
    /// <returns>True if a schema may be used.</returns>
    protected virtual bool SupportsSchema()
    {
        return true;
    }

    /// <summary>
    /// Builds the table name, schema-qualified where the engine allows it.
    /// </summary>
    /// <param name="options">The table to name.</param>
    /// <returns>The quoted, optionally qualified table name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">A name is unusable, or a schema was set for an engine without schemas.</exception>
    protected string QualifiedTable(ProtectionKeyStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var table = this.Quote(ProtectionKeyIdentifier.Validate(options.TableName, nameof(options)));

        if (string.IsNullOrEmpty(options.SchemaName))
        {
            return table;
        }

        if (!this.SupportsSchema())
        {
            throw new ArgumentException(
                $"The {this.GetName()} dialect has no schemas, so SchemaName must be left unset.",
                nameof(options));
        }

        return $"{this.Quote(ProtectionKeyIdentifier.Validate(options.SchemaName, nameof(options)))}.{table}";
    }
}
