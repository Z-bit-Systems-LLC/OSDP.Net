using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Tests.Messages.SecureChannel;

/// <summary>
/// Tests for GitHub Issue #217: the ACU must pick a new RND.A after a failed secure channel
/// handshake, because the session keys are derived only from the SCBK and RND.A
/// (OSDP v2.2.2 Annex D.3), and RND.A must come from a cryptographic random number generator.
/// </summary>
[TestFixture]
[Category("Unit")]
public class HandshakeRandomNumberTest
{
    private static readonly byte[] NonDefaultKey =
        [0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x01, 0x02, 0x03, 0x04, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f];

    private static readonly byte[] OtherKey =
        [0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90, 0xa0, 0xb0, 0xc0, 0xd0, 0xe0, 0xf0, 0x01];

    [Test]
    public void GivenClientCryptogramMismatch_WhenInitializing_NewServerRandomNumberIsChosen()
    {
        // Arrange
        var deviceProxy = CreateDeviceProxy();
        var serverRandomNumber = CopyServerRandomNumber(deviceProxy);

        // The PD claims the right key type but computed the cryptogram with a different key
        var payload = BuildCcryptPayload(deviceProxy, OtherKey);

        // Act
        var exception = Assert.Throws<Exception>(() => deviceProxy.InitializeSecureChannel(payload, [0x01]));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("Invalid client cryptogram"));
            Assert.That(deviceProxy.MessageSecureChannel.IsInitialized, Is.False);
            Assert.That(deviceProxy.MessageSecureChannel.ServerRandomNumber, Is.Not.EqualTo(serverRandomNumber),
                "RND.A must change after a rejected client cryptogram");
        });
    }

    [Test]
    public void GivenKeyTypeMismatch_WhenInitializing_NewServerRandomNumberIsChosen()
    {
        // Arrange
        var deviceProxy = CreateDeviceProxy();
        var serverRandomNumber = CopyServerRandomNumber(deviceProxy);
        var payload = BuildCcryptPayload(deviceProxy, NonDefaultKey);

        // Act: the PD claims SCBK-D while the ACU sent SCBK
        Assert.Throws<SecureChannelKeyTypeMismatchException>(
            () => deviceProxy.InitializeSecureChannel(payload, [0x00]));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(deviceProxy.MessageSecureChannel.IsInitialized, Is.False);
            Assert.That(deviceProxy.MessageSecureChannel.ServerRandomNumber, Is.Not.EqualTo(serverRandomNumber),
                "RND.A must change after a key type mismatch");
        });
    }

    [Test]
    public void GivenInvalidPayload_WhenInitializing_NewServerRandomNumberIsChosen()
    {
        // Arrange
        var deviceProxy = CreateDeviceProxy();
        var serverRandomNumber = CopyServerRandomNumber(deviceProxy);

        // Act
        Assert.Throws<InvalidPayloadException>(() => deviceProxy.InitializeSecureChannel(new byte[16], [0x01]));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(deviceProxy.MessageSecureChannel.IsInitialized, Is.False);
            Assert.That(deviceProxy.MessageSecureChannel.ServerRandomNumber, Is.Not.EqualTo(serverRandomNumber),
                "RND.A must change after an invalid osdp_CCRYPT payload");
        });
    }

    [Test]
    public void GivenValidResponse_WhenInitializing_ServerRandomNumberIsKept()
    {
        // Arrange
        var deviceProxy = CreateDeviceProxy();
        var serverRandomNumber = CopyServerRandomNumber(deviceProxy);
        var payload = BuildCcryptPayload(deviceProxy, NonDefaultKey);

        // Act
        deviceProxy.InitializeSecureChannel(payload, [0x01]);

        // Assert: osdp_SCRYPT and the session keys depend on the RND.A sent in osdp_CHLNG
        Assert.Multiple(() =>
        {
            Assert.That(deviceProxy.MessageSecureChannel.IsInitialized, Is.True);
            Assert.That(deviceProxy.MessageSecureChannel.ServerRandomNumber, Is.EqualTo(serverRandomNumber));
        });
    }

    [Test]
    public void GivenManySessionResets_ServerRandomNumbersAreAllDifferent()
    {
        // Arrange
        var context = new SecurityContext(NonDefaultKey);
        var seen = new HashSet<string>();

        // Act
        for (var index = 0; index < 1000; index++)
        {
            context.Reset();
            seen.Add(Convert.ToBase64String(context.ServerRandomNumber));
        }

        // Assert
        Assert.That(seen, Has.Count.EqualTo(1000));
    }

    [Test]
    public void GivenContextsCreatedTogether_ServerRandomNumbersAreDifferent()
    {
        // Arrange & Act: System.Random instances created in the same tick on .NET Framework
        // return the same bytes, so contexts created back to back would have shared RND.A
        var numbers = Enumerable.Range(0, 100)
            .Select(_ => Convert.ToBase64String(new SecurityContext(NonDefaultKey).ServerRandomNumber))
            .ToList();

        // Assert
        Assert.That(numbers.Distinct().Count(), Is.EqualTo(numbers.Count));
    }

    private static DeviceProxy CreateDeviceProxy() =>
        new(address: 0, useCrc: true, useSecureChannel: true, secureChannelKey: NonDefaultKey);

    // ServerRandomNumber is refilled in place, so keep a copy to compare against
    private static byte[] CopyServerRandomNumber(DeviceProxy deviceProxy) =>
        deviceProxy.MessageSecureChannel.ServerRandomNumber.ToArray();

    /// <summary>
    /// Builds the osdp_CCRYPT payload (cUID[8] + RND.B[8] + client cryptogram[16]) that a PD
    /// holding the given key would send.
    /// </summary>
    private static byte[] BuildCcryptPayload(DeviceProxy deviceProxy, byte[] pdKey)
    {
        var serverRnd = deviceProxy.MessageSecureChannel.ServerRandomNumber;
        byte[] clientRnd = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88];

        var context = new SecurityContext(pdKey);
        using var crypto = context.CreateCypher(true);
        crypto.Key = SecurityContext.GenerateKey(crypto, [
            0x01, 0x82, serverRnd[0], serverRnd[1], serverRnd[2], serverRnd[3], serverRnd[4], serverRnd[5]
        ]);
        var clientCryptogram = SecurityContext.GenerateKey(crypto, serverRnd, clientRnd);

        return new byte[8].Concat(clientRnd).Concat(clientCryptogram).ToArray();
    }
}
