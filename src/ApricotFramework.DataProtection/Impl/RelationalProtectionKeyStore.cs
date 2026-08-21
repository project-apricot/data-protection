using System.Data;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Impl;

/// <summary>
/// A key store over one table, using nothing but ADO.NET.
/// </summary>
/// <remarks>
/// Every call takes a connection, runs one statement and returns the connection. There is no
/// transaction: each statement stands alone, and the key manager never asks for two to be atomic.
/// </remarks>
public sealed class RelationalProtectionKeyStore : IProtectionKeyStore
{
    /// <summary>
    /// Supplies the connection each call runs on.
    /// </summary>
    private readonly Func<IProtectionKeyConnection> connectionFactory;

    /// <summary>
    /// The dialect whose SQL to run.
    /// </summary>
    private readonly IProtectionKeySqlDialect dialect;

    /// <summary>
    /// Where the key ring is stored.
    /// </summary>
    private readonly ProtectionKeyStoreOptions options;

    /// <summary>
    /// Creates a store.
    /// </summary>
    /// <param name="connectionFactory">Supplies a connection per call.</param>
    /// <param name="dialect">The dialect whose SQL to run.</param>
    /// <param name="options">Where the key ring is stored.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public RelationalProtectionKeyStore(
        Func<IProtectionKeyConnection> connectionFactory,
        IProtectionKeySqlDialect dialect,
        ProtectionKeyStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(options);

        if (options.CommandTimeout is { } timeout && (timeout < TimeSpan.Zero || timeout.TotalSeconds > int.MaxValue))
        {
            // A span outside this range cannot be expressed as the command timeout, and an
            // unchecked cast would turn it into an arbitrary one.
            throw new ArgumentOutOfRangeException(
                nameof(options),
                timeout,
                $"Command timeout must be a non-negative span of at most {int.MaxValue} seconds.");
        }

        this.connectionFactory = connectionFactory;
        this.dialect = dialect;
        this.options = options;
    }

    /// <inheritdoc />
    public IReadOnlyList<ProtectionKeyRecord> GetAll()
    {
        using var lease = this.connectionFactory();
        using var command = this.CreateCommand(lease, this.dialect.GetSelectAllSql(this.options));
        using var reader = command.ExecuteReader();

        // By name, not by position: a custom dialect may list the columns in any order, and two of
        // them are strings, so a positional read would swap them without failing.
        var id = reader.GetOrdinal(ProtectionKeyColumns.Id);
        var friendlyName = reader.GetOrdinal(ProtectionKeyColumns.FriendlyName);
        var xml = reader.GetOrdinal(ProtectionKeyColumns.Xml);

        var records = new List<ProtectionKeyRecord>();

        while (reader.Read())
        {
            records.Add(new ProtectionKeyRecord(
                reader.GetInt32(id),
                reader.IsDBNull(friendlyName) ? null : reader.GetString(friendlyName),
                reader.IsDBNull(xml) ? null : reader.GetString(xml)));
        }

        return records;
    }

    /// <inheritdoc />
    public void Add(string friendlyName, string? xml)
    {
        ArgumentNullException.ThrowIfNull(friendlyName);

        using var lease = this.connectionFactory();
        using var command = this.CreateCommand(lease, this.dialect.GetInsertSql(this.options));

        AddParameter(command, "@FriendlyName", friendlyName);
        AddParameter(command, "@Xml", xml);

        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public bool Delete(IReadOnlyList<int> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);

        if (orderedIds.Count == 0)
        {
            return true;
        }

        using var lease = this.connectionFactory();
        using var command = this.CreateCommand(lease, this.dialect.GetDeleteSql(this.options));

        var parameter = AddParameter(command, "@Id", value: null);

        foreach (var id in orderedIds)
        {
            parameter.Value = id;

            // A row that is already gone is not a failure, but a statement that removed nothing
            // when it should have is, so stop rather than continue past it.
            if (command.ExecuteNonQuery() < 1)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Adds one parameter to a command.
    /// </summary>
    /// <param name="command">The command to add to.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value, or null for a database null.</param>
    /// <returns>The parameter.</returns>
    private static IDbDataParameter AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);

        return parameter;
    }

    /// <summary>
    /// Creates a command on a leased connection.
    /// </summary>
    /// <param name="lease">The leased connection.</param>
    /// <param name="sql">The statement to run.</param>
    /// <returns>The command.</returns>
    private IDbCommand CreateCommand(IProtectionKeyConnection lease, string sql)
    {
        var command = lease.GetConnection().CreateCommand();
        command.CommandText = sql;

        if (this.options.CommandTimeout is { } timeout)
        {
            command.CommandTimeout = (int)timeout.TotalSeconds;
        }

        return command;
    }
}
