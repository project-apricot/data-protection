namespace ApricotFramework.DataProtection.AspNetCore.Impl;

/// <summary>
/// Records that a host has accepted keys being written without an encryptor.
/// </summary>
/// <remarks>
/// Deliberately configures nothing. Registering a do-nothing encryptor would look equivalent and
/// is not: the framework wraps every stored element in an <c>encryptedSecret</c> naming the
/// decryptor's assembly-qualified type, so the keys would change shape, stop being readable by a
/// plain framework setup, and become unreadable altogether if the encryptor were later removed.
/// </remarks>
internal sealed class UnprotectedKeysAllowed;
