using System.Data;

namespace ApricotFramework.DataProtection;

/// <summary>
/// A borrowed connection the store returns when it is done with it.
/// </summary>
/// <remarks>
/// The store disposes this, never the connection itself. That is what lets a host hand over a
/// connection it owns through some other lifetime — a pool, or a registry handing out a
/// reference — without the store closing something it did not open.
/// </remarks>
public interface IProtectionKeyConnection : IDisposable
{
    /// <summary>
    /// Gets the connection, opening it if it is closed.
    /// </summary>
    /// <returns>An open connection.</returns>
    IDbConnection GetConnection();
}
