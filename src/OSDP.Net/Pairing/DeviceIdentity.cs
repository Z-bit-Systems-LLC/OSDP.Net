using System;

namespace OSDP.Net.Pairing;

/// <summary>
/// The manufacturer/model/serial triple that identifies a device, following the
/// IEEE 802.1AR IDevID subject convention. Carried in the subject field of a
/// <see cref="C509Certificate"/>.
/// </summary>
public sealed class DeviceIdentity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceIdentity"/> class.
    /// </summary>
    /// <param name="manufacturer">The device manufacturer name.</param>
    /// <param name="model">The device model name or number.</param>
    /// <param name="serialNumber">The device serial number.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public DeviceIdentity(string manufacturer, string model, string serialNumber)
    {
        Manufacturer = manufacturer ?? throw new ArgumentNullException(nameof(manufacturer));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        SerialNumber = serialNumber ?? throw new ArgumentNullException(nameof(serialNumber));
    }

    /// <summary>Gets the device manufacturer name.</summary>
    public string Manufacturer { get; }

    /// <summary>Gets the device model name or number.</summary>
    public string Model { get; }

    /// <summary>Gets the device serial number.</summary>
    public string SerialNumber { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Manufacturer} {Model} (S/N {SerialNumber})";
}
