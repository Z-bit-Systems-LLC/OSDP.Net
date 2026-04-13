# SmartCardSample

A console app demonstrating **OSDP Transparent Mode** (XWR / XRD commands) using `OSDP.Net`. It tunnels raw ISO 7816-4 APDUs from the ACU through a PD's reader to a smart card, letting you test contact or contactless smart cards over an OSDP link.

## What it does

1. Opens a serial connection to a PD.
2. Every 5 seconds, polls the PD with `ReadModeSetting`. If the PD reports it is in Mode 0, the sample switches it to Mode 1 (transparent mode).
3. When the PD raises an unsolicited `ExtendedRead` reply with `Mode=1, PReply=1` (card present), the sample:
   - Sends a `SmartCardScan` to confirm the card is present.
   - Prints `Card Present` and drops into an **APDU REPL**.
   - In the REPL, each line is parsed as a hex string, sent as a `PassAPDUCommand`, and the response is printed as `Mode:PReply:hex-bytes`.
   - An empty line exits the REPL and terminates the smart card session on the PD.
4. Press **Ctrl+C** at any time to exit the sample cleanly.

## Configuration

Edit `appsettings.json` before running:

```json
{
    "OSDP": {
        "PortName":      "COM4",
        "BaudRate":      "9600",
        "DeviceAddress": "0",
        "ReaderNumber":  "0"
    }
}
```

| Key             | Meaning                                                                |
|-----------------|------------------------------------------------------------------------|
| `PortName`      | Serial port the PD is connected to (e.g. `COM4` on Windows, `/dev/ttyUSB0` on Linux). |
| `BaudRate`      | OSDP baud rate — must match the PD's configured rate.                  |
| `DeviceAddress` | OSDP address of the PD.                                                |
| `ReaderNumber`  | Reader index on the PD (most single-reader PDs use `0`).               |

The sample uses **clear-text** OSDP (no secure channel). If your PD requires a secure channel you'll need to switch the `AddDevice` call in `Program.cs` to pass a key.

## Running

```sh
dotnet run --project src/samples/SmartCardSample
```

You should see:

```
SmartCardSample running. Press Ctrl+C to exit.

Device is Online in Clear Text mode
```

Once the PD comes online and reports transparent mode, present a smart card to the reader. The sample will print `Card Present` and prompt you for APDUs:

```
Card Present. Enter APDU as hex (blank line to exit):
>
```

## Testing card reads with common APDUs

Input is case-insensitive and **spaces are ignored**, so `00A40400...` and `00 A4 04 00 ...` both work.

### Universal "is the card alive?" — GET CHALLENGE

```
00 84 00 00 08
```

Almost every ISO 7816-4 card responds with 8 random bytes + `90 00`. Good first sanity check because it doesn't depend on any particular applet.

### SELECT PIV Application (best fit for access-control readers)

```
00 A4 04 00 0B A0 00 00 03 08 00 00 10 00 01 00
```

- **PIV card:** returns an FCI template (`61 xx ...`) ending in `90 00`.
- **Non-PIV card:** returns `6A 82` (file/app not found) — still proves the transparent tunnel works.

### SELECT PPSE (contactless EMV payment cards)

```
00 A4 04 00 0E 32 50 41 59 2E 53 59 53 2E 44 44 46 30 31 00
```

Any contactless Visa/Mastercard returns its list of supported AIDs.

### SELECT Master File (legacy / generic ISO 7816)

```
00 A4 00 00 02 3F 00
```

### Reading the response

The sample prints the decoded reply as `Mode:PReply:hex-bytes`. The **last two bytes** of the hex are the ISO 7816-4 status word:

| SW      | Meaning                          |
|---------|----------------------------------|
| `90 00` | Success                          |
| `6A 82` | File / applet not found          |
| `6D 00` | INS (instruction) not supported  |
| `6E 00` | CLA (class) not supported        |
| `63 Cx` | Authentication failed, `x` tries remaining |
| `69 82` | Security status not satisfied    |

For example, a response of `1:1:61-17-4F-0B-A0-00-00-03-08-00-00-10-00-01-00-90-00` means: Mode 1, PReply 1, and the card returned a PIV FCI template ending in `90 00` (success).

## Troubleshooting

- **`Device is Offline`** — check the serial port, baud rate, wiring, and PD address.
- **`No reply to ReadModeSetting — resetting device.`** — the PD accepted the command but didn't respond; the sample automatically resets the device and retries.
- **`Received NAK ...`** — the PD explicitly rejected the command. Common causes: the PD doesn't support transparent mode, or secure channel is required.
- **APDU returns `6D 00` / `6E 00`** — the card doesn't support the instruction you sent; try a different APDU.
- **APDU returns nothing / times out** — no card is present, the card was removed mid-command, or the reader couldn't negotiate with the card. Present the card and retry.

## Related documentation

- [OSDP Transparent Mode](https://www.securityindustry.org/2026/03/18/security-industry-association-confirms-openness-of-transparent-mode-for-osdp-standard/) — SIA article confirming the protocol is open.
- [`docs/transparent-mode-guide.md`](../../../docs/transparent-mode-guide.md) — transparent mode protocol overview, ACU and PD usage, and API reference.
- [`docs/supported_commands.md`](../../../docs/supported_commands.md) — full list of OSDP commands supported by this library.
- [`docs/api-usage-guide.md`](../../../docs/api-usage-guide.md) — general `ControlPanel` usage.
