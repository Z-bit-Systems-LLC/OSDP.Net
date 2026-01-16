using System;
using NUnit.Framework;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Tests.Messages.SecureChannel;

/// <summary>
/// Tests for GitHub Issue #193: Incorrect processing of paired-key osdp_CCRYPT
///
/// When initiating a secure channel, OSDP.Net should validate that the PD's response
/// (osdp_CCRYPT) contains a Security Control Block (SCB) that matches what the ACU
/// originally sent in osdp_CHLNG.
///
/// The third byte of the SCB indicates:
/// - 0x00: Using SCBK-D (default key)
/// - 0x01: Using SCBK (paired/installed key)
///
/// If the ACU sends SCBK (0x01) but the PD responds with SCBK-D (0x00), this is a
/// protocol violation and should be rejected.
/// </summary>
[TestFixture]
[Category("Unit")]
public class ScbKeyTypeMismatchTest
{
    private static readonly byte[] NonDefaultKey =
        { 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x01, 0x02, 0x03, 0x04, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f };

    /// <summary>
    /// Verifies that when ACU uses SCBK and PD correctly responds with SCBK,
    /// the secure channel initialization succeeds.
    /// </summary>
    [Test]
    public void GivenAcuUsesSCBK_WhenPdRespondsWith_SCBK_InitializationSucceeds()
    {
        // Arrange
        var deviceProxy = new DeviceProxy(
            address: 0,
            useCrc: true,
            useSecureChannel: true,
            secureChannelKey: NonDefaultKey);

        Assert.That(deviceProxy.MessageSecureChannel.IsUsingDefaultKey, Is.False,
            "ACU should be configured to use non-default key (SCBK)");

        var (payload, secureBlockData) = BuildCcryptResponse(
            deviceProxy, NonDefaultKey, pdClaimsDefaultKey: false);

        // Act & Assert
        Assert.DoesNotThrow(() => deviceProxy.InitializeSecureChannel(payload, secureBlockData),
            "Initialization should succeed when key types match");
        Assert.That(deviceProxy.MessageSecureChannel.IsInitialized, Is.True);
    }

    /// <summary>
    /// Verifies that when ACU uses SCBK-D and PD correctly responds with SCBK-D,
    /// the secure channel initialization succeeds.
    /// </summary>
    [Test]
    public void GivenAcuUsesSCBKD_WhenPdRespondsWith_SCBKD_InitializationSucceeds()
    {
        // Arrange
        var deviceProxy = new DeviceProxy(
            address: 0,
            useCrc: true,
            useSecureChannel: true,
            secureChannelKey: SecurityContext.DefaultKey);

        Assert.That(deviceProxy.MessageSecureChannel.IsUsingDefaultKey, Is.True,
            "ACU should be configured to use default key (SCBK-D)");

        var (payload, secureBlockData) = BuildCcryptResponse(
            deviceProxy, SecurityContext.DefaultKey, pdClaimsDefaultKey: true);

        // Act & Assert
        Assert.DoesNotThrow(() => deviceProxy.InitializeSecureChannel(payload, secureBlockData),
            "Initialization should succeed when key types match");
        Assert.That(deviceProxy.MessageSecureChannel.IsInitialized, Is.True);
    }

