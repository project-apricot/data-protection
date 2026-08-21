using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace ApricotFramework.DataProtection.AspNetCore.Impl;

/// <summary>
/// Presents a <see cref="IProtectionKeyStore"/> to the key manager.
/// </summary>
/// <remarks>
/// A row that holds unparseable XML fails the whole read. That is deliberate and matches every
/// built-in repository: the key manager stores revocations here too, so skipping a row it could
/// not read risks treating a revoked key as live.
/// </remarks>
public sealed class ProtectionKeyXmlRepository : IDeletableXmlRepository
{
    /// <summary>
    /// The store holding the rows.
    /// </summary>
    private readonly IProtectionKeyStore store;

    /// <summary>
    /// Creates a repository over a store.
    /// </summary>
    /// <param name="store">The store holding the rows.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public ProtectionKeyXmlRepository(IProtectionKeyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        this.store = store;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var elements = new List<XElement>();

        foreach (var record in this.store.GetAll())
        {
            if (!string.IsNullOrWhiteSpace(record.Xml))
            {
                elements.Add(XElement.Parse(record.Xml));
            }
        }

        return elements.AsReadOnly();
    }

    /// <inheritdoc />
    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);

        this.store.Add(friendlyName, element.ToString(SaveOptions.DisableFormatting));
    }

    /// <inheritdoc />
    public bool DeleteElements(Action<IReadOnlyCollection<IDeletableElement>> chooseElements)
    {
        ArgumentNullException.ThrowIfNull(chooseElements);

        var deletable = new List<DeletableElement>();

        foreach (var record in this.store.GetAll())
        {
            if (!string.IsNullOrWhiteSpace(record.Xml))
            {
                deletable.Add(new DeletableElement(record.Id, XElement.Parse(record.Xml)));
            }
        }

        chooseElements(deletable);

        var ids = deletable
            .Where(candidate => candidate.DeletionOrder.HasValue)
            .OrderBy(candidate => candidate.DeletionOrder.GetValueOrDefault())
            .Select(candidate => candidate.Id)
            .ToList();

        return this.store.Delete(ids);
    }

    /// <summary>
    /// One element the caller may mark for deletion, remembering the row it came from.
    /// </summary>
    private sealed class DeletableElement(int id, XElement element) : IDeletableElement
    {
        /// <summary>
        /// Gets the row this element was read from.
        /// </summary>
        public int Id { get; } = id;

        /// <inheritdoc />
        public XElement Element { get; } = element;

        /// <inheritdoc />
        public int? DeletionOrder { get; set; }
    }
}
