namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// Baud Rate identifiers defined by the OSDP Line Quality Test Procedure, section 3.4.
    /// </summary>
    /// <remarks>
    /// Identifiers 0x00 through 0x05 are the six rates enumerated in OSDP section 5.2, of which a
    /// conforming device need only support one. <see cref="Baud460800"/> is not an OSDP-defined
    /// rate; it is reserved by the line quality specification because it is in common use on short
    /// runs, and is excluded from <see cref="LineQualityProtocol.DefaultBaudRates"/> by default.
    /// </remarks>
    public enum LineQualityBaudRate : byte
    {
        /// <summary>9600 baud. Baseline rate, and the rate a responder falls back to.</summary>
        Baud9600 = 0x00,

        /// <summary>19200 baud.</summary>
        Baud19200 = 0x01,

        /// <summary>38400 baud.</summary>
        Baud38400 = 0x02,

        /// <summary>57600 baud.</summary>
        Baud57600 = 0x03,

        /// <summary>115200 baud.</summary>
        Baud115200 = 0x04,

        /// <summary>230400 baud, the highest rate named by OSDP section 5.2.</summary>
        Baud230400 = 0x05,

        /// <summary>460800 baud. An extension to OSDP, not part of the standard rate list.</summary>
        Baud460800 = 0x06
    }

    /// <summary>
    /// Status codes returned in an Echo Response, per section 3.7 of the Line Quality Test Procedure.
    /// </summary>
    public enum EchoStatus : byte
    {
        /// <summary>Pattern received and echoed correctly.</summary>
        Success = 0x00,

        /// <summary>The requested Pattern ID is not recognized by the responder.</summary>
        UnsupportedPattern = 0x01,

        /// <summary>The requested payload length exceeds the responder's receive buffer.</summary>
        LengthError = 0x02,

        /// <summary>The responder is temporarily unable to process the request.</summary>
        Busy = 0x03
    }

    /// <summary>
    /// Status codes returned in a Baud Rate Change Acknowledgment, per section 3.7 of the
    /// Line Quality Test Procedure.
    /// </summary>
    public enum BaudRateChangeStatus : byte
    {
        /// <summary>The responder will switch to the requested rate after sending this reply.</summary>
        Success = 0x00,

        /// <summary>The requested Baud Rate ID is not supported by the responder.</summary>
        UnsupportedRate = 0x01,

        /// <summary>The responder recognized the rate but could not switch to it.</summary>
        SwitchFailed = 0x02
    }

    /// <summary>
    /// Test profiles from section 3.10 of the Line Quality Test Procedure. The profile sets the
    /// number of iterations per pattern/size combination, which in turn determines the smallest
    /// packet loss rate the run is capable of detecting.
    /// </summary>
    public enum TestProfile
    {
        /// <summary>
        /// 10 iterations per combination, 160 packets per baud rate. Detects loss above roughly
        /// 1.9%. Fast enough for interactive use, but cannot substantiate a 99.9% success claim.
        /// </summary>
        Screening,

        /// <summary>
        /// 60 iterations per combination, 960 packets per baud rate. Detects loss above roughly
        /// 0.31%. The default for a commissioning report.
        /// </summary>
        Qualification,

        /// <summary>
        /// 200 iterations per combination, 3200 packets per baud rate. Detects loss above roughly
        /// 0.094%, the only profile that can substantiate the 99.9% success criterion. Normally
        /// run once at the rate selected for the installation.
        /// </summary>
        Extended
    }
}
