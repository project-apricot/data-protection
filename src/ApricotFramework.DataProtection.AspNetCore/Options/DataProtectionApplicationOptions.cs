namespace ApricotFramework.DataProtection.AspNetCore.Options;

/// <summary>
/// The identity the key ring is scoped to.
/// </summary>
public sealed class DataProtectionApplicationOptions
{
    /// <summary>
    /// The application discriminator, or null for the framework's own default.
    /// </summary>
    /// <remarks>
    /// Two applications sharing a store must set different values, or each can unprotect the
    /// other's payloads. Changing it makes existing payloads unreadable, so it is effectively
    /// frozen once anything has been protected.
    /// </remarks>
    public string? Application { get; set; }
}
