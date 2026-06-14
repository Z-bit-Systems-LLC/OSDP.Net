# PDConsole Settings

PDConsole is a virtual OSDP Peripheral Device (PD) simulator. Its behavior is driven by a JSON
configuration file, `appsettings.json`, located next to the executable (the application's working
directory).

## How settings are loaded and saved

- On startup PDConsole reads **`appsettings.json`** from the working directory.
- If the file does **not** exist, it is created automatically with the built-in defaults.
- If the file exists but fails to parse, PDConsole logs the error and falls back to defaults.
- Property names are matched **case-insensitively**, and all enum values are written/read as
  **strings** (e.g. `"Serial"`, `"Install"`, `"ContactStatusMonitoring"`).
- The file can also be loaded/saved at runtime from the UI. Saving rewrites the same JSON with
  indentation.
- **Auto-update on `osdp_KEYSET`:** when an ACU sets a new secure channel key, PDConsole writes the
  new key to `Security.SecureChannelKey`, switches `Security.SecureChannelMode` to `Secure`, and
  persists the file so subsequent runs use the new key. (No change is made if the mode was
  `ClearText`.)

The file is organized into four sections — `Connection`, `Device`, `Security`, `Simulation` — plus
two top-level switches.

---

## Top-level settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `EnableLogging` | bool | `true` | Enables application/diagnostic logging. |
| `EnableTracing` | bool | `false` | Enables OSDP packet capture. Captured packets are written to the `captures/` folder as `.osdpcap` (raw) and `.txt` (parsed) files. |

---

## `Connection`

Controls how the PD listens for an ACU.

| Setting | Type | Default | Description |
|---|---|---|---|
| `Type` | enum | `Serial` | Transport type: `Serial` or `TcpServer`. |
| `SerialPortName` | string | `"COM3"` | Serial port to listen on (e.g. `COM3`, `/dev/ttyUSB0`). Used when `Type` = `Serial`. |
| `SerialBaudRate` | int | `9600` | Serial line rate; must match the ACU. Used when `Type` = `Serial`. |
| `TcpServerAddress` | string | `"0.0.0.0"` | Local address the PD binds to. Used when `Type` = `TcpServer`. |
| `TcpServerPort` | int | `12000` | TCP port the PD listens on. Used when `Type` = `TcpServer`. |

---

## `Device`

Identity and capabilities the PD reports to the ACU (via `osdp_ID` / `osdp_CAP`).

| Setting | Type | Default | Description |
|---|---|---|---|
| `Address` | byte | `0` | The PD's OSDP address (0–127). Must match what the ACU polls. |
| `UseCrc` | bool | `true` | Use CRC-16 message checking (vs. the simple checksum). |
| `VendorCode` | string (hex) | `"000000"` | 3-byte IEEE vendor OUI, reported in `osdp_PDID`. |
| `Model` | string | `"PDConsole"` | Product model name. |
| `SerialNumber` | string | `"123456789"` | Device serial number. |
| `FirmwareMajor` | byte | `1` | Firmware major version. |
| `FirmwareMinor` | byte | `0` | Firmware minor version. |
| `FirmwareBuild` | byte | `0` | Firmware build number. |
| `Capabilities` | list | see below | Device capability reports returned to `osdp_CAP`. |
| `ExtendedId` | object | see below | Extended ID (`osdp_EXT_PDID`) details. |

### `Device.Capabilities`

A list of capability entries, each with:

| Field | Type | Description |
|---|---|---|
| `Function` | enum | Capability function code (see table below). |
| `Compliance` | byte | Compliance level for that function (meaning is function-specific per the OSDP spec). |
| `NumberOf` | byte | Quantity (e.g. number of inputs, outputs, readers, LEDs). |

The PD also derives the size of its status reports from these counts — e.g. `ContactStatusMonitoring.NumberOf`
sets the number of inputs reported in `osdp_ISTATR`, `OutputControl.NumberOf` the outputs in
`osdp_OSTATR`, and `Readers.NumberOf` the readers in `osdp_RSTATR`.

Valid `Function` values:

| Function | Notes |
|---|---|
| `ContactStatusMonitoring` | Number of inputs. |
| `OutputControl` | Number of outputs. |
| `CardDataFormat` | Card data format support. |
| `ReaderLEDControl` | Number of LEDs per reader. |
| `ReaderAudibleOutput` | Number of audible outputs. |
| `ReaderTextOutput` | Number of text rows. |
| `TimeKeeping` | Time-keeping support. |
| `CheckCharacterSupport` | Whether CRC/check character is supported. |
| `CommunicationSecurity` | AES-128 / secure channel support. |
| `ReceiveBufferSize` | Max receive buffer size. |
| `LargestCombinedMessageSize` | Max combined message size. |
| `SmartCardSupport` | Transparent-mode / smart card support. |
| `Readers` | Number of readers. |
| `Biometrics` | Biometric support. |
| `SecurePINEntry` | Secure PIN entry support. |
| `OSDPVersion` | Reported OSDP version. |
| `ExtendedIdResponse` | Extended ID (`osdp_EXT_PDID`) support. |

