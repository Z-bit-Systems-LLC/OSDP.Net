using System;

namespace OSDP.Net.Model;

/// <summary>
/// Represents the Client Unique Identifier (cUID) used during an OSDP secure channel establishment.
/// The cUID is an 8-byte value composed of the vendor code and serial number.
/// </summary>
public readonly struct ClientIdentification
{
    /// <summary>
    /// Vendor code assigned by the Security Industry Association (SIA).
    /// Must be exactly 3 bytes.
    /// </summary>
    public byte[] VendorCode { get; }

    /// <summary>
    /// Device serial number. Stored as a 32-bit unsigned integer (4 bytes).
    /// </summary>
    public uint SerialNumber { get; }

    /// <summary>
    /// Creates a new ClientIdentification instance.
    /// </summary>
    /// <param name="vendorCode">Vendor code (must be exactly 3 bytes)</param>
    /// <param name="serialNumber">Device serial number</param>
    /// <exception cref="ArgumentNullException">Thrown when vendorCode is null</exception>
    /// <exception cref="ArgumentException">Thrown when vendorCode is not exactly 3 bytes</exception>
    public ClientIdentification(byte[] vendorCode, uint serialNumber)
    {
        if (vendorCode == null)
            throw new ArgumentNullException(nameof(vendorCode));
        if (vendorCode.Length != 3)
            throw new ArgumentException("Vendor code must be exactly 3 bytes", nameof(vendorCode));

        VendorCode = vendorCode;
        SerialNumber = serialNumber;
    }

    /// <summary>
    /// Converts the client identification to an 8-byte array for use in the secure channel protocol.
    /// Format: Vendor Code (3 bytes) + Serial Number (4 bytes, little-endian) + Padding (1 byte)
    /// </summary>
    /// <returns>8-byte array representing the cUID</returns>
    public byte[] ToBytes()
    {
        var result = new byte[8];

        // Copy vendor code (3 bytes)
        VendorCode.CopyTo(result, 0);

        // Copy thy serial number as little-endian (4 bytes)
        result[3] = (byte)(SerialNumber & 0xFF);
        result[4] = (byte)((SerialNumber >> 8) & 0xFF);
        result[5] = (byte)((SerialNumber >> 16) & 0xFF);
        result[6] = (byte)((SerialNumber >> 24) & 0xFF);

        // Byte 7 is padding (already 0)

        return result;
    }

    /// <summary>
    /// Returns a string representation of the client identification.
    /// </summary>
    public override string ToString()
    {
        return $"VendorCode: {BitConverter.ToString(VendorCode)}, SerialNumber: {SerialNumber}";
    }
}
