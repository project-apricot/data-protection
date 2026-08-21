using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Tests;

/// <summary>
/// Pins the SQL text. These statements run against a table someone else already created, so a
/// change here is a change to a published contract, not a refactor.
/// </summary>
public class ProtectionKeySqlDialectTests
{
    private static readonly ProtectionKeyStoreOptions Default = new();

    [Fact]
    public void MySql_Statements_AreExact()
    {
        var dialect = new MySqlProtectionKeyDialect();

        Assert.Equal(
            "SELECT `Id`, `FriendlyName`, `Xml` FROM `DataProtectionKeys`",
            dialect.GetSelectAllSql(Default));
        Assert.Equal(
            "INSERT INTO `DataProtectionKeys` (`FriendlyName`, `Xml`) VALUES (@FriendlyName, @Xml)",
            dialect.GetInsertSql(Default));
        Assert.Equal(
            "DELETE FROM `DataProtectionKeys` WHERE `Id` = @Id",
            dialect.GetDeleteSql(Default));
    }

    [Fact]
    public void PostgreSql_Statements_AreExact()
    {
        var dialect = new PostgreSqlProtectionKeyDialect();

        Assert.Equal(
            "SELECT \"Id\", \"FriendlyName\", \"Xml\" FROM \"DataProtectionKeys\"",
            dialect.GetSelectAllSql(Default));
        Assert.Equal(
            "INSERT INTO \"DataProtectionKeys\" (\"FriendlyName\", \"Xml\") VALUES (@FriendlyName, @Xml)",
            dialect.GetInsertSql(Default));
    }

    [Fact]
    public void SqlServer_Statements_AreExact()
    {
        var dialect = new SqlServerProtectionKeyDialect();

        Assert.Equal(
            "SELECT [Id], [FriendlyName], [Xml] FROM [DataProtectionKeys]",
            dialect.GetSelectAllSql(Default));
        Assert.Equal(
            "DELETE FROM [DataProtectionKeys] WHERE [Id] = @Id",
            dialect.GetDeleteSql(Default));
    }

    [Fact]
    public void Sqlite_Statements_AreExact()
    {
        Assert.Equal(
            "SELECT \"Id\", \"FriendlyName\", \"Xml\" FROM \"DataProtectionKeys\"",
            new SqliteProtectionKeyDialect().GetSelectAllSql(Default));
    }

    [Fact]
    public void MySql_CreateTableScript_IsExact()
    {
        Assert.Equal(
            """
            CREATE TABLE `DataProtectionKeys` (
              `Id` INT NOT NULL AUTO_INCREMENT,
              `FriendlyName` TEXT NULL,
              `Xml` LONGTEXT NULL,
              PRIMARY KEY (`Id`)
            );
            """,
            new MySqlProtectionKeyDialect().GetCreateTableScript(Default));
    }

    [Fact]
    public void PostgreSql_CreateTableScript_IsExact()
    {
        Assert.Equal(
            """
            CREATE TABLE "DataProtectionKeys" (
              "Id" SERIAL PRIMARY KEY,
              "FriendlyName" TEXT NULL,
              "Xml" TEXT NULL
            );
            """,
            new PostgreSqlProtectionKeyDialect().GetCreateTableScript(Default));
    }

    [Fact]
    public void SqlServer_CreateTableScript_IsExact()
    {
        Assert.Equal(
            """
            CREATE TABLE [DataProtectionKeys] (
              [Id] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
              [FriendlyName] NVARCHAR(MAX) NULL,
              [Xml] NVARCHAR(MAX) NULL
            );
            """,
            new SqlServerProtectionKeyDialect().GetCreateTableScript(Default));
    }

    [Fact]
    public void Sqlite_CreateTableScript_IsExact()
    {
        Assert.Equal(
            """
            CREATE TABLE "DataProtectionKeys" (
              "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
              "FriendlyName" TEXT NULL,
              "Xml" TEXT NULL
            );
            """,
            new SqliteProtectionKeyDialect().GetCreateTableScript(Default));
    }

    [Fact]
    public void ColumnNames_MatchTheEntityFrameworkCoreProvider()
    {
        // The official provider's entity is DataProtectionKey { Id, FriendlyName, Xml }. Matching
        // it is what lets one table be read by either implementation.
        var sql = new SqlServerProtectionKeyDialect().GetSelectAllSql(Default);

        Assert.Contains("[Id]", sql, StringComparison.Ordinal);
        Assert.Contains("[FriendlyName]", sql, StringComparison.Ordinal);
        Assert.Contains("[Xml]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_QualifiesTheTable()
    {
        var options = new ProtectionKeyStoreOptions { SchemaName = "keys" };

        Assert.Equal(
            "SELECT [Id], [FriendlyName], [Xml] FROM [keys].[DataProtectionKeys]",
            new SqlServerProtectionKeyDialect().GetSelectAllSql(options));
    }

    [Fact]
    public void BlankSchema_IsTreatedAsUnset_NotAsAnError()
    {
        // A templated environment variable that is present but empty binds to "", and the dialect
        // already treats that as "no schema". Validation must agree.
        var options = new ProtectionKeyStoreOptions { SchemaName = string.Empty };

        Assert.Equal(
            "SELECT [Id], [FriendlyName], [Xml] FROM [DataProtectionKeys]",
            new SqlServerProtectionKeyDialect().GetSelectAllSql(options));
    }

    [Fact]
    public void Sqlite_BlankSchema_IsAccepted_BecauseItMeansUnset()
    {
        var options = new ProtectionKeyStoreOptions { SchemaName = string.Empty };

        Assert.Equal(
            "SELECT \"Id\", \"FriendlyName\", \"Xml\" FROM \"DataProtectionKeys\"",
            new SqliteProtectionKeyDialect().GetSelectAllSql(options));
    }

    [Fact]
    public void Sqlite_Schema_IsRejectedBecauseTheEngineHasNone()
    {
        var options = new ProtectionKeyStoreOptions { SchemaName = "keys" };

        Assert.Throws<ArgumentException>(
            () => new SqliteProtectionKeyDialect().GetSelectAllSql(options));
    }

    [Theory]
    [InlineData("keys`; DROP TABLE users; --")]
    [InlineData("keys\"; DROP TABLE users; --")]
    [InlineData("keys]; DROP TABLE users; --")]
    [InlineData("has space")]
    [InlineData("has-hyphen")]
    [InlineData("1leading_digit")]
    [InlineData("")]
    public void TableName_ThatCouldEscapeTheStatement_IsRejected(string tableName)
    {
        var options = new ProtectionKeyStoreOptions { TableName = tableName };

        Assert.Throws<ArgumentException>(
            () => new MySqlProtectionKeyDialect().GetSelectAllSql(options));
    }

    [Fact]
    public void TableName_OverTheLengthLimit_IsRejected()
    {
        var options = new ProtectionKeyStoreOptions { TableName = new string('a', 64) };

        Assert.Throws<ArgumentException>(
            () => new MySqlProtectionKeyDialect().GetSelectAllSql(options));
    }

    [Fact]
    public void BuiltIn_CoversEveryDialectByName()
    {
        var byName = ProtectionKeySqlDialects.Compose(additional: null);

        Assert.Equal(4, byName.Count);
        Assert.True(byName.ContainsKey("mysql"));
        Assert.True(byName.ContainsKey("PostgreSql"));
        Assert.True(byName.ContainsKey("SQLSERVER"));
        Assert.True(byName.ContainsKey("Sqlite"));
    }

    [Fact]
    public void Compose_ExtraSharingABuiltInName_ReplacesIt()
    {
        var replacement = new StubDialect(MySqlProtectionKeyDialect.DialectName);

        var byName = ProtectionKeySqlDialects.Compose([replacement]);

        Assert.Equal(4, byName.Count);
        Assert.Same(replacement, byName["MySql"]);
    }

    [Fact]
    public void Compose_ExtraWithABlankName_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => ProtectionKeySqlDialects.Compose([new StubDialect("  ")]));
    }

    private sealed class StubDialect(string name) : IProtectionKeySqlDialect
    {
        public string GetName() => name;

        public string GetSelectAllSql(ProtectionKeyStoreOptions options) => string.Empty;

        public string GetInsertSql(ProtectionKeyStoreOptions options) => string.Empty;

        public string GetDeleteSql(ProtectionKeyStoreOptions options) => string.Empty;

        public string GetCreateTableScript(ProtectionKeyStoreOptions options) => string.Empty;
    }
}
