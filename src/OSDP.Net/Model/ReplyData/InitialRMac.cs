using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// Represents the payload of osdp_RMAC_I reply
    /// </summary>
    internal class InitialRMac : PayloadData
    {
        /// <summary>
        /// Creates a new instance of InitialRMac
        /// </summary>
        /// <param name="rmac">Initial R-MAC value to send back to the ACU</param>
        /// <param name="serverCryptogramAccepted">Flag indicating whether or not 
        /// server cryptogram was accepted by the PD</param>
        public InitialRMac(byte[] rmac, bool serverCryptogramAccepted)
        {
            RMac = rmac;
            ServerCryptogramAccepted = serverCryptogramAccepted;
        }

        /// <summary>
        /// Initial R-MAC to be used as a seed for MAC signing of the next
        /// command message
        /// </summary>
        public byte[] RMac { get; }
        
        public bool ServerCryptogramAccepted { get; }

        /// <inheritdoc/>
        public override byte Code => (byte)ReplyType.InitialRMac;
        
        /// <inheritdoc />
        public override bool IsSecurityInitialization => true;
        
        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock()
        {
            // osdp_RMAC_I carries SCS_14. Per the OSDP spec (section D.1.3.4) the data byte is 0x01
            // when the server cryptogram was accepted (secure channel established); 0xFF signals rejection.
            return new byte[]
            {
                0x03,
                (byte)SecurityBlockType.SecureConnectionSequenceStep4,
                (byte)(ServerCryptogramAccepted ? 0x01 : 0xff)
            };
        }

        /// <inheritdoc/>
        public override byte[] BuildData()
        {
            return RMac;
        }
    }
}
