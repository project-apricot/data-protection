using System.Data;

namespace ApricotFramework.DataProtection.Impl;

/// <summary>
/// A lease over a connection it created, which it opens on demand and disposes when returned.
/// </summary>
/// <remarks>
/// Use this when the host hands over a factory that builds a fresh connection each time. A host
/// that borrows a connection from somewhere with its own lifetime should implement
/// <see cref="IProtectionKeyConnection"/> instead, so disposing the lease releases whatever that
/// lifetime actually is.
/// </remarks>
public sealed class OwnedProtectionKeyConnection : IProtectionKeyConnection
{
    /// <summary>
    /// The connection this lease owns.
    /// </summary>
    private readonly IDbConnection connection;

    /// <summary>
    /// Creates a lease that owns a connection.
    /// </summary>
    /// <param name="connection">The connection, open or closed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is null.</exception>
    public OwnedProtectionKeyConnection(IDbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.connection = connection;
    }

    /// <inheritdoc />
    public IDbConnection GetConnection()
    {
        if (this.connection.State != ConnectionState.Open)
        {
            this.connection.Open();
        }

        return this.connection;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.connection.Dispose();
    }
}
