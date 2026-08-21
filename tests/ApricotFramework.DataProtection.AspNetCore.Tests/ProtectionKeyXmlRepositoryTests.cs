using System.Xml;
using System.Xml.Linq;
using ApricotFramework.DataProtection.AspNetCore.Impl;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace ApricotFramework.DataProtection.AspNetCore.Tests;

public class ProtectionKeyXmlRepositoryTests
{
    [Fact]
    public void StoreElement_WritesTheUnformattedElement()
    {
        var store = new FakeProtectionKeyStore();
        var element = XElement.Parse("<key id=\"a\">\n  <descriptor>  x  </descriptor>\n</key>");

        new ProtectionKeyXmlRepository(store).StoreElement(element, "key-1");

        // The stored string is a published format: it is what the official Entity Framework Core
        // provider writes, so a table stays readable by either implementation.
        Assert.Equal(
            "<key id=\"a\"><descriptor>  x  </descriptor></key>",
            Assert.Single(store.GetAll()).Xml);
    }

    [Fact]
    public void StoreElement_MatchesSaveOptionsDisableFormattingExactly()
    {
        var store = new FakeProtectionKeyStore();
        var element = XElement.Parse("<key><a x=\"1\" /><b>text</b></key>");

        new ProtectionKeyXmlRepository(store).StoreElement(element, "key-1");

        Assert.Equal(element.ToString(SaveOptions.DisableFormatting), Assert.Single(store.GetAll()).Xml);
    }

    [Fact]
    public void GetAllElements_RoundTripsAStoredElement()
    {
        var store = new FakeProtectionKeyStore();
        var repository = new ProtectionKeyXmlRepository(store);
        var element = XElement.Parse("<key id=\"a\"><descriptor>x</descriptor></key>");

        repository.StoreElement(element, "key-1");

        Assert.True(XNode.DeepEquals(element, Assert.Single(repository.GetAllElements())));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void GetAllElements_SkipsRowsHoldingNoElement(string? xml)
    {
        var store = new FakeProtectionKeyStore();
        store.Seed("empty", xml);

        Assert.Empty(new ProtectionKeyXmlRepository(store).GetAllElements());
    }

    [Fact]
    public void GetAllElements_MalformedRow_Throws()
    {
        var store = new FakeProtectionKeyStore();
        store.Seed("good", "<key />");
        store.Seed("truncated", "<key");

        // Deliberate, and the same as every built-in repository: the key manager stores
        // revocations here too, so a row it cannot read must not be quietly dropped.
        Assert.Throws<XmlException>(() => new ProtectionKeyXmlRepository(store).GetAllElements());
    }

    [Fact]
    public void DeleteElements_DeletesOnlyWhatTheCallerMarked()
    {
        var store = new FakeProtectionKeyStore();
        store.Seed("a", "<key id=\"a\" />");
        store.Seed("b", "<key id=\"b\" />");
        store.Seed("c", "<key id=\"c\" />");

        var deleted = new ProtectionKeyXmlRepository(store).DeleteElements(elements =>
        {
            foreach (var element in elements)
            {
                if (element.Element.Attribute("id")?.Value != "b")
                {
                    element.DeletionOrder = 1;
                }
            }
        });

        Assert.True(deleted);
        Assert.Equal([1, 3], store.DeletedInOrder);
    }

    [Fact]
    public void DeleteElements_DeletesInIncreasingDeletionOrder()
    {
        var store = new FakeProtectionKeyStore();
        store.Seed("a", "<key id=\"a\" />");
        store.Seed("b", "<key id=\"b\" />");
        store.Seed("c", "<key id=\"c\" />");

        new ProtectionKeyXmlRepository(store).DeleteElements(elements =>
        {
            var order = elements.Count;

            foreach (var element in elements)
            {
                element.DeletionOrder = order--;
            }
        });

        Assert.Equal([3, 2, 1], store.DeletedInOrder);
    }

    [Fact]
    public void DeleteElements_NothingMarked_DeletesNothingAndSucceeds()
    {
        var store = new FakeProtectionKeyStore();
        store.Seed("a", "<key />");

        Assert.True(new ProtectionKeyXmlRepository(store).DeleteElements(_ => { }));
        Assert.Empty(store.DeletedInOrder);
    }

    [Fact]
    public void DeleteElements_FailedDeletion_SkipsTheRemainderAndReportsFailure()
    {
        var store = new FakeProtectionKeyStore();
        store.Seed("a", "<key id=\"a\" />");
        store.Seed("b", "<key id=\"b\" />");
        store.FailingIds.Add(1);

        var deleted = new ProtectionKeyXmlRepository(store).DeleteElements(elements =>
        {
            foreach (var element in elements)
            {
                element.DeletionOrder = element.Element.Attribute("id")?.Value == "a" ? 1 : 2;
            }
        });

        Assert.False(deleted);
        Assert.Empty(store.DeletedInOrder);
    }

    [Fact]
    public void Repository_SupportsDeletion()
    {
        // The official Entity Framework Core provider implements only IXmlRepository, so key
        // deletion is unavailable there. It is available here.
        Assert.IsAssignableFrom<IDeletableXmlRepository>(
            new ProtectionKeyXmlRepository(new FakeProtectionKeyStore()));
    }
}
