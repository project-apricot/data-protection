using System.Security.Cryptography.X509Certificates;
using ApricotFramework.DataProtection.AspNetCore.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.DataProtection.AspNetCore.Tests;

public class KeyProtectionTests
{
    private static IConfiguration EmptyConfiguration()
    {
        return new ConfigurationBuilder().Build();
    }

    private static IDataProtectionBuilder Stored(IServiceCollection services)
    {
        return services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName("/app")
            .PersistKeysToStore(new FakeProtectionKeyStore());
    }

    private static KeyManagementOptions Resolve(ServiceProvider provider)
    {
        return provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
    }

    [Fact]
    public void Storage_WithNoEncryptorAndNoAcknowledgement_FailsAndSaysWhy()
    {
        var services = new ServiceCollection();
        Stored(services);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var error = Assert.Throws<OptionsValidationException>(() => Resolve(provider));
        Assert.Contains("without encryption", error.Message, StringComparison.Ordinal);
        Assert.Contains("AllowUnprotectedKeys", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowUnprotectedKeys_LetsStorageStart()
    {
        var services = new ServiceCollection();
        Stored(services).AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Null(Resolve(provider).XmlEncryptor);
    }

    [Fact]
    public void AllowUnprotectedKeys_BeforeStorage_AlsoWorks()
    {
        var services = new ServiceCollection();
        services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName("/app")
            .AllowUnprotectedKeys()
            .PersistKeysToStore(new FakeProtectionKeyStore());

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Null(Resolve(provider).XmlEncryptor);
    }

    [Fact]
    public void AllowUnprotectedKeys_ConfiguresNoEncryptor_SoStoredKeysAreUnchanged()
    {
        var services = new ServiceCollection();
        Stored(services).AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        // Registering a do-nothing encryptor would wrap every element in encryptedSecret and name
        // the decryptor type inside it, changing the stored shape forever. This must not.
        Assert.Null(Resolve(provider).XmlEncryptor);
    }

    [Fact]
    public void AllowUnprotectedKeys_CalledTwice_IsIdempotent()
    {
        var services = new ServiceCollection();
        Stored(services)
            .AllowUnprotectedKeys()
            .AllowUnprotectedKeys();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Null(Resolve(provider).XmlEncryptor);
    }

    [Fact]
    public void ARealEncryptor_SatisfiesTheRequirementWithoutAnyAcknowledgement()
    {
        using var certificate = SelfSigned();

        var services = new ServiceCollection();
        Stored(services).ProtectKeysWithCertificate(certificate);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(Resolve(provider).XmlEncryptor);
    }

    [Fact]
    public void NoStorage_IsNotOurConcern()
    {
        // The framework keeps its own default at-rest encryption when nothing replaces the key
        // storage, so a host that adds no store has nothing to answer for.
        var services = new ServiceCollection();
        services.AddDataProtectionCore(EmptyConfiguration()).SetApplicationName("/app");

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Null(Resolve(provider).XmlRepository);
    }

    private static X509Certificate2 SelfSigned()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest("CN=apricot-tests", key,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
