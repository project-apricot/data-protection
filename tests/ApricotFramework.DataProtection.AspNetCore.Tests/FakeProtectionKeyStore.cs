namespace ApricotFramework.DataProtection.AspNetCore.Tests;

/// <summary>
/// An in-memory store, so repository behaviour can be tested without a database.
/// </summary>
internal sealed class FakeProtectionKeyStore : IProtectionKeyStore
{
    private readonly List<ProtectionKeyRecord> records = [];

    private int nextId = 1;

    public List<int> DeletedInOrder { get; } = [];

    public HashSet<int> FailingIds { get; } = [];

    public void Seed(string? friendlyName, string? xml)
    {
        this.records.Add(new ProtectionKeyRecord(this.nextId++, friendlyName, xml));
    }

    public IReadOnlyList<ProtectionKeyRecord> GetAll()
    {
        return this.records.AsReadOnly();
    }

    public void Add(string friendlyName, string? xml)
    {
        this.Seed(friendlyName, xml);
    }

    public bool Delete(IReadOnlyList<int> orderedIds)
    {
        foreach (var id in orderedIds)
        {
            if (this.FailingIds.Contains(id))
            {
                return false;
            }

            this.DeletedInOrder.Add(id);
            this.records.RemoveAll(record => record.Id == id);
        }

        return true;
    }
}