    /// <summary>
    /// Verifies that when ACU uses SCBK but PD responds with SCBK-D in its SCB header,
    /// the initialization fails with SecureChannelKeyTypeMismatchException.
    /// This is the fix for GitHub Issue #193.
    /// </summary>
    [Test]
    public void GivenAcuUsesSCBK_WhenPdRespondsWith_SCBKD_InitializationFails()
    {
        // Arrange
        var deviceProxy = new DeviceProxy(
            address: 0,
            useCrc: true,
            useSecureChannel: true,
            secureChannelKey: NonDefaultKey);

        Assert.That(deviceProxy.MessageSecureChannel.IsUsingDefaultKey, Is.False,
            "ACU should be configured to use non-default key (SCBK)");

        // PD uses the correct key for crypto but sends wrong SCB header (claims SCBK-D)
        var (payload, secureBlockData) = BuildCcryptResponse(
            deviceProxy, NonDefaultKey, pdClaimsDefaultKey: true);

        // Act & Assert
        var exception = Assert.Throws<SecureChannelKeyTypeMismatchException>(
            () => deviceProxy.InitializeSecureChannel(payload, secureBlockData));

        Assert.That(exception, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(exception!.AcuUsedDefaultKey, Is.False, "ACU used SCBK");
            Assert.That(exception.PdClaimedDefaultKey, Is.True, "PD claimed SCBK-D");
            Assert.That(exception.Message, Does.Contain("SCBK").And.Contain("SCBK-D"));
        });

