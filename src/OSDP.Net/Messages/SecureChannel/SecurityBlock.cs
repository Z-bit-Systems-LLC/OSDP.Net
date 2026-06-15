using System;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// Contains standard Security Control Block data that can be used when building messages
/// </summary>
public static class SecurityBlock
{
    /// <summary>
    /// Secure channel is established and the
    /// reply message contains a MAC signature but the data field
    /// is unencrypted or not present
    /// </summary>
    public static ReadOnlySpan<byte> CommandMessageWithNoDataSecurity => new byte[]
    {
        0x02,
        (byte)SecurityBlockType.CommandMessageWithNoDataSecurity
    };
    
    /// <summary>
    /// 
    /// </summary>
    public static ReadOnlySpan<byte> ReplyMessageWithNoDataSecurity => new byte[]
    {
        0x02,
        (byte)SecurityBlockType.ReplyMessageWithNoDataSecurity
    };
    
    /// <summary>
    /// 
    /// </summary>
    public static ReadOnlySpan<byte> CommandMessageWithDataSecurity => new byte[]
    {
        0x02,
        (byte)SecurityBlockType.CommandMessageWithDataSecurity
    };
    
    /// <summary>
    ///
    /// </summary>
    public static ReadOnlySpan<byte> ReplyMessageWithDataSecurity => new byte[]
    {
        0x02,
        (byte)SecurityBlockType.ReplyMessageWithDataSecurity
    };

    /// <summary>
    /// SC2 command (ACU -> PD); secure channel established and the command data
    /// field is encrypted using AES-256 GCM (block type 0x27).
    /// </summary>
    public static ReadOnlySpan<byte> CommandMessageWithDataSecurityV2 => new byte[]
    {
        0x02,
        (byte)SecurityBlockType.CommandMessageWithDataSecurityV2
    };

    /// <summary>
    /// SC2 reply (PD -> ACU); secure channel established and the reply data
    /// field is encrypted using AES-256 GCM (block type 0x28).
    /// </summary>
    public static ReadOnlySpan<byte> ReplyMessageWithDataSecurityV2 => new byte[]
    {
        0x02,
        (byte)SecurityBlockType.ReplyMessageWithDataSecurityV2
    };
}