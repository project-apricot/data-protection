namespace ApricotFramework.DataProtection;

/// <summary>
/// The columns of the key ring table.
/// </summary>
/// <remarks>
/// Fixed, and named as the official Entity Framework Core provider names them, so one table serves
/// either implementation. A custom dialect must select all three; the store reads them by name, so
/// the order it lists them in does not matter.
/// </remarks>
public static class ProtectionKeyColumns
{
    /// <summary>
    /// The identity the database assigns.
    /// </summary>
    public const string Id = "Id";

    /// <summary>
    /// The name the key manager gave the element.
    /// </summary>
    public const string FriendlyName = "FriendlyName";

    /// <summary>
    /// The serialized element.
    /// </summary>
    public const string Xml = "Xml";
}
