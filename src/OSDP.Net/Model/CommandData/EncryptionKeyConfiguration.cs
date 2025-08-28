using System;
using System.Collections.Generic;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Command data to set the encryption key configuration on a PD.
    /// </summary>
    public class EncryptionKeyConfiguration : CommandData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionKeyConfiguration"/> class.
        /// </summary>
        /// <param name="keyType">Type of the key.</param>
        /// <param name="keyData">16 bytes of key data.</param>
        public EncryptionKeyConfiguration(KeyType keyType, byte[] keyData)
        {
            KeyType = keyType;
            KeyData = keyData;
        }

        /// <summary>
        /// Gets the type of the key.
        /// </summary>
        public KeyType KeyType { get; }

        /// <summary>
        /// Gets 16 bytes of key data.
        /// </summary>
        public byte[] KeyData { get; }

        /// <inheritdoc />
        public override CommandType CommandType => CommandType.KeySet;

        /// <inheritdoc />
        public override byte Code => (byte)CommandType;
        
        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;
 
        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new List<byte>
            {
                (byte)KeyType,
                (byte)KeyData.Length
            };
            data.AddRange(KeyData);

            return data.ToArray();
        }
        
        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of EncryptionKeyConfiguration representing the message payload</returns>
        public static EncryptionKeyConfiguration ParseData(ReadOnlySpan<byte> data)
        {
            byte keyLength = data[1];
            
            return new EncryptionKeyConfiguration((KeyType)data[0], data.Slice(2, keyLength).ToArray());
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"  Key Type: {KeyType}");
            sb.AppendLine($"Key Length: {KeyData.Length} bytes");
            sb.AppendLine($"  Key Data: {BitConverter.ToString(KeyData)}");
            return sb.ToString();
        }
    }
}