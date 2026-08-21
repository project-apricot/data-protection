using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Tests;

public class RelationalProtectionKeyStoreTests
{
    [Fact]
    public void GetAll_EmptyTable_ReturnsEmpty()
    {
        using var harness = new SqliteKeyStoreHarness();

        Assert.Empty(harness.CreateStore().GetAll());
    }

    [Fact]
    public void Add_WritesFriendlyNameAndXmlAndLetsTheServerAssignTheId()
    {
        using var harness = new SqliteKeyStoreHarness();

        harness.CreateStore().Add("key-1", "<key id=\"a\" />");

        var row = Assert.Single(harness.ReadRawRows());
        Assert.Equal(1, row.Id);
        Assert.Equal("key-1", row.FriendlyName);
        Assert.Equal("<key id=\"a\" />", row.Xml);
    }

    [Fact]
    public void Add_NullXml_StoresDatabaseNull()
    {
        using var harness = new SqliteKeyStoreHarness();

        harness.CreateStore().Add("key-1", xml: null);

        Assert.Null(Assert.Single(harness.ReadRawRows()).Xml);
    }

    [Fact]
    public void GetAll_ReturnsEveryRowIncludingOnesWithNoXml()
    {
        using var harness = new SqliteKeyStoreHarness();
        harness.InsertRaw("a", "<key />");
        harness.InsertRaw("b", null);

        var records = harness.CreateStore().GetAll();

        Assert.Equal(2, records.Count);
        Assert.Null(records[1].Xml);
    }

    [Fact]
    public void Delete_RemovesOnlyTheNamedRows()
    {
        using var harness = new SqliteKeyStoreHarness();
        harness.InsertRaw("a", "<key />");
        harness.InsertRaw("b", "<key />");
        harness.InsertRaw("c", "<key />");

        Assert.True(harness.CreateStore().Delete([1, 3]));

        var row = Assert.Single(harness.ReadRawRows());
        Assert.Equal("b", row.FriendlyName);
    }

    [Fact]
    public void Delete_NoIds_SucceedsWithoutTouchingTheTable()
    {
        using var harness = new SqliteKeyStoreHarness();
        harness.InsertRaw("a", "<key />");

        Assert.True(harness.CreateStore().Delete([]));
        Assert.Single(harness.ReadRawRows());
    }

    [Fact]
    public void Delete_MissingRow_ReportsFailureAndStops()
    {
        using var harness = new SqliteKeyStoreHarness();
        harness.InsertRaw("a", "<key />");
        harness.InsertRaw("b", "<key />");

        // 99 does not exist, so the second identifier must never be attempted.
        Assert.False(harness.CreateStore().Delete([99, 1]));
        Assert.Equal(2, harness.ReadRawRows().Count);
    }

    [Fact]
    public void Store_HonoursAConfiguredTableName()
    {
        using var harness = new SqliteKeyStoreHarness(new ProtectionKeyStoreOptions
        {
            TableName = "infra_data_protection_keys",
        });

        harness.CreateStore().Add("key-1", "<key />");

        Assert.Single(harness.ReadRawRows());
    }

    [Fact]
    public void Add_LargeXml_RoundTripsIntact()
    {
        using var harness = new SqliteKeyStoreHarness();
        var xml = $"<key>{new string('x', 500_000)}</key>";

        harness.CreateStore().Add("big", xml);

        Assert.Equal(xml, Assert.Single(harness.CreateStore().GetAll()).Xml);
    }

    [Fact]
    public void Add_NonAsciiFriendlyName_RoundTripsIntact()
    {
        using var harness = new SqliteKeyStoreHarness();

        harness.CreateStore().Add("ключ-🔑", "<key />");

        Assert.Equal("ключ-🔑", Assert.Single(harness.CreateStore().GetAll()).FriendlyName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3_000_000_000)]
    public void Constructor_CommandTimeoutOutsideTheExpressibleRange_IsRejected(double seconds)
    {
        using var harness = new SqliteKeyStoreHarness();
        var options = new ProtectionKeyStoreOptions { CommandTimeout = TimeSpan.FromSeconds(seconds) };

        // An unchecked cast would otherwise turn this into an arbitrary timeout.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Impl.RelationalProtectionKeyStore(
            () => throw new InvalidOperationException("never reached"),
            harness.Dialect,
            options));
    }

    [Fact]
    public void Constructor_MaximumTimeSpan_IsRejected()
    {
        using var harness = new SqliteKeyStoreHarness();
        var options = new ProtectionKeyStoreOptions { CommandTimeout = TimeSpan.MaxValue };

        Assert.Throws<ArgumentOutOfRangeException>(() => new Impl.RelationalProtectionKeyStore(
            () => throw new InvalidOperationException("never reached"),
            harness.Dialect,
            options));
    }
}
