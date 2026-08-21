namespace ApricotFramework.DataProtection;

/// <summary>
/// One stored row of the key ring.
/// </summary>
/// <param name="Id">The row identifier, used to delete the row.</param>
/// <param name="FriendlyName">The name the key manager gave the element.</param>
/// <param name="Xml">The serialized element, or null for a row that holds none.</param>
/// <remarks>
/// The key manager stores revocations here alongside keys, so a row is not necessarily a key.
/// </remarks>
public sealed record ProtectionKeyRecord(int Id, string? FriendlyName, string? Xml);
