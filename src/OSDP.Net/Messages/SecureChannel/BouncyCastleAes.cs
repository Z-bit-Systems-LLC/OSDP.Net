using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// An <see cref="Aes"/> implementation backed by BouncyCastle, used where the platform has no AES,
/// such as browser WebAssembly. It supports only what secure channel needs: ECB and CBC on whole
/// blocks, with no padding or zero padding.
/// </summary>
internal sealed class BouncyCastleAes : Aes
{
    private const int BlockSizeInBytes = 16;

    private static readonly bool NativeSupported = IsNativeSupported();

    /// <summary>
    /// Forces the BouncyCastle implementation even where native AES is available. For tests only.
    /// </summary>
    internal static bool ForceBouncyCastle { get; set; }

    /// <summary>
    /// Creates the platform's AES implementation, or a BouncyCastle one where the platform has none.
    /// </summary>
    internal static Aes CreateAes() =>
        NativeSupported && !ForceBouncyCastle ? Aes.Create() : new BouncyCastleAes();

    // In browser WebAssembly Aes.Create() succeeds; the first transform throws.
    private static bool IsNativeSupported()
    {
        try
        {
            using var aes = Aes.Create();
            using var transform = aes.CreateEncryptor(new byte[BlockSizeInBytes], new byte[BlockSizeInBytes]);
            return true;
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException or CryptographicException)
        {
            return false;
        }
    }

    // Parameter names match SymmetricAlgorithm's declarations.
    // ReSharper disable InconsistentNaming

    /// <inheritdoc />
    public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV) =>
        CreateTransform(true, rgbKey, rgbIV);

    /// <inheritdoc />
    public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV) =>
        CreateTransform(false, rgbKey, rgbIV);

    // ReSharper restore InconsistentNaming

    /// <inheritdoc />
    public override void GenerateKey() => KeyValue = RandomBytes(KeySizeValue / 8);

    /// <inheritdoc />
    public override void GenerateIV() => IVValue = RandomBytes(BlockSizeInBytes);

    private static byte[] RandomBytes(int count)
    {
        var bytes = new byte[count];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(bytes);
        return bytes;
    }

    private ICryptoTransform CreateTransform(bool encrypt, byte[] key, byte[] iv)
    {
        if (Padding != PaddingMode.None && Padding != PaddingMode.Zeros)
        {
            throw new NotSupportedException($"Padding mode {Padding} is not supported");
        }

        IBlockCipher cipher;
        ICipherParameters parameters = new KeyParameter(key);
        switch (Mode)
        {
            case CipherMode.ECB:
                cipher = new AesEngine();
                break;
            case CipherMode.CBC:
                cipher = new CbcBlockCipher(new AesEngine());
                parameters = new ParametersWithIV(parameters, iv ?? new byte[BlockSizeInBytes]);
                break;
            default:
                throw new NotSupportedException($"Cipher mode {Mode} is not supported");
        }

        cipher.Init(encrypt, parameters);
        return new BlockTransform(cipher);
    }

    /// <summary>
    /// Runs whole blocks through a BouncyCastle block cipher. Secure channel always passes whole
    /// blocks, so with no or zero padding the final block needs no special handling.
    /// </summary>
    private sealed class BlockTransform(IBlockCipher cipher) : ICryptoTransform
    {
        public int InputBlockSize => BlockSizeInBytes;

        public int OutputBlockSize => BlockSizeInBytes;

        public bool CanTransformMultipleBlocks => true;

        public bool CanReuseTransform => false;

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer,
            int outputOffset)
        {
            if (inputCount % BlockSizeInBytes != 0)
            {
                throw new CryptographicException("The input must be a whole number of blocks");
            }

            for (var offset = 0; offset < inputCount; offset += BlockSizeInBytes)
            {
                cipher.ProcessBlock(inputBuffer, inputOffset + offset, outputBuffer, outputOffset + offset);
            }

            return inputCount;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            var output = new byte[inputCount];
            TransformBlock(inputBuffer, inputOffset, inputCount, output, 0);
            return output;
        }

        public void Dispose()
        {
        }
    }
}