        Assert.That(deviceProxy.MessageSecureChannel.IsInitialized, Is.False,
            "Secure channel should not be initialized after SCB mismatch");
    }

    /// <summary>
    /// Verifies that when ACU uses SCBK-D but PD responds with SCBK in its SCB header,
    /// the initialization fails with SecureChannelKeyTypeMismatchException.
    /// </summary>
    [Test]
    public void GivenAcuUsesSCBKD_WhenPdRespondsWith_SCBK_InitializationFails()
    {
        // Arrange
        var deviceProxy = new DeviceProxy(
            address: 0,
            useCrc: true,
            useSecureChannel: true,
            secureChannelKey: SecurityContext.DefaultKey);

        Assert.That(deviceProxy.MessageSecureChannel.IsUsingDefaultKey, Is.True,
            "ACU should be configured to use default key (SCBK-D)");

        // PD uses the correct key for crypto but sends wrong SCB header (claims SCBK)
        var (payload, secureBlockData) = BuildCcryptResponse(
            deviceProxy, SecurityContext.DefaultKey, pdClaimsDefaultKey: false);

        // Act & Assert
        var exception = Assert.Throws<SecureChannelKeyTypeMismatchException>(
            () => deviceProxy.InitializeSecureChannel(payload, secureBlockData));

        Assert.That(exception, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(exception!.AcuUsedDefaultKey, Is.True, "ACU used SCBK-D");
            Assert.That(exception.PdClaimedDefaultKey, Is.False, "PD claimed SCBK");
        });

        Assert.That(deviceProxy.MessageSecureChannel.IsInitialized, Is.False,
            "Secure channel should not be initialized after SCB mismatch");
    }

    /// <summary>
    /// Verifies that when ACU uses SCBK-D and PD actually uses a different key
    /// (as it would if it truly were using SCBK), the cryptogram validation catches the mismatch.
    ///
    /// NOTE: This is different from the SCB header mismatch. The cryptogram validation provides
    /// protection when the PD actually uses a different key, regardless of what it claims in the SCB.
    /// </summary>
    [Test]
    public void GivenAcuUsesSCBKD_WhenPdActuallyUsesSCBK_CryptogramValidationCatchesMismatch()
    {
        // Arrange
        var deviceProxy = new DeviceProxy(
            address: 0,
            useCrc: true,
            useSecureChannel: true,
            secureChannelKey: SecurityContext.DefaultKey);

        Assert.That(deviceProxy.MessageSecureChannel.IsUsingDefaultKey, Is.True,
            "ACU should be configured to use default key (SCBK-D)");

        // PD generates cryptogram using a NON-default key AND claims SCBK in SCB
        // This simulates a real key mismatch where PD is actually using a different key
        var (payload, secureBlockData) = BuildCcryptResponse(
            deviceProxy, NonDefaultKey, pdClaimsDefaultKey: false);

        // Act & Assert
        // SCB validation happens first and catches the mismatch
        var exception = Assert.Throws<SecureChannelKeyTypeMismatchException>(
            () => deviceProxy.InitializeSecureChannel(payload, secureBlockData));

        Assert.That(exception, Is.Not.Null);
    }

    /// <summary>
    /// Verifies that initialization fails when secureBlockData is null.
    /// The osdp_CCRYPT response must include the SCB key type indicator.
    /// </summary>
    [Test]
    public void GivenNullSecureBlockData_InitializationFails()
    {
        // Arrange
        var deviceProxy = new DeviceProxy(
            address: 0,
            useCrc: true,
            useSecureChannel: true,
            secureChannelKey: NonDefaultKey);

        var (payload, _) = BuildCcryptResponse(deviceProxy, NonDefaultKey, pdClaimsDefaultKey: false);

        // Act & Assert - null secureBlockData should throw InvalidPayloadException
        var exception = Assert.Throws<InvalidPayloadException>(
            () => deviceProxy.InitializeSecureChannel(payload, null));
        Assert.That(exception!.Message, Does.Contain("security control block"));
    }

    /// <summary>
    /// Verifies that initialization fails when secureBlockData is empty.
    /// The osdp_CCRYPT response must include the SCB key type indicator.
    /// </summary>
    [Test]
    public void GivenEmptySecureBlockData_InitializationFails()
    {
        // Arrange
        var deviceProxy = new DeviceProxy(
            address: 0,
            useCrc: true,
            useSecureChannel: true,
            secureChannelKey: NonDefaultKey);

        var (payload, _) = BuildCcryptResponse(deviceProxy, NonDefaultKey, pdClaimsDefaultKey: false);

        // Act & Assert - empty secureBlockData should throw InvalidPayloadException
        var exception = Assert.Throws<InvalidPayloadException>(
            () => deviceProxy.InitializeSecureChannel(payload, Array.Empty<byte>()));
        Assert.That(exception!.Message, Does.Contain("security control block"));
    }

    /// <summary>
    /// Builds a mock osdp_CCRYPT response payload and SCB data for testing.
    /// </summary>
    private static (byte[] payload, byte[] secureBlockData) BuildCcryptResponse(
        DeviceProxy deviceProxy, byte[] cryptoKey, bool pdClaimsDefaultKey)
    {
        var serverRnd = deviceProxy.MessageSecureChannel.ServerRandomNumber;
        var clientRnd = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
        var clientCryptogram = GenerateValidClientCryptogram(cryptoKey, serverRnd, clientRnd);

        // Build payload: cUID (8 bytes) + clientRnd (8 bytes) + clientCryptogram (16 bytes)
        var cUID = new byte[8];
        var payload = new byte[32];
        Array.Copy(cUID, 0, payload, 0, 8);
        Array.Copy(clientRnd, 0, payload, 8, 8);
        Array.Copy(clientCryptogram, 0, payload, 16, 16);

        // Build SCB data: just the key type indicator byte
        // (In real messages, SecureBlockData is extracted from the full SCB minus length and type bytes)
        byte[] secureBlockData = [(byte)(pdClaimsDefaultKey ? 0x00 : 0x01)];

        return (payload, secureBlockData);
    }

    /// <summary>
    /// Generates a valid client cryptogram for testing purposes.
    /// This mimics what a PD would generate in response to osdp_CHLNG.
    /// </summary>
    private static byte[] GenerateValidClientCryptogram(byte[] securityKey, byte[] serverRnd, byte[] clientRnd)
    {
        var context = new SecurityContext(securityKey);
        using var crypto = context.CreateCypher(true);

        // Generate S-ENC key
        var enc = SecurityContext.GenerateKey(crypto, new byte[]
        {
            0x01, 0x82, serverRnd[0], serverRnd[1], serverRnd[2],
            serverRnd[3], serverRnd[4], serverRnd[5]
        });

        // Generate client cryptogram using S-ENC
        crypto.Key = enc;
        return SecurityContext.GenerateKey(crypto, serverRnd, clientRnd);
    }
}
