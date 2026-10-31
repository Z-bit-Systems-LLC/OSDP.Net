using System;
using System.Security.Cryptography;
using NUnit.Framework;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Tests.Messages.SecureChannel;

/// <summary>
/// Checks the BouncyCastle AES fallback, used where the platform has no AES (browser WebAssembly),
/// against published test vectors and against the platform implementation.
/// </summary>
[TestFixture]
[Category("Unit")]
public class BouncyCastleAesTest
{
    private static readonly byte[] CbcKey = Convert.FromHexString("2b7e151628aed2a6abf7158809cf4f3c");
    private static readonly byte[] CbcIv = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");

    private static readonly byte[] CbcPlaintext = Convert.FromHexString(
        "6bc1bee22e409f96e93d7e117393172aae2d8a571e03ac9c9eb76fac45af8e51" +
        "30c81c46a35ce411e5fbc1191a0a52eff69f2445df4f9b17ad2b417be66c3710");

    private static readonly byte[] CbcCiphertext = Convert.FromHexString(
        "7649abac8119b246cee98e9b12e9197d5086cb9b507219ee95db113a917678b2" +
        "73bed6b8e3c1743b7116e69e222295163ff1caa1681fac09120eca307586e1a7");

    // FIPS-197 Appendix C.1 and C.3
    [TestCase("000102030405060708090a0b0c0d0e0f", "69c4e0d86a7b0430d8cdb78070b4c55a")]
    [TestCase("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", "8ea2b7ca516745bfeafc49904b496089")]
    public void EcbEncrypt_MatchesFips197Vector(string keyHex, string expectedHex)
    {
        var plaintext = Convert.FromHexString("00112233445566778899aabbccddeeff");

        var ciphertext = Ecb(new BouncyCastleAes(), Convert.FromHexString(keyHex), plaintext, encrypt: true);

        Assert.That(Convert.ToHexString(ciphertext), Is.EqualTo(expectedHex.ToUpperInvariant()));
    }

    // NIST SP 800-38A F.2.1 and F.2.2
    [Test]
    public void CbcEncryptAndDecrypt_MatchSp80038AVectors()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Cbc(new BouncyCastleAes(), CbcKey, CbcIv, CbcPlaintext, encrypt: true),
                Is.EqualTo(CbcCiphertext), "encrypt");
            Assert.That(Cbc(new BouncyCastleAes(), CbcKey, CbcIv, CbcCiphertext, encrypt: false),
                Is.EqualTo(CbcPlaintext), "decrypt");
        });
    }

    [Test]
    public void RandomInputs_MatchPlatformAes()
    {
        var random = new Random(1234);

        for (var i = 0; i < 500; i++)
        {
            var key = RandomBytes(random, random.Next(2) == 0 ? 16 : 32);
            var iv = RandomBytes(random, 16);
            var data = RandomBytes(random, 16 * random.Next(1, 17));
            var encrypt = random.Next(2) == 0;

            if (random.Next(2) == 0)
            {
                var block = data.AsSpan(0, 16).ToArray();
                Assert.That(Ecb(new BouncyCastleAes(), key, block, encrypt),
                    Is.EqualTo(Ecb(Aes.Create(), key, block, encrypt)), $"ECB case {i}");
            }
            else
            {
                Assert.That(Cbc(new BouncyCastleAes(), key, iv, data, encrypt),
                    Is.EqualTo(Cbc(Aes.Create(), key, iv, data, encrypt)), $"CBC case {i}");
            }
        }
    }

    [Test]
    public void KeyAndIVChangesAfterCreation_AreUsedByNextTransform()
    {
        // MessageSecureChannel.GenerateMac chains blocks by updating IV and Key on one instance
        var random = new Random(42);
        var firstKey = RandomBytes(random, 16);
        var secondKey = RandomBytes(random, 16);
        var blocks = RandomBytes(random, 48);

        Assert.That(Mac(new BouncyCastleAes(), firstKey, secondKey, blocks),
            Is.EqualTo(Mac(Aes.Create(), firstKey, secondKey, blocks)));
    }

    [Test]
    public void PartialBlock_Throws()
    {
        Assert.Throws<CryptographicException>(() =>
            Cbc(new BouncyCastleAes(), CbcKey, CbcIv, new byte[15], encrypt: true));
    }

    [TestCase(CipherMode.CBC, PaddingMode.PKCS7)]
    [TestCase(CipherMode.CFB, PaddingMode.None)]
    public void UnsupportedModeOrPadding_Throws(CipherMode mode, PaddingMode padding)
    {
        Assert.Throws<NotSupportedException>(() =>
        {
            using var aes = new BouncyCastleAes();
            aes.Key = CbcKey;
            aes.Mode = mode;
            aes.Padding = padding;
            aes.CreateEncryptor().Dispose();
        });
    }

    [Test]
    public void CreateAes_UsesPlatformAesWhereAvailable()
    {
        using var aes = BouncyCastleAes.CreateAes();

        Assert.That(aes, Is.Not.InstanceOf<BouncyCastleAes>());
    }

    private static byte[] Ecb(Aes aes, byte[] key, byte[] block, bool encrypt)
    {
        using (aes)
        {
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.Zeros;
            aes.KeySize = key.Length * 8;
            aes.Key = key;
            using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
            return transform.TransformFinalBlock(block, 0, block.Length);
        }
    }

    private static byte[] Cbc(Aes aes, byte[] key, byte[] iv, byte[] data, bool encrypt)
    {
        using (aes)
        {
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.KeySize = key.Length * 8;
            aes.Key = key;
            aes.IV = iv;
            using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
            return transform.TransformFinalBlock(data, 0, data.Length);
        }
    }

    private static byte[] Mac(Aes aes, byte[] firstKey, byte[] lastKey, byte[] blocks)
    {
        using (aes)
        {
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.KeySize = 128;
            aes.Key = firstKey;
            aes.IV = new byte[16];
            for (var offset = 0; offset < blocks.Length; offset += 16)
            {
                if (offset + 16 == blocks.Length) aes.Key = lastKey;
                using var encryptor = aes.CreateEncryptor();
                aes.IV = encryptor.TransformFinalBlock(blocks, offset, 16);
            }

            return aes.IV;
        }
    }

    private static byte[] RandomBytes(Random random, int count)
    {
        var bytes = new byte[count];
        random.NextBytes(bytes);
        return bytes;
    }
}