### `Device.ExtendedId`

Used to build the extended ID response (`osdp_EXT_PDID`).

| Setting | Type | Default | Description |
|---|---|---|---|
| `Manufacturer` | string | `"PDConsole Simulator"` | Manufacturer name. |
| `HardwareDescription` | string | `"Virtual PD"` | Free-text hardware description. |
| `Url` | string | `""` | Optional product URL (omitted when empty). |
| `ConfigurationReference` | string | `""` | Optional configuration reference (omitted when empty). |
| `AdditionalFirmwareVersions` | list of string | `[]` | Extra firmware version strings (e.g. for multiple microcontrollers). |

---

## `Security`

Controls the PD's secure channel behavior.

| Setting | Type | Default | Description |
|---|---|---|---|
| `SecureChannelMode` | enum | `ClearText` | `ClearText`, `Install`, or `Secure` (see below). |
| `SecureChannelKey` | string (hex) | default key | 16-byte SCBK as a 32-char hex string. Only used when mode is `Secure`. |

`SecureChannelMode`:

| Mode | Meaning |
|---|---|
| `ClearText` | Secure channel disabled; all communication is in the clear (`RequireSecurity = false`). |
| `Install` | Secure channel using the well-known default key **SCBK-D** (`30 31 … 3F`). Used for first-time keying; the ACU can then push a real key via `osdp_KEYSET`. |
| `Secure` | Secure channel using the per-installation key **SCBK** from `SecureChannelKey`. |

> When an ACU sends `osdp_KEYSET` while in `Install` or `Secure` mode, PDConsole stores the new key,
> switches the mode to `Secure`, and saves `appsettings.json`. The ACU then re-establishes the secure
> channel with the new key.

---

## `Simulation`

Values the PD uses when simulating user activity.

| Setting | Type | Default | Description |
|---|---|---|---|
| `CardNumber` | string | `"01010101010101010101010101"` | Card bit string presented when simulating a card read. |
| `PinNumber` | string | `"1234#"` | Keypad digits presented when simulating PIN entry. |

---

## Example `appsettings.json`

```json
{
  "Connection": {
    "Type": "Serial",
    "SerialPortName": "COM3",
    "SerialBaudRate": 9600,
    "TcpServerAddress": "0.0.0.0",
    "TcpServerPort": 12000
  },
  "Device": {
    "Address": 0,
    "UseCrc": true,
    "VendorCode": "000000",
    "Model": "PDConsole",
    "SerialNumber": "123456789",
    "FirmwareMajor": 1,
    "FirmwareMinor": 0,
    "FirmwareBuild": 0,
    "ExtendedId": {
      "Manufacturer": "PDConsole Simulator",
      "HardwareDescription": "Virtual PD",
      "Url": "",
      "ConfigurationReference": "",
      "AdditionalFirmwareVersions": []
    },
    "Capabilities": [
      { "Function": "ContactStatusMonitoring", "Compliance": 4, "NumberOf": 1 },
      { "Function": "OutputControl", "Compliance": 4, "NumberOf": 1 },
      { "Function": "CardDataFormat", "Compliance": 1, "NumberOf": 1 },
      { "Function": "ReaderLEDControl", "Compliance": 4, "NumberOf": 1 },
      { "Function": "ReaderAudibleOutput", "Compliance": 2, "NumberOf": 1 },
      { "Function": "ReaderTextOutput", "Compliance": 1, "NumberOf": 1 },
      { "Function": "CheckCharacterSupport", "Compliance": 1, "NumberOf": 1 },
      { "Function": "CommunicationSecurity", "Compliance": 1, "NumberOf": 1 },
      { "Function": "SmartCardSupport", "Compliance": 0, "NumberOf": 0 },
      { "Function": "Readers", "Compliance": 0, "NumberOf": 1 },
      { "Function": "OSDPVersion", "Compliance": 2, "NumberOf": 0 },
      { "Function": "ExtendedIdResponse", "Compliance": 1, "NumberOf": 0 }
    ]
  },
  "Security": {
    "SecureChannelMode": "Install",
    "SecureChannelKey": "303132333435363738393A3B3C3D3E3F"
  },
  "Simulation": {
    "CardNumber": "01010101010101010101010101",
    "PinNumber": "1234#"
  },
  "EnableLogging": true,
  "EnableTracing": true
}
```
