namespace OSDP.Net.Messages
{
    /// <summary>
    /// Identifies the type of OSDP protocol reply (PD -> ACU) message
    /// </summary>
    public enum ReplyType : byte
    {
        /// <summary>
        /// osdp_ACK - general acknowledge; nothing to report
        /// </summary>
        Ack = 0x40,

        /// <summary>
        /// osdp_NAK - acknowledge; error response
        /// </summary>
        Nak = 0x41,

        /// <summary>
        /// osdp_PDID - device identification report
        /// </summary>
        PdIdReport = 0x45,

        /// <summary>
        /// osdp_PDCAP - device capabilities report
        /// </summary>
        PdCapabilitiesReport = 0x46,

        /// <summary>
        /// osdp_LSTATR - local status report
        /// </summary>
        LocalStatusReport = 0x48,

        /// <summary>
        /// osdp_ISTATR - input status report
        /// </summary>
        InputStatusReport = 0x49,

        /// <summary>
        /// osdp_OSTATR - output status report
        /// </summary>
        OutputStatusReport = 0x4A,

        /// <summary>
        /// osdp_RSTATR - reader tamper status report
        /// </summary>
        ReaderStatusReport = 0x4B,

        /// <summary>
        /// osdp_RAW - card data report; raw bits array
        /// </summary>
        RawReaderData = 0x50,

        /// <summary>
        /// osdp_FMT - card data report; character array
        /// </summary>
        FormattedReaderData = 0x51,

        /// <summary>
        /// osdp_KEYPAD - keypad data report
        /// </summary>
        KeypadData = 0x53,

        /// <summary>
        /// osdp_COM - communication configuration report
        /// </summary>
        PdCommunicationsConfigurationReport = 0x54,

        /// <summary>
        /// osdp_BIOREADR - scan and send biometric data
        /// </summary>
        BiometricData = 0x57,

        /// <summary>
        /// osdp_BIOMATCHR - scan and match biometric template
        /// </summary>
        BiometricMatchResult = 0x58,

        /// <summary>
        /// osdp_EXT_PDID - extended device identification report
        /// </summary>
        ExtendedPdIdReport = 0x59,

        /// <summary>
        /// osdp_CCRYPT - client's ID and client's random number
        /// </summary>
        CrypticData = 0x76,

        /// <summary>
        /// osdp_RMAC_I - client cryptogram packet and the initial R-MAC
        /// </summary>
        InitialRMac = 0x78,

        /// <summary>
        /// osdp_BUSY - PD busy reply
        /// </summary>
        Busy = 0x79,

        /// <summary>
        /// osdp_FTSTAT - file transfer status
        /// </summary>
        FileTransferStatus = 0x7A,

        /// <summary>
        /// osdp_PIVDATAR - PIV data reply
        /// </summary>
        PIVData = 0x80,

        /// <summary>
        /// osdp_CRAUTHR - response to challenge
        /// </summary>
        ResponseToChallenge = 0x82,

        /// <summary>
        /// osdp_osdp_MFGSTATR - manufacturer specific status reply
        /// </summary>
        ManufactureSpecific = 0x90,

        /// <summary>
        /// osdp_XRD - extended read reply
        /// </summary>
        ExtendedRead = 0xB1
    }
}