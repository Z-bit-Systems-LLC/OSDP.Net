namespace OSDP.Net.Messages.SecureChannel
{
    /// <summary>
    /// Security Block Type values as defined by OSDP protocol
    /// </summary>
    public enum SecurityBlockType : byte
    {
        /// <summary>
        /// SCS_11 - Sent along with osdp_CHLNG command when ACU initiates
        /// a secure channel connection
        /// </summary>
        BeginNewSecureConnectionSequence = 0x11,

        /// <summary>
        /// SCS_12 - Sent along with osdp_CCRYPT message in response to
        /// osdp_CHLNG command
        /// </summary>
        SecureConnectionSequenceStep2 = 0x12,

        /// <summary>
        /// SCS_13 - Sent along with osdp_SCRYPT command as the third step
        /// of secure channel handshake
        /// </summary>
        SecureConnectionSequenceStep3 = 0x13,

        /// <summary>
        /// SCS_14 - Sent along with osdp_RMAC_I message in response to
        /// ACU's osdp_SCRYPT message. This is the final step in secure
        /// channel handshake. Once this is received by ACU, the secure
        /// channel is established on both sides
        /// </summary>
        SecureConnectionSequenceStep4 = 0x14,

        /// <summary>
        /// SCS_15 - ACU -> PD; secure channel is established and the 
        /// command message contains a MAC signature but the data field
        /// is unencrypted
        /// </summary>
        CommandMessageWithNoDataSecurity = 0x15,

        /// <summary>
        /// SCS_16 - PD -> ACU; secure channel is established and the
        /// reply message contains a MAC signature but the data field
        /// is unencrypted
        /// </summary>
        ReplyMessageWithNoDataSecurity = 0x16,

        /// <summary>
        /// SCS_17 - ACU -> PD; secure channel is established. The command
        /// message contains a MAC signature and the data field is 
        /// encrypted using the S-ENC key
        /// </summary>
        CommandMessageWithDataSecurity = 0x17,

        /// <summary>
        /// SCS_18 - PD -> ACU; secure channel is established. The reply
        /// message contains a MAC signature and the data field is
        /// encrypted using the S-ENC key
        /// </summary>
        ReplyMessageWithDataSecurity = 0x18
    }
}
