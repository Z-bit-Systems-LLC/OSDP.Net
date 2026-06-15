namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Key Type
    /// </summary>
    public enum KeyType
    {
        /// <summary>
        /// The AES-128 secure channel base key (SC1, 16 bytes).
        /// </summary>
        SecureChannelBaseKey = 0x01,

        /// <summary>
        /// The AES-256 secure channel base key (OSDP-SC2, 32 bytes). See the OSDP-SC2 Annex, Table 2.
        /// </summary>
        SecureChannelBaseKeyAes256 = 0x02
    }
}