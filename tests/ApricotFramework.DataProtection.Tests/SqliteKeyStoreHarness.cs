using System.Data;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Impl;
using ApricotFramework.DataProtection.Options;
using Microsoft.Data.Sqlite;

namespace ApricotFramework.DataProtection.Tests;

/// <summary>
/// A throwaway SQLite database with the key ring table already created.
/// </summary>
/// <remarks>
/// A file rather than <c>:memory:</c>, because the store opens a fresh connection per call and an
/// in-memory database would give each one an empty database of its own. The table is created from
/// the dialect's own script, so a column name the store gets wrong cannot be hidden by a fixture
/// that agrees with the mistake.
/// </remarks>
internal sealed class SqliteKeyStoreHarness : IDisposable
{
    private readonly string path;

    public SqliteKeyStoreHarness(ProtectionKeyStoreOptions? options = null)
    {
        this.path = Path.Combine(Path.GetTempPath(), $"apricot-dp-{Guid.NewGuid():N}.db");
        this.Options = options ?? new ProtectionKeyStoreOptions();
        this.Dialect = new SqliteProtectionKeyDialect();

        using var connection = this.Open();
        using var command = connection.CreateCommand();
        command.CommandText = this.Dialect.GetCreateTableScript(this.Options);
        command.ExecuteNonQuery();
    }

    public ProtectionKeyStoreOptions Options { get; }

    public IProtectionKeySqlDialect Dialect { get; }

    public RelationalProtectionKeyStore CreateStore()
    {
        return new RelationalProtectionKeyStore(
            () => new OwnedProtectionKeyConnection(this.Open()),
            this.Dialect,
            this.Options);
    }

    public IReadOnlyList<(int Id, string? FriendlyName, string? Xml)> ReadRawRows()
    {
        using var connection = this.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"Id\", \"FriendlyName\", \"Xml\" FROM \"{this.Options.TableName}\" ORDER BY \"Id\"";

        using var reader = command.ExecuteReader();
        var rows = new List<(int, string?, string?)>();

        while (reader.Read())
        {
            rows.Add((
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    public void InsertRaw(string? friendlyName, string? xml)
    {
        using var connection = this.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO \"{this.Options.TableName}\" (\"FriendlyName\", \"Xml\") VALUES ($name, $xml)";
        command.Parameters.AddWithValue("$name", friendlyName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$xml", xml ?? (object)DBNull.Value);
        command.ExecuteNonQuery();
    }

    public SqliteConnection OpenConnection()
    {
        return this.Open();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={this.path}");
        connection.Open();

        return connection;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(this.path);
    }
}
