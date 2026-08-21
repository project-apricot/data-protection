using ApricotFramework.DataProtection;
using ApricotFramework.DataProtection.AspNetCore.Extensions;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Pointed at the temp folder so running the example writes nothing into the repo.
var connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), "apricot-data-protection-relational.db")}";

// No data access library involved: the host owns the driver and hands over a connection. This is
// the whole integration — the key store needs nothing else.
builder.Services.AddDataProtectionCore(builder.Configuration)
    .PersistKeysToRelationalStore(_ => new SqliteConnection(connectionString))

    // Choosing storage drops the framework's default at-rest encryption, so an answer is required.
    // The example keeps nothing worth protecting; a real deployment chains a ProtectKeysWith call.
    .AllowUnprotectedKeys();

var app = builder.Build();

// The library issues no DDL, so the table has to exist. A real deployment does this in a
// migration; the example does it at startup so it can be run with no setup.
CreateKeyTable(connectionString, app.Services);

app.MapGet("/api/roundtrip", (IDataProtectionProvider protection) =>
{
    var protector = protection.CreateProtector("example-purpose");
    var payload = protector.Protect("Hello, World!");

    return Results.Ok(new
    {
        Protected = payload,
        Unprotected = protector.Unprotect(payload),
    });
});

app.MapGet("/api/keys", (IProtectionKeyStore store) =>
    Results.Ok(store.GetAll().Select(record => new { record.Id, record.FriendlyName })));

app.Run();

static void CreateKeyTable(string connectionString, IServiceProvider services)
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProtectionKeyStoreOptions>>().Value;

    using var connection = new SqliteConnection(connectionString);
    connection.Open();

    using var command = connection.CreateCommand();

    // Generated from the same dialect the store queries with, so the two cannot drift.
    command.CommandText = new SqliteProtectionKeyDialect()
        .GetCreateTableScript(options)
        .Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS", StringComparison.Ordinal);
    command.ExecuteNonQuery();
}
