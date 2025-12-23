using System;
using System.Linq;
using System.Security.Cryptography;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// Security context used within MessageSecureChannel
/// 
/// This state data is placed into its own class to facilitate use cases where multiple channels
/// (i.e. one for incoming packets; one for outgoing) have to share the same security state.
/// </summary>
public class SecurityContext
{
    private byte[] _securityKey = DefaultKey;

    /// <summary>
    /// Represents the default key used in the security context.
    /// </summary>
    internal static readonly byte[] DefaultKey = "0123456789:;<=>?"u8.ToArray();

    /// <summary>
    /// Represents a security context for OSDP secure channel.
    /// </summary>
    public SecurityContext(byte[] securityKey = null) => Reset(securityKey);

    /// <summary>
    /// Resets secure channel back to its initial state
    /// </summary>
    /// <param name="securityKey">
    /// Can be optionally passed in to change the security key. If null, existing
    /// key (or default one, SCBK-D) will be used if one was never specified
    /// currently used
    /// </param>
    public void Reset(byte[] securityKey = null)
    {
        if (securityKey != null)
        {
            _securityKey = securityKey;
        }

        IsUsingDefaultKey = !(_securityKey != null && !_securityKey.SequenceEqual(DefaultKey));
        IsInitialized = false;
        IsSecurityEstablished = false;
        new Random().NextBytes(ServerRandomNumber);
    }

    /// <summary>
    /// A flag indicating whether or not channel security has been established
    /// </summary>
    internal bool IsSecurityEstablished { get; set; }

    /// <summary>
    /// Symmertric message encryption key established by the secure channel handshake
    /// </summary>
    internal byte[] Enc { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// S-MAC1 value
    /// </summary>
    internal byte[] SMac1 { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// S-MAC2 value
    /// </summary>
    internal byte[] SMac2 { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// R-MAC value
    /// </summary>
    internal byte[] RMac { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// C-MAC value
    /// </summary>
    internal byte[] CMac { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Represents the server cryptogram used in the secure channel protocol.
    /// </summary>
    internal byte[] ServerCryptogram { get; private set; }

    /// <summary>
    /// Represents the server's random number in the secure channel.
    /// </summary>
    /// <remarks>
    /// The server's random number is used in the initialization of the secure channel
    /// to establish a secure connection between the client and the server.
    /// </remarks>
    /// <example>
    /// serverRandomNumber.CreateNewRandomNumber();
    /// </example>
    internal byte[] ServerRandomNumber { get; } = new byte[8];

    /// <summary>
    /// Gets a value indicating whether the security context is initialized.
    /// </summary>
    /// <value>
    /// <c>true</c> if the security context is initialized; otherwise, <c>false</c>.
    /// </value>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the security context is using the default key.
    /// </summary>
    /// <value>
    /// <c>true</c> if the security context is using the default key; otherwise, <c>false</c>.
    /// </value>
    public bool IsUsingDefaultKey { get; private set; }

    /// <summary>
    /// Creates a new instance of AES cypher
    /// </summary>
    /// <param name="isForSessionSetup">We use the cypher in two major use cases: 
    /// session setup and message data encryption. Depending on the case, it has 
    /// to be initialized slightly differently so this flag indicates which case 
    /// is currently needed.</param>
    /// <param name="key"></param>
    /// <returns>Cypher instance</returns>
    internal Aes CreateCypher(bool isForSessionSetup, byte[] key = null)
    {
        var crypto = Aes.Create();
        if (crypto == null)
        {
            throw new Exception("Unable to create AES algorithm");
        }

        if (!isForSessionSetup)
        {
            crypto.Mode = CipherMode.CBC;
            crypto.Padding = PaddingMode.None;
        }
        else
        {
            crypto.Mode = CipherMode.ECB;
            crypto.Padding = PaddingMode.Zeros;
        }
        
        crypto.KeySize = 128;
        crypto.BlockSize = 128;
        crypto.Key = key ?? _securityKey;

        return crypto;
    }

    /// <summary>
    /// Generates a cryptographic key by encrypting the input data using AES.
    /// Specialized for OSDP secure channel key derivation.
    /// </summary>
    /// <param name="aes">AES crypto instance configured for key generation</param>
    /// <param name="input">Input bytes to be used for key generation. Length must be 16 bytes or less.</param>
    /// <returns>16-byte encrypted key</returns>
    internal static byte[] GenerateKey(Aes aes, byte[] input)
    {
        var buffer = new byte[16];
        input.CopyTo(buffer, 0);

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Generates a cryptographic key by concatenating two byte arrays and encrypting with AES.
    /// Specialized for OSDP secure channel key derivation.
    /// </summary>
    /// <param name="aes">AES crypto instance configured for key generation</param>
    /// <param name="input1">First input byte array</param>
    /// <param name="input2">Second input byte array</param>
    /// <returns>16-byte encrypted key</returns>
    /// <remarks>The total length of input1 + input2 must be 16 bytes or less.</remarks>
    internal static byte[] GenerateKey(Aes aes, byte[] input1, byte[] input2)
    {
        var buffer = new byte[16];
        input1.CopyTo(buffer, 0);
        input2.CopyTo(buffer, input1.Length);

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Initializes the Access Control Unit (ACU) with the provided client random number and cryptogram.
    /// </summary>
    /// <param name="clientRandomNumber">The client random number.</param>
    /// <param name="clientCryptogram">The client cryptogram.</param>
    /// <exception cref="Exception">Thrown if the client cryptogram is invalid.</exception>
    internal void InitializeACU(byte[] clientRandomNumber, byte[] clientCryptogram)
    {
        using var keyAlgorithm = CreateCypher(true);
        Enc = GenerateKey(keyAlgorithm, new byte[]
        {
            0x01, 0x82, ServerRandomNumber[0], ServerRandomNumber[1], ServerRandomNumber[2],
            ServerRandomNumber[3], ServerRandomNumber[4], ServerRandomNumber[5]
        });

        using var serverCypher  = CreateCypher(true, Enc);
        if (!clientCryptogram.SequenceEqual(GenerateKey(serverCypher, 
                ServerRandomNumber, clientRandomNumber)))
        {
            throw new Exception("Invalid client cryptogram");
        }

        SMac1 = GenerateKey(keyAlgorithm,
            new byte[]
            {
                0x01, 0x01, ServerRandomNumber[0], ServerRandomNumber[1], ServerRandomNumber[2],
                ServerRandomNumber[3], ServerRandomNumber[4], ServerRandomNumber[5]
            });
        SMac2 = GenerateKey(keyAlgorithm,
            new byte[]
            {
                0x01, 0x02, ServerRandomNumber[0], ServerRandomNumber[1], ServerRandomNumber[2],
                ServerRandomNumber[3], ServerRandomNumber[4], ServerRandomNumber[5]
            });
                
        ServerCryptogram = GenerateKey(serverCypher, clientRandomNumber, ServerRandomNumber);
        IsInitialized = true;
    }

    internal void Establish(byte[] rmac)
    {
        RMac = rmac;
        IsSecurityEstablished = true;
    }
}