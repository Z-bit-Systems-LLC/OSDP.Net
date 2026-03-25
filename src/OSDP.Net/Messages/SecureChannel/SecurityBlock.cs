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
}