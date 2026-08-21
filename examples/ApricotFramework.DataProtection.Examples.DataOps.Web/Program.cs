using ApricotFramework.DataProtection;
using ApricotFramework.DataOps;
using ApricotFramework.DataOps.AspNetCore.Extensions;
using ApricotFramework.DataOps.AspNetCore.Options;
using ApricotFramework.DataProtection.AspNetCore.Extensions;
using ApricotFramework.DataProtection.DataOps.Extensions;
using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// The data source and its driver belong to the host; the library never picks one. Declared in
// code here, and pointed at the temp folder, so running the example writes nothing into the repo.
builder.Services.AddDataOperations(options =>
{
    options.DefaultDataSource = "Main";
    options.DataSources["Main"] = new DataSourceOptions
    {
        Provider = SqlProvider.Sqlite,
        ConnectionString = $"Data Source={Path.Combine(Path.GetTempPath(), "apricot-data-protection-dataops.db")}",
    };
});
builder.Services.AddDataSourceConnection(SqlProvider.Sqlite, connectionString => new SqliteConnection(connectionString));

// One call for the discriminator, one for storage. Keeping them apart is what leaves every other
// builder extension — key protection, lifetime, algorithms — available to the host.
builder.Services.AddDataProtectionCore(builder.Configuration)
    .PersistKeysToDataOpsStore()

    // Choosing storage drops the framework's default at-rest encryption, so an answer is required.
    // The example keeps nothing worth protecting; a real deployment chains a ProtectKeysWith call.
    .AllowUnprotectedKeys();

var app = builder.Build();

// The library issues no DDL, so the table has to exist. A real deployment does this in a
// migration; the example does it at startup so it can be run with no setup.
CreateKeyTable(app);

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

static void CreateKeyTable(IHost host)
{
    var dialect = new SqliteProtectionKeyDialect();
    var options = new ProtectionKeyStoreOptions { TableName = "DataProtectionKeys" };

    using var reference = host.Services.GetRequiredService<IConnectionRegistry>().CreateOrNull(dataSource: null)
        ?? throw new InvalidOperationException("No default data source is configured.");

    reference.Connection.Open();

    using var command = reference.Connection.CreateCommand();

    // Generated from the same dialect the store queries with, so the two cannot drift.
    command.CommandText = dialect.GetCreateTableScript(options)
        .Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS", StringComparison.Ordinal);
    command.ExecuteNonQuery();
}
