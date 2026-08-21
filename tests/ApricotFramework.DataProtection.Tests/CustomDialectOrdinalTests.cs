using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Impl;
using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Tests;

/// <summary>
/// A dialect is an extension point, so the store must not depend on the order a custom one happens
/// to list the columns in. Reading by position would silently swap two same-typed columns.
/// </summary>
public class CustomDialectOrdinalTests
{
    [Fact]
    public void CustomDialect_ListingColumnsInAnotherOrder_StillReadsTheRightValues()
    {
        using var harness = new SqliteKeyStoreHarness();
        harness.InsertRaw("the-name", "<key id=\"a\" />");

        var store = new RelationalProtectionKeyStore(
            () => new OwnedProtectionKeyConnection(harness.OpenConnection()),
            new ReorderedDialect(),
            harness.Options);

        var record = Assert.Single(store.GetAll());

        Assert.Equal(1, record.Id);
        Assert.Equal("the-name", record.FriendlyName);
        Assert.Equal("<key id=\"a\" />", record.Xml);
    }

    [Fact]
    public void CustomDialect_OmittingAColumn_FailsNamingIt()
    {
        using var harness = new SqliteKeyStoreHarness();
        harness.InsertRaw("the-name", "<key />");

        var store = new RelationalProtectionKeyStore(
            () => new OwnedProtectionKeyConnection(harness.OpenConnection()),
            new IncompleteDialect(),
            harness.Options);

        var error = Assert.ThrowsAny<Exception>(() => store.GetAll());

        Assert.Contains("Xml", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same columns as the built-in SQLite dialect, listed in a different order.
    /// </summary>
    private sealed class ReorderedDialect : IProtectionKeySqlDialect
    {
        public string GetName() => "Reordered";

        public string GetSelectAllSql(ProtectionKeyStoreOptions options) =>
            $"SELECT \"Xml\", \"Id\", \"FriendlyName\" FROM \"{options.TableName}\"";

        public string GetInsertSql(ProtectionKeyStoreOptions options) =>
            $"INSERT INTO \"{options.TableName}\" (\"FriendlyName\", \"Xml\") VALUES (@FriendlyName, @Xml)";

        public string GetDeleteSql(ProtectionKeyStoreOptions options) =>
            $"DELETE FROM \"{options.TableName}\" WHERE \"Id\" = @Id";

        public string GetCreateTableScript(ProtectionKeyStoreOptions options) => string.Empty;
    }

    /// <summary>
    /// A dialect that forgets a column the store needs.
    /// </summary>
    private sealed class IncompleteDialect : IProtectionKeySqlDialect
    {
        public string GetName() => "Incomplete";

        public string GetSelectAllSql(ProtectionKeyStoreOptions options) =>
            $"SELECT \"Id\", \"FriendlyName\" FROM \"{options.TableName}\"";

        public string GetInsertSql(ProtectionKeyStoreOptions options) => string.Empty;

        public string GetDeleteSql(ProtectionKeyStoreOptions options) => string.Empty;

        public string GetCreateTableScript(ProtectionKeyStoreOptions options) => string.Empty;
    }
}
