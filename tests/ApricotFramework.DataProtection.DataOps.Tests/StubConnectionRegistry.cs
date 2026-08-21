using System.Data;
using ApricotFramework.DataOps;
using Microsoft.Data.Sqlite;

namespace ApricotFramework.DataProtection.DataOps.Tests;

/// <summary>
/// A registry handing out SQLite connections, standing in for a configured data source.
/// </summary>
internal sealed class StubConnectionRegistry : IConnectionRegistry
{
    private readonly string path;

    private readonly SqlProvider provider;

    private readonly string? knownDataSource;

    public StubConnectionRegistry(string path, SqlProvider provider = SqlProvider.Sqlite, string? knownDataSource = null)
    {
        this.path = path;
        this.provider = provider;
        this.knownDataSource = knownDataSource;
    }

    public int ReferencesCreated { get; private set; }

    public int ReferencesDisposed { get; private set; }

    public string GetDefaultDataSource()
    {
        return this.knownDataSource ?? "Main";
    }

    public IConnectionReference? CreateOrNull(string? dataSource)
    {
        if (dataSource is not null && dataSource != this.GetDefaultDataSource())
        {
            return null;
        }

        this.ReferencesCreated++;

        return new StubConnectionReference(
            new SqliteConnection($"Data Source={this.path}"),
            this.provider,
            () => this.ReferencesDisposed++);
    }

    private sealed class StubConnectionReference(IDbConnection connection, SqlProvider provider, Action onDispose)
        : IConnectionReference
    {
        public IDbConnection Connection { get; } = connection;

        public SqlProvider Provider { get; } = provider;

        public void Dispose()
        {
            onDispose();
            this.Connection.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            this.Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
