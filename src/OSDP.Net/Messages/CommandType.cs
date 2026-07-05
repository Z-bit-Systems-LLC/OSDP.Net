namespace OSDP.Net.Messages
{
    /// <summary>
    /// Identifies the type of OSDP protocol command (ACU -> PD) message
    /// </summary>
    public enum CommandType : byte
    {
        /// <summary>
        /// osdp_POLL - poll request
        /// </summary>
        Poll = 0x60,

        /// <summary>
        /// osdp_ID - ID report request
        /// </summary>
        IdReport = 0x61,

        /// <summary>
        /// osdp_CAP - peripheral device capabilities request
        /// </summary>
        DeviceCapabilities = 0x62,

        /// <summary>
        /// osdp_LSTAT - local status report request
        /// </summary>
        LocalStatus = 0x64,

        /// <summary>
        /// osdp_ISTAT - input status report request
        /// </summary>
        InputStatus = 0x65,

        /// <summary>
        /// osdp_OSTAT - output status report request
        /// </summary>
        OutputStatus = 0x66,

        /// <summary>
        /// osdp_RSTAT - reader status report request
        /// </summary>
        ReaderStatus = 0x67,

        /// <summary>
        /// osdp_OUT - output control command
        /// </summary>
        OutputControl = 0x68,

        /// <summary>
        /// osdp_LED - Reader LED control command
        /// </summary>
        LEDControl = 0x69,

        /// <summary>
        /// osdp_BUZ - Reader buzzer control command
        /// </summary>
        BuzzerControl = 0x6A,

        /// <summary>
        /// osdp_TEXT - Reader text output command
        /// </summary>
        TextOutput = 0x6B,

        /// <summary>
        /// osdp_COMSET - communication configuration command
        /// </summary>
        CommunicationSet = 0x6E,

        /// <summary>
        /// osdp_BIOREAD - scan and match biometric data
        /// </summary>
        BioRead = 0x73,

        /// <summary>
        /// osdp_BIOMATCH - scan and match biometric template
        /// </summary>
        BioMatch = 0x74,

        /// <summary>
        /// osdp_KEYSET - Encryption key set
        /// </summary>
        KeySet = 0x75,

        /// <summary>
        /// osdp_CHLNG - challenge and secure session initialization request
        /// </summary>
        SessionChallenge = 0x76,

        /// <summary>
        /// osdp_SCRYPT - server's random number and server cryptogram
        /// </summary>
        ServerCryptogram = 0x77,

        /// <summary>
        /// osdp_ACURXSIZE - ACU receive size
        /// </summary>
        MaxReplySize = 0x7B,

        /// <summary>
        /// osdp_FILETRANSFER - file transfer command
        /// </summary>
        FileTransfer = 0x7C,

        /// <summary>
        /// osdp_MFG - manufacturer specific command
        /// </summary>
        ManufacturerSpecific = 0x80,

        /// <summary>
        /// osdp_XWR - extended write data
        /// </summary>
        ExtendedWrite = 0xA1,

        /// <summary>
        /// osdp_ABORT - abort current operation
        /// </summary>
        Abort = 0xA2,

        /// <summary>
        /// osdp_PIVDATA - get PIV data
        /// </summary>
        PivData = 0xA3,

        /// <summary>
        /// osdp_GENAUTH - generate authenticate
        /// </summary>
        GenerateChallenge = 0xA4,

        /// <summary>
        /// osdp_CRAUTH - authenticate challenge
        /// </summary>
        AuthenticateChallenge = 0xA5,

        /// <summary>
        /// osdp_KEEPACTIVE - keep reader active
        /// </summary>
        KeepActive = 0xA7,

        /// <summary>
        /// osdp_PAIR - asymmetric device pairing fragment (experimental extension).
        /// Command code 0xB0 is chosen to avoid the OSDP 3.0 PIV draft's tentative 0xA6-0xAF block;
        /// it is not an SIA-assigned value.
        /// </summary>
        Pair = 0xB0
    }
}