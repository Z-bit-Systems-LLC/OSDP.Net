using OSDP.Net.Messages;
using System;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// Represents a reply to osdp_CHLNG command and contain's PD's ID, 
    /// PD-generated random number and PD cryptogram
    /// </summary>
    internal class ChallengeResponse : PayloadData
    {
        /// <summary>
        /// Create a new instance of the ChallengeResponse
        /// </summary>
        /// <param name="cUID">PD(Client) ID to send back to ACU</param>
        /// <param name="rndB">PD random number</param>
        /// <param name="cryptogram">PD Cryptogram</param>
        /// <param name="isUsingDefaultKey"></param>
        public ChallengeResponse(byte[] cUID, byte[] rndB, byte[] cryptogram, bool isUsingDefaultKey)
        {
            ClientUID = cUID;
            RndB = rndB;
            Cryptogram = cryptogram;
            IsUsingDefaultKey = isUsingDefaultKey;
        }

        /// <summary>
        /// Client Unique ID
        /// </summary>
        public byte[] ClientUID {get; }

        /// <summary>
        /// Rnd.B as defined in OSDP.NET spec
        /// </summary>
        public byte[] RndB { get; }

        /// <summary>
        /// Client (PD-side) cryptogram as defined in OSDP.NET spec
        /// </summary>
        public byte[] Cryptogram { get; }

        public bool IsUsingDefaultKey { get; }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <param name="isDefaultKey"></param>
        /// <returns>An instance of ChallengeResponse representing the message payload</returns>
        public static ChallengeResponse ParseData(ReadOnlySpan<byte> data, bool isDefaultKey)
        {
            // SC2: cUID(8) + RndB(16) + cryptogram(32) = 56 bytes
            if (data.Length == 56)
            {
                return new ChallengeResponse(
                    data.Slice(0, 8).ToArray(),
                    data.Slice(8, 16).ToArray(),
                    data.Slice(24, 32).ToArray(),
                    false
                );
            }

            // SC1: cUID(8) + RndB(8) + cryptogram(16) = 32 bytes
            if (data.Length != 32)
            {
                throw new InvalidPayloadException($"Challenge response must be 32 or 56 bytes, received {data.Length}");
            }

            return new ChallengeResponse(
                data.Slice(0, 8).ToArray(),
                data.Slice(8, 8).ToArray(),
                data.Slice(16, 16).ToArray(),
                isDefaultKey
            );
        }

        /// <inheritdoc/>
        public override byte[] BuildData()
        {
            var payload = new byte[ClientUID.Length + RndB.Length + Cryptogram.Length];
            ClientUID.CopyTo(payload, 0);
            RndB.CopyTo(payload, ClientUID.Length);
            Cryptogram.CopyTo(payload, ClientUID.Length + RndB.Length);

            return payload;
        }
        
        /// <inheritdoc/>
        public override byte Code => (byte)ReplyType.CrypticData;
        
        /// <inheritdoc />
        public override bool IsSecurityInitialization => true;
        
        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock()
        {
            byte scbData = RndB.Length == 16
                ? (byte)0x02
                : (byte)(IsUsingDefaultKey ? 0x00 : 0x01);

            return new byte[]
            {
                0x03,
                (byte)SecurityBlockType.SecureConnectionSequenceStep2,
                scbData
            };
        }
    }
}
