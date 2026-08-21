using ApricotFramework.DataProtection.Dialects;
using ApricotFramework.DataProtection.Options;

namespace ApricotFramework.DataProtection.Tests;

/// <summary>
/// Keeps the documented DDL and the generated DDL the same text.
/// </summary>
/// <remarks>
/// Someone adding a migration by hand copies the script out of the docs, so a docs page that has
/// drifted from the statements the store runs is a real defect rather than a typo.
/// </remarks>
public class DocumentationDdlTests
{
    [Fact]
    public void RelationalPage_ContainsEveryDialectsCreateTableScript()
    {
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "relational.mdx"))
            .ReplaceLineEndings("\n");
        var options = new ProtectionKeyStoreOptions();

        foreach (var dialect in ProtectionKeySqlDialects.BuiltIn())
        {
            var script = dialect.GetCreateTableScript(options).ReplaceLineEndings("\n");

            Assert.Contains(script, page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RelationalPage_ContainsTheStatementsTheStoreRuns()
    {
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "relational.mdx"))
            .ReplaceLineEndings("\n");
        var options = new ProtectionKeyStoreOptions();
        var dialect = new MySqlProtectionKeyDialect();

        Assert.Contains(dialect.GetSelectAllSql(options), page, StringComparison.Ordinal);
        Assert.Contains(dialect.GetInsertSql(options), page, StringComparison.Ordinal);
        Assert.Contains(dialect.GetDeleteSql(options), page, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_DocumentsTheDefaultTableName()
    {
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md"));

        Assert.Contains(ProtectionKeyStoreOptions.DefaultTableName, readme, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApricotFramework.DataProtection.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }
}
