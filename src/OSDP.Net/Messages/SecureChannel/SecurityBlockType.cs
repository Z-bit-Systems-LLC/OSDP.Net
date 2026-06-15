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
        ReplyMessageWithDataSecurity = 0x18,

        // OSDP-SC2 (AES-256 GCM) block types. The OSDP-SC2 Annex assigns SC2 its own
        // distinct 0x2X range so SC2 traffic is identifiable by block type alone rather
        // than relying on the SCB data byte. The SCB data byte remains 0x02 for all SC2 steps.

        /// <summary>
        /// SCS_21 - SC2; sent with osdp_CHLNG when the ACU begins a new SC2
        /// secure channel connection sequence
        /// </summary>
        BeginNewSecureConnectionSequenceV2 = 0x21,

        /// <summary>
        /// SCS_22 - SC2; sent with osdp_CCRYPT in response to osdp_CHLNG
        /// </summary>
        SecureConnectionSequenceStep2V2 = 0x22,

        /// <summary>
        /// SCS_23 - SC2; sent with osdp_SCRYPT as the third step of the SC2 handshake
        /// </summary>
        SecureConnectionSequenceStep3V2 = 0x23,

        /// <summary>
        /// SCS_24 - SC2; sent with osdp_RMAC_I as the final step of the SC2 handshake
        /// </summary>
        SecureConnectionSequenceStep4V2 = 0x24,

        /// <summary>
        /// SCS_25 - SC2; ACU -> PD; secure channel established, command carries a MAC
        /// (GCM tag) but the data field is unencrypted (development/test use only)
        /// </summary>
        CommandMessageWithNoDataSecurityV2 = 0x25,

        /// <summary>
        /// SCS_26 - SC2; PD -> ACU; secure channel established, reply carries a MAC
        /// (GCM tag) but the data field is unencrypted (development/test use only)
        /// </summary>
        ReplyMessageWithNoDataSecurityV2 = 0x26,

        /// <summary>
        /// SCS_27 - SC2; ACU -> PD; secure channel established, command data is
        /// encrypted with S-ENC using AES-256 GCM
        /// </summary>
        CommandMessageWithDataSecurityV2 = 0x27,

        /// <summary>
        /// SCS_28 - SC2; PD -> ACU; secure channel established, reply data is
        /// encrypted with S-ENC using AES-256 GCM
        /// </summary>
        ReplyMessageWithDataSecurityV2 = 0x28
    }
}
