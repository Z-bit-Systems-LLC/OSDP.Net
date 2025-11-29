using System;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// Definitions of the capability function codes.
    /// </summary>
    public enum CapabilityFunction
    {
        /// <summary>Capability is not defined.</summary>
        Unknown = 0,

        /// <summary>
        ///   <para>The compliance level of contact status monitoring.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>01 - PD monitors and reports the state of the circuit without any supervision. The PD encodes the circuit status per its default interpretation of contact state to active/inactive status.</para>
        ///   <para>02 - Like 1, plus: The PD accepts configuration of the encoding of the open/closed circuit status to the reported active/inactive status. (User may configure each circuit as "normally closed" or "normally open".)</para>
        ///   <para>03 - Like 2, plus: PD supports supervised monitoring. The operating mode for each circuit is determined by configuration settings.</para>
        ///   <para>04 - Like 3, plus: the PD supports custom End-Of-Line settings within the Manufacturer's guidelines.</para>
        ///   <para />
        ///   <para>Number Of: The number of Inputs.</para>
        /// </summary>
        ContactStatusMonitoring = 1,

        /// <summary>
        ///   <para>The compliance level of output control.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>01 - The PD is able to activate and deactivate the Output per direct command from the ACU.</para>
        ///   <para>02 - Like 1, plus: The PD is able to accept configuration of the Output driver to set the inactive state of the Output. The typical state of an inactive Output is the state of the Output when no power is applied to the PD and the Output device (relay) is not energized. The inverted drive setting causes the PD to energize the Output during the inactive state and de- energize the Output during the active state.</para>
        ///   <para>03 - Like 2, plus: PD supports supervised monitoring. The operating mode for each circuit is determined by configuration settings.</para>
        ///   <para>04 - Like 3, plus: the PD supports custom End-Of-Line settings within the Manufacturer's guidelines.</para>
        ///   <para />
        ///   <para>Number Of: The number of Outputs.</para>
        /// </summary>
        OutputControl = 2,

        /// <summary>
        ///   <para>The form card data format presented to the control panel.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>01 - The PD sends card data to the ACU as array of bits, not exceeding 1024 bits.</para>
        ///   <para>02 - The PD sends card data to the ACU as array of BCD characters, not exceeding 256 characters.</para>
        ///   <para>03 - The PD can send card data to the ACU as array of bits, or as an array of BCD characters.</para>
        ///   <para />
        ///   <para>Number Of: N/A, set to 0.</para>
        /// </summary>
        CardDataFormat = 3,

        /// <summary>
        ///   <para>The compliance level of reader LEDs.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>01 - The PD support on/off control only.</para>
        ///   <para>02 - The PD supports timed commands.</para>
        ///   <para>03 - Like 2, plus bi-color LEDs.</para>
        ///   <para>04 - Like 2, plus tri-color LEDs.</para>
        ///   <para />
        ///   <para>Number Of: The number of LEDs per reader.</para>
        /// </summary>
        ReaderLEDControl = 4,

        /// <summary>
        ///   <para>The compliance level of reader audio output.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>01 - The PD supports on/off control only.</para>
        ///   <para>02 - The PD supports timed commands.</para>
        ///   <para />
        ///   <para>Number Of: This field is ignored.</para>
        /// </summary>
        ReaderAudibleOutput = 5,

        /// <summary>
        ///   <para>The compliance level of reader text output.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>00 - The PD has no text display support.</para>
        ///   <para>01 - The PD supports 1 row of 16 characters.</para>
        ///   <para>02 - The PD supports 2 rows of 16 characters.</para>
        ///   <para>03 - The PD supports 4 rows of 16 characters.</para>
        ///   <para />
        ///   <para>Number Of: Number of textual displays per reader.</para>
        /// </summary>
        ReaderTextOutput = 6,

        [Obsolete("This capability function is obsolete.", false)]
#pragma warning disable CS1591
        TimeKeeping = 7,
#pragma warning restore CS1591

        /// <summary>
        ///   <para>Check character support for the PD.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>00 - The PD does not support CRC-16, only checksum mode.</para>
        ///   <para>01 -The PD supports the 16-bit CRC-16 mode.</para>
        ///   <para />
        ///   <para>Number Of: N/A, set to 0.</para>
        /// </summary>
        CheckCharacterSupport = 8,

        /// <summary>
        ///   <para>Secure communications support for the PD.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>Bit 0 (mask 0x01) - AES128 support.</para>
        ///   <para />
        ///   <para>Number Of: (Bit-0) default AES128 key.</para>
        /// </summary>
        CommunicationSecurity = 9,

        /// <summary>
        ///   <para>Maximum size single message the PD can receive.</para>
        ///   <para />
        ///   <para>Compliance Levels: LSB of the buffer size.</para>
        ///   <para />
        ///   <para>Number Of: MSB of the buffer size.</para>
        /// </summary>
        ReceiveBufferSize = 10,

        /// <summary>
        ///   <para>Maximum size multi-part message  the PD can handle.</para>
        ///   <para />
        ///   <para>Compliance Levels: LSB of the combined buffer size.</para>
        ///   <para />
        ///   <para>Number Of: MSB of the combined buffer size.</para>
        /// </summary>
        LargestCombinedMessageSize = 11,

        /// <summary>
        ///   <para>Smart card communication support for the PD.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>Bit 0 (mask 0x01) - PD supports transparent reader mode.</para>
        ///   <para>Bit 1 (mask 0x02) - PD supports extended packet mode.</para>
        ///   <para />
        ///   <para>Number Of: N/A, set to 0.</para>
        /// </summary>
        SmartCardSupport = 12,

        /// <summary>
        ///   <para>Number of readers present on PD.</para>
        ///   <para />
        ///   <para>Compliance Levels: Must be zero.</para>
        ///   <para />
        ///   <para>Number Of: Indicates the number of attached downstream readers.</para>
        /// </summary>
        Readers = 13,

        /// <summary>
        ///   <para>The compliance level of reader biometric input.</para>
        ///   <para/>
        ///   <para>Compliance Levels:</para>
        ///   <para>0 - No Biometric.</para>
        ///   <para>1 - Fingerprint, Template 1.</para>
        ///   <para>2 - Fingerprint, Template 2.</para>
        ///   <para>3 - Iris, Template 1.</para>
        ///   <para/>
        ///   <para>Number Of: N/A, set to 0.</para>
        /// </summary>
        Biometrics = 14,

        /// <summary>
        ///   <para>Secure PIN entry support for the PD.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>0 - Does not support SPE.</para>
        ///   <para>1 - Supports SPE.</para>
        ///   <para />
        ///   <para>Number Of: N/A, set to 0.</para>
        /// </summary>
        SecurePINEntry = 15,

        /// <summary>
        ///   <para>Version of OSDP support by the PD.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>0 - Unspecified</para>
        ///   <para>1 - IEC 60839-11-5</para>.
        ///   <para>2 - SIA OSDP 2.2</para>
        ///   <para />
        ///   <para>Number Of: N/A, set to 0.</para>
        /// </summary>
        OSDPVersion = 16,

        /// <summary>
        ///   <para>Extended ID response support for the PD.</para>
        ///   <para />
        ///   <para>Compliance Levels:</para>
        ///   <para>0x00 - Capability not supported.</para>
        ///   <para>0x01 - Extended ID response is supported.</para>
        ///   <para />
        ///   <para>Number Of: Must be 0x00.</para>
        /// </summary>
        ExtendedIdResponse = 17
    }
}