using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Dialects;

/// <summary>
/// The SQL one database engine needs for the key ring table.
/// </summary>
/// <remarks>
/// The statements differ only in how identifiers are quoted; the create-table script differs in
/// how an auto-incrementing key and a large text column are spelled.
/// </remarks>
public interface IProtectionKeySqlDialect
{
    /// <summary>
    /// Gets the name this dialect is selected by in configuration.
    /// </summary>
    /// <returns>The dialect name.</returns>
    string GetName();

    /// <summary>
    /// Builds the statement reading every row.
    /// </summary>
    /// <param name="options">The table to read.</param>
    /// <returns>The statement.</returns>
    string GetSelectAllSql(ProtectionKeyStoreOptions options);

    /// <summary>
    /// Builds the statement inserting one row, bound to <c>@FriendlyName</c> and <c>@Xml</c>.
    /// </summary>
    /// <param name="options">The table to write.</param>
    /// <returns>The statement.</returns>
    string GetInsertSql(ProtectionKeyStoreOptions options);

    /// <summary>
    /// Builds the statement deleting one row, bound to <c>@Id</c>.
    /// </summary>
    /// <param name="options">The table to delete from.</param>
    /// <returns>The statement.</returns>
    string GetDeleteSql(ProtectionKeyStoreOptions options);

    /// <summary>
    /// Builds the script creating the table.
    /// </summary>
    /// <param name="options">The table to create.</param>
    /// <returns>The script.</returns>
    /// <remarks>
    /// Nothing in this library runs it. It exists so a migration, a test fixture and the
    /// documentation all describe the same table the statements above read and write.
    /// </remarks>
    string GetCreateTableScript(ProtectionKeyStoreOptions options);
}
