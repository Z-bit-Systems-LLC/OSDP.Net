# OSDP Line Quality Test — Implementation Plan (OSDP.Net)

**Status:** M1–M6 complete and merged to `main` (`f005a85`, 2026-08-20) · validated on real RS-485
**Spec:** [OSDP Line Quality Test Procedure v1.2](https://gist.github.com/bytedreamer/94d039c3eb033ec62b4806161f690125)

> **Implementation notes (2026-08-20)**
>
> M1–M6 are complete: 71 line quality tests within a suite of 650, all passing, and
> `jb inspectcode` clean at 0 errors and 0 warnings.
>
> Two things changed from the plan as written:
>
> 1. **`IRetunableOsdpConnection` was extracted** (`src/OSDP.Net/Connections/`). The driver and
>    responder originally took `SerialPortOsdpConnection` directly, which made the whole feature
>    untestable without hardware — a problem for CI as much as for the bench. Both now take the
>    interface, and `test/OSDP.Net.Tests/LineQuality/LoopbackConnection.cs` provides an in-memory
>    pair with a write filter for dropping and corrupting frames.
> 2. **Loopback integration tests were added ahead of M6.** They prove the two halves agree,
>    that a baud change moves both ends, and that each failure category lands in the right
>    bucket. They cannot prove anything about timing or signal integrity — an in-memory pipe
>    delivers instantly and never drops a bit — which is why M6 on real adapters still mattered,
>    and it duly found two bugs the loopback could not.
>
> **M6 results (COM3 ↔ COM7, two FTDI USB-RS-485 adapters):**
>
> | Run | Packets | Failures | Result |
> |---|---|---|---|
> | Screening, 9600 only | 160 | 0 | PASS, 29.7s |
> | Screening, all six OSDP rates | 960 | 0 | PASS at every rate, 1m 17s |
> | Qualification, 230400 | 960 | 0 | PASS, detection limit 0.31% |
>
> Baud switching was exercised at every transition and both ends stayed in step:
> `9600→19200→38400→57600→115200→230400→9600`.
>
> **Capacitor-loaded line (2026-08-20).** A four-decade capacitance box was fitted across the pair
> to degrade the line under control. The break point moves monotonically, roughly one rate step per
> doubling:
>
> | Capacitance | Highest passing rate |
> |---|---|
> | none | 230400 |
> | 100 nF | 115200 |
> | 150 nF | 57600 |
> | 300 nF | 19200 |
> | 500 nF | 9600 (19200 marginal, 1 integrity error in 160) |
> | 1000 nF | none — commands arrive at 9600, replies do not |
>
> Measured with no termination fitted, which is the harsh case: an unterminated line gives a given
> capacitor a much longer time constant. The full recipe is in Appendix 8.4 and 8.5.5 of the spec.
>
> **Five bugs only hardware could have found**, all fixed:
>
> 1. **`TryReturnToBaseline` failed silently.** It ignored whether the change was acknowledged and
>    retuned the controller anyway, leaving the responder on 230400 while the controller dropped to
>    9600. The next run then could not find the responder at all. Now retried, outcome checked, and
>    a warning names the rate the responder may still be on.
> 2. **Recovery probed each rate only once.** A single missed reply made the search walk past the
>    rate the responder was actually on and give up. Now two attempts per rate.
> 3. **The initial presence probe was a single shot.** The first exchange of a session is the
>    slowest — a managed responder is still warming its reply path and can miss the 200 ms window
>    once, sending the controller into a needless rate search. Now three attempts. Probes are not
>    measurements, so retrying them compromises no statistic.
> 4. **A failed baud rate change was reported as `UNTESTED`, and the run still said `PASS`.** When
>    the capacitor made 230400 unreachable, the report showed the rate as untested and an overall
>    verdict of PASS — materially understating the finding. Spec §6.1 counts a rate change that did
>    not complete as a **FAIL** of that rate. `BaudRateResult` now separates `FailureReason` (the
>    line could not carry the transition, or could not sustain the rate once there → FAIL) from
>    `SkipReason` (the responder said it does not support that rate → UNTESTED, since that is a
>    property of the device rather than the cable). Both the console and Markdown reports show the
>    two under separate headings.
> 5. **A failed search left the controller on the highest rate it had probed**, so every later rate
>    change went out over a link that had just proven it could not carry that rate. One real failure
>    became a cascade of invented ones. The controller now falls back to the baseline, which is also
>    where a responder implementing the idle revert will be.
>
> Separately, **the responder could be wedged permanently by line noise**: the shared `Bus` read
> helpers report a faulted port and an idle line identically, so a receiver stuck in an error state
> spun forever without answering or logging. It now tells a read timeout from a read error and
> reinitialises the port after three consecutive failures. Confirmed on the bench — the 500 nF run
> logged repeated `Serial read failed` and kept working, where the previous build died silently.
>
> **Empirical confirmation of the timing caveat (spec §5.2).** Max response time was 31–52 ms at
> *every* baud rate, including 230400 where the entire exchange is about 1 ms of wire time. That
> flatness is the FTDI latency timer (16 ms per direction), not the cable — exactly what the
> caveat predicts. It is the strongest argument for treating timing as a pass/fail gate against
> 200 ms rather than as a measurement.

## 1. Scope

Implement **both** roles of the line quality (LQ) test in OSDP.Net:

- **Driver** (test controller / ACU side) — sends echo requests, drives baud rate changes, collects statistics, produces a report.
- **Responder** (test device / PD side) — answers at address 125, echoes patterns, switches baud rates.

Having both in one library means the whole protocol can be validated on the bench over a two-port loopback before any embedded firmware exists, and OSDP-Bench gets a working reference responder for free.

OSDP-Embedded and OSDP-Bench work is **out of scope for this plan** and sketched in §9.

## 2. Key decision: run outside `ControlPanel` / `Bus`

The LQ driver must **not** be built on `ControlPanel.ManufacturerSpecificCommand`. The message layer is reusable; the transport layer is not.

| Problem | Where | Why it breaks the test |
|---|---|---|
| Round-trip time is unmeasurable | `ControlPanel.cs:1096-1143` | Commands are queued on a `DeviceProxy` and dispatched by the poll loop with 10 ms `WaitOne` granularity; the caller waits on an event with an 8 s timeout. Measured latency reflects scheduling, not the line. |
| Integrity failures are indistinguishable from timeouts | `Bus.cs:416-419`, `ReplyTracker.cs:30` | `IsValidReply` folds address mismatch and bad CRC together and `ProcessReply` silently returns. Spec §5.1 requires separate counts. |
| Automatic retries corrupt loss statistics | `Bus.cs:320` | A retried packet that succeeds is counted as a delivery. Spec §4.2 prohibits retries during measurement. |
| Bus injects its own traffic | `Bus.cs:203-368` | POLL / ID / CAP / secure channel setup, which an LQ responder does not answer. |
| SQN 0 resets the device | `Bus.cs:410` | Interacts badly with any SQN handling the test needs. |
| Late replies are discarded | `SerialPortOsdpConnection.cs:77` | `DiscardInBuffer()` before every write. |

**Precedent:** `ControlPanel.DiscoverDevice` already operates outside a running bus, opening and closing its own connections. The LQ driver follows the same pattern.

What we *do* reuse, all `internal` to the OSDP.Net assembly:

- `OutgoingMessage` / `Control` — framing, length, CRC, driver byte. Passing a `null` secure channel yields a clear-text frame with no SCB, which is exactly what spec §3.3 requires.
- `OutgoingReply` — sets reply address to `command.Address | 0x80` and echoes the control block. Satisfies responder requirements #4 and #5 with no extra code.
- `IncomingMessage` — parsing plus `IsDataCorrect` for the CRC check.
- `Bus.WaitForStartOfMessage` / `WaitForMessageLength` / `WaitForRestOfMessage` — `internal static` framed-read helpers.
- `Model.CommandData.ManufacturerSpecific` — builds the `osdp_MFG` payload as-is.

## 3. Component design

New namespace `OSDP.Net.LineQuality`, under `src/OSDP.Net/LineQuality/`.

```
LineQuality/
  LineQualityProtocol.cs      constants, pattern generation/validation, baud ID mapping
  TestPattern.cs              enum 0x00-0x05
  LineQualityBaudRate.cs      enum 0x00-0x06 + int mapping
  EchoRequest.cs              PayloadData, Code = 0x80  (wraps ManufacturerSpecific)
  BaudRateChange.cs           PayloadData, Code = 0x80
  EchoResponse.cs             PayloadData, Code = 0x90  (responder side)
  BaudRateChangeAck.cs        PayloadData, Code = 0x90
  LineQualityTest.cs          the driver
  LineQualityOptions.cs       profile, rates, timeouts, tracer, progress, cancellation
  LineQualityReport.cs        results tree + verdicts + detection limits
  LineQualityResponder.cs     the responder
```

### 3.1 Protocol layer

```csharp
public static class LineQualityProtocol
{
    public static ReadOnlySpan<byte> VendorCode => new byte[] { 0x02, 0x00, 0x0A };
    public const byte TestAddress = 0x7D;
    public const int  MaxPayloadLength = 113;   // frame = 128 bytes exactly

    public static byte[] GeneratePattern(TestPattern pattern, int length);
    public static bool   ValidatePattern(TestPattern pattern, ReadOnlySpan<byte> data);
    public static int    ToBaudRate(LineQualityBaudRate id);
    public static bool   TryGetBaudRateId(int baudRate, out LineQualityBaudRate id);
}
```

Pure, allocation-light, trivially unit-testable. Spec §3.6 gives a 6-row expected-output table that becomes the test fixture directly.

### 3.2 Driver

```csharp
public sealed class LineQualityTest
{
    public LineQualityTest(SerialPortOsdpConnection connection, ILoggerFactory loggerFactory = null);
    public Task<LineQualityReport> RunAsync(LineQualityOptions options);
}
```

Per-packet loop, deliberately synchronous in structure:

1. Build `OutgoingMessage(address, new Control(sqn, useCrc: true, hasSecurityControlBlock: false), echoRequest)`.
2. `BuildMessage(null)` → write. Start `Stopwatch` **after** the write completes (spec §5.2 measures from end of command transmission).
3. Read one byte at a time until SOM or 200 ms elapses; stamp the timer at the first reply byte. This is the reported response time.
4. Read the rest of the frame; construct `IncomingMessage` inside a `try`/`catch` — a badly mangled frame can throw during parse, and that counts as an integrity error, not a crash.
5. Classify: `Timeout` / `IntegrityError` (`!IsDataCorrect` or parse throw) / `Nak` / `PatternMismatch` / `Received`.
6. Advance control-byte SQN 1→2→3 and the payload sequence number. **No retries.**
7. Idle-line delay of 2 character times — reuse the `Bus.IdleLineDelay` formula (`Bus.cs:82`).

Baud rate changes issue the `BaudRateChange` command, await the ack, drain, wait 100 ms, then call `SetBaudRate` on the connection (§4.1).

Verdicts and detection limits (`3/N`) are computed in `LineQualityReport` per spec §5.3 and §6.1, so the reporting rules live in one place and are unit-testable without hardware.

### 3.3 Responder

Two options were considered:

- **(A) Subclass `Device`** and override `HandleManufacturerCommand` (`Device.cs:333`). Reuses sequence policing and reply plumbing, but `Device` is driven by a connection *listener*, and changing baud rate mid-session means tearing the listener down and rebuilding it — which fights the 100 ms switch requirement.
- **(B) Standalone `LineQualityResponder`** reading an `IOsdpConnection` directly, framing replies with `OutgoingReply`.

**Choose (B).** It is symmetric with the driver, keeps the baud switch under our control, and the responder legitimately does not need POLL/ID/CAP/secure channel (spec §8.3). A `Device`-based convenience wrapper can follow later if someone wants LQ support inside a full PD.

```csharp
public sealed class LineQualityResponder : IDisposable
{
    public LineQualityResponder(SerialPortOsdpConnection connection,
                                byte address = LineQualityProtocol.TestAddress,
                                ILoggerFactory loggerFactory = null);
    public Task RunAsync(CancellationToken token);
    public event EventHandler<LineQualityExchangeEventArgs> ExchangeCompleted;
}
```

Behaviour per spec §8.3: respond only to its address, echo vendor code / sequence / data, status codes for unsupported pattern and length error, default 9600 on start, and the SHOULD-level **auto-revert to 9600 after 30 s with no valid command** — cheap here and it makes recovery testing far less tedious than power-cycling.

## 4. Library changes required

### 4.1 In-place baud rate change — required

`IOsdpConnection.BaudRate` is get-only (`IOsdpConnection.cs:13`) and `SerialPortOsdpConnection` fixes the rate at construction (`SerialPortOsdpConnection.cs:54`). Today, switching means Close/reopen the COM port: slow, racy on Windows (why `DiscoveryOptions.ReconnectDelay` exists), and it toggles DTR/RTS, which can glitch the line.

`System.IO.Ports.SerialPort.BaudRate` is settable while open. Add:

```csharp
public void SetBaudRate(int baudRate);   // on SerialPortOsdpConnection
```

Leave `IOsdpConnection` untouched — this is serial-specific and does not belong on the interface.

### 4.2 Drain before switching — required

The 100 ms in spec §3.5 starts when the last bit leaves the line, not when `WriteAsync` returns. .NET on Windows has no `tcdrain`. Approach, in order:

1. Poll `SerialPort.BytesToWrite == 0` to clear the driver buffer.
2. Add the computed shift-register time: `frameBytes * 10 / baudRate` seconds.
3. Then the 100 ms settle.

For the 15-byte ack (14 + driver byte) that is ~16 ms at 9600 and less above, so a full 100 ms measured from the write call is safe in practice — but implement the explicit drain anyway, because the responder does the same thing and the margin there is what keeps the two ends in sync.

### 4.3 Responder-safe write path — required

`SerialPortOsdpConnection.WriteAsync` calls `DiscardInBuffer()` before every write (`SerialPortOsdpConnection.cs:77`). Harmless for the driver; **harmful for the responder**, where an inbound command may already be arriving while the previous reply is being sent, and would be discarded.

Add an opt-out (constructor flag or a `DiscardBuffersBeforeWrite` property, defaulting to today's behaviour so nothing existing changes).

### 4.4 Reply name mapping — drive-by fix

`CommandReplyExtensions.cs:63` maps `ReplyType.ManufactureSpecific` (0x90) to `"osdp_MFGSTATR"`, and the doc comment at `ReplyType.cs:114` says the same. Per OSDP v2.2 Annex A.2 — cross-checked against OSDP-Embedded's `osdp_replies.h` — **0x90 is `osdp_MFGREP`**; `osdp_MFGSTATR` is 0x83. This mislabels every MFG reply in trace output across ACUConsole, PDConsole and OSDP-Bench. One-line fix, worth doing with this work since LQ traces are all `osdp_MFGREP`.

### 4.5 Bus integrity-error surfacing — *not* required here

The standalone driver constructs `IncomingMessage` itself, so it can count CRC failures directly; no `Bus` change is needed for this plan. Surfacing integrity failures from `Bus` (`Bus.cs:416`) as a countable event remains worth doing on its own merits — OSDP-Bench's monitor page would benefit — but it is decoupled from LQ and should not gate it.

## 5. Bench validation (COM3 / COM7)

Two ports are available for live wire testing. Target setup:

```
LineQualityTest  ──► COM3 ══ RS-485 ══ COM7 ◄── LineQualityResponder
   (driver)                                        (responder)
```

Both ends are driven by the CLI (§7) — `osdpnet linequality respond` in one terminal, `osdpnet linequality run` in another — so the full matrix runs unattended with no bespoke test scaffolding.

**Hardware: two USB-to-RS-485 adapters on a real cable** (confirmed 2026-08-20). This is the good case — the loopback exercises everything, including baud switching, response timing, and genuine error counts, not just protocol logic.

Two setup steps follow from that:

1. **Set the adapter latency timers to 1 ms** before any run whose timing numbers will be reported. FTDI parts default to 16 ms, which at 230400 baud is several times the entire exchange (§5.2 of the spec). On Windows this is Device Manager → Port Settings → Advanced → Latency Timer. Record whether it was done — the report template has a field for it.
2. **Check termination.** With two adapters on a short bench cable, reflections are a plausible source of pattern-dependent failures that have nothing to do with the code under test. Know whether the adapters have built-in 120 Ω termination before treating a marginal result as a bug.

A useful early sanity check: run the Screening profile on a known-good short cable and confirm zero failures at every rate. Anything else means the harness, not the line, is suspect.

**Fault injection.** For deliberately corrupted frames and dropped replies, the OSDP MCP server (`drop_next_n_replies`, `inject_raw`, `nak_next`) can stand in for the responder and drive the driver's error taxonomy through every branch without needing a marginal cable.

## 6. Test plan

**Unit (no hardware)**

- Pattern generation against the spec §3.6 expected-output table, all six patterns at length 8, plus 0, 1, 48, 113 and boundary rejection at 114.
- **Golden frames** — spec §3.9 now carries three complete command/reply pairs with real CRCs. These go straight in as byte-for-byte assertions:

| Frame | Expected bytes |
|---|---|
| Ex1 command | `53 7D 0F 00 05 80 02 00 0A 01 00 02 00 5E 6B` |
| Ex1 reply | `53 FD 0F 00 05 90 02 00 0A 01 00 00 00 29 9A` |
| Ex3 command | `53 7D 0E 00 07 80 02 00 0A 02 08 04 44 C7` |
| Ex3 reply | `53 FD 0E 00 07 90 02 00 0A 02 08 00 11 39` |

  (Ex2's 31-byte pair is in the spec too. Note `BuildMessage` prepends the `0xFF` driver byte — strip it before comparing.)
- Classification logic: each of timeout / integrity error / NAK / pattern mismatch / success from synthetic reply bytes.
- Verdict and detection-limit maths from synthetic counters — the whole of spec §5.3 and §6.1 with no I/O.

**Integration (COM3 ↔ COM7)**

- Screening profile end to end at 9600, verify 160 packets and a clean report.
- Baud change up and down across all supported rates; assert both ends land on the same rate.
- Recovery: force a failed transition, assert the driver's rate-cycling recovery finds the responder, and that the 30 s auto-revert works.
- Full Screening sweep across all seven rates; compare wall clock against the spec §3.10 estimates.

**Before pushing:** `jb inspectcode OSDP.Net.sln` must be clean (0 errors, 0 warnings) per CLAUDE.md.

## 7. CLI

A new console project, `src/OSDP.Net.LineQuality.Cli`, producing an **`osdp-linequality`** executable. This is the primary way the LQ test gets driven — a field tech running a commissioning report and a CI job running the loopback want the same headless, scriptable entry point, and neither wants Terminal.Gui.

It also collapses bench validation into two terminal windows, so the loopback harness comes free with the tool rather than being separate test scaffolding.

**Scope: line quality only.** This is a single-purpose tester with a flat verb tree, not a general OSDP CLI. No `discover`, no device management. If a broader `osdpnet` tool is ever wanted, it is a separate project that can reference the same library types.

**Shape**

- Target `net10.0`, matching ACUConsole and PDConsole. The library multi-targets `net8.0;netstandard2.0;net10.0`; the CLI does not need to.
- Packable as a .NET global tool (`ToolCommandName` = `osdp-linequality`) so field use needs no repo checkout.
- Argument parsing: **hand-rolled**. Three verbs and roughly eight options do not justify a `System.CommandLine` dependency, and the repo has no CLI parser precedent to follow. Revisit only if the verb count grows.
- Logging through `Microsoft.Extensions.Logging.Console`, already a dependency.

**Verbs**

```
osdp-linequality run                 the driver (§3.2)
    --port COM3
    --profile screening|qualification|extended     (default: screening)
    --rates 9600,19200,38400,...                   (default: the six OSDP rates)
    --address 125
    --json report.json                             machine-readable results
    --markdown report.md                           section 7 commissioning report
    --osdpcap capture.osdpcap                      Wireshark-compatible capture
    --quiet                                        suppress progress, keep summary
    --tester / --location / --cable / --responder / --notes
    --latency-timer-adjusted                       qualifies the timing figures

osdp-linequality respond             the responder (§3.3)
    --port COM7
    --address 125

osdp-linequality ports               list available serial ports
```

`ports` earns its place: the first thing anyone does is work out which COM port the adapter enumerated as.

**Output contract**

Human-readable tables on stdout by default — the spec §7.2 summary table maps onto it almost directly, with the profile and detection limit printed alongside every verdict so a Screening PASS cannot be mistaken for a commissioning result. `--json` emits the same `LineQualityReport` object for tooling.

`--markdown` renders the full spec §7 report. The renderer lives in the **library**
(`LineQualityMarkdownReport`), not the CLI, so OSDP-Bench can reuse it for the same report rather
than reimplementing it — see §10. Fields the test cannot discover (tester, location, cable, PD
model) come from optional flags and are otherwise left as blanks for the technician to complete.

Documentation for the tool itself lives in `src/OSDP.Net.LineQuality.Cli/README.md`.

Exit codes, so CI and scripts can gate on results:

| Code | Meaning |
|---|---|
| 0 | All tested rates PASS |
| 1 | Best result was MARGINAL |
| 2 | One or more rates FAIL |
| 3 | Test could not run (port unavailable, no responder, cancelled) |

**Bench usage**

```
# terminal 1
osdp-linequality respond --port COM7

# terminal 2
osdp-linequality run --port COM3 --profile qualification --json report.json
```

## 8. Milestones

| # | Deliverable | Depends on | Status |
|---|---|---|---|
| M1 | Protocol layer + payload types + unit tests (golden frames) | — | Done |
| M2 | `SetBaudRate`, drain helper, responder-safe write path (§4.1-4.3) | — | Done |
| M3 | `LineQualityResponder` | M1, M2 | Done |
| M4 | `LineQualityTest` driver + `LineQualityReport` | M1, M2 | Done |
| M5 | `OSDP.Net.LineQuality.Cli` project — `run` / `respond` / `ports` | M3, M4 | Done |
| M6 | Loopback validation on COM3/COM7 via the CLI | M5 | Done — see header |
| M7 | Global-tool packaging (`IsPackable` is false for now) | M5 | Not started |

Terminal.Gui integration in ACUConsole/PDConsole is **not** planned — the CLI supersedes it for this feature.

**Still unverified on hardware**, worth doing when someone is next at the bench:

1. **The FTDI latency timer.** Drop it to 1 ms in Device Manager and re-run; the change in reported
   response time is the portion of the measurement that belongs to the adapter rather than the
   cable. Not done here because it edits the machine's device configuration.
2. **The Extended profile** (3200 packets/rate, ~5 minutes at 230400), which is the only profile
   that can substantiate the 99.9% criterion.

**Verified on hardware since:**

- The 30-second idle auto-revert, seen firing as `Idle timeout: reverted to 9600 baud` after a
  failed transition left the responder stranded — exactly the case it exists for.
- The error taxonomy against real physics rather than injected faults: the 500 nF run produced a
  genuine integrity error (1 in 160 at 19200, with max response elevated to 69.5 ms), and every
  capacitance above 100 nF produced real unreachable-rate failures.
- The responder's read-fault recovery, which logged `Serial read failed` repeatedly during the
  500 nF run and kept answering.

**Known limitation, not yet addressed.** Once a rate fails, the responder may be stranded at that
rate and cannot be commanded back over the link that just failed — only its idle revert recovers
it, and a sweep moves between rates faster than that timeout. Rates tested immediately after a
failure are therefore attempted while the responder may still be deaf. The report states what
happened rather than hiding it, but those rows are not cleanly measured. The fix would be to poll
at the baseline for up to the revert window after a failed rate: adaptive, costing nothing when the
responder is reachable and up to 30 s when it is stranded. Deferred as a design call, not a defect.

## 9. Deferred / follow-on

- **Bus integrity-error events** (§4.5) — independent value, not a blocker.
- **`Device`-based responder wrapper** — for PDs that want LQ support alongside normal operation.
- **`.osdpcap` capture during LQ runs** — the tracer hook is already in `LineQualityOptions`; wiring it to `OSDPFileCaptureTracer` is nearly free and makes failures inspectable in Wireshark.

## 10. Downstream repos (not this plan)

**OSDP-Embedded** is the natural home for a dedicated hardware responder. `osdp_mfg_decode` / `osdp_mfgrep_build` already exist and `OSDP_CMD_MFG` is in the dispatch table, so the responder is roughly 200 lines. The only architectural question is baud switching: `osdp_pd_transport_t` has `read`/`write`/`now_ms` and no `set_baud`. Do **not** add I/O to the library — instead have the LQ module call an application callback after the ack is queued and let the app reconfigure its own UART once it has drained, which preserves the library's freestanding design. Gate the module behind a CMake option. `OSDP_FRAME_MAX_LEN` is 1440 so the 128-byte frame is fine; confirm the PD's RX/TX scratch buffers separately.

**OSDP-Bench** gets a `LineQualityAction` alongside the existing `IDeviceAction` implementations, plus a results page. It should call `LineQualityMarkdownReport.Render` for the spec §7 report rather than building its own — the renderer is in the library precisely so both front ends produce the same document. Two things to plan for: the LQ driver needs raw port access outside `ControlPanel`, which the current `ISerialPortConnectionService` seam does not expose; and every new UI string needs resx entries across six languages via Crowdin, so settle the English copy first. Surface the profile and detection limit prominently — a Screening PASS must not read like a commissioning result.

## 11. Open questions

1. **460800 support** — the spec reserves Baud Rate ID 0x06, but OSDP §5.2 lists only 9600 through 230400, so it is an extension rather than a standard rate. It is excluded from `--rates` by default; confirm the adapters can reach it before enabling. (The misleading doc comment on `SerialPortOsdpConnection.StandardBaudRates` calling it standard has been corrected.)
2. **Default profile** — plan assumes Screening as the default for interactive use, Qualification for reports. Confirm before the CLI output format is fixed.
3. **Report format** — is the `--json` shape a stable contract for OSDP-Bench to consume later, or internal-only for now? Affects how much care the `LineQualityReport` serialization needs. The Markdown renderer is already library-side and reusable, so Bench may not need the JSON at all.

**Resolved:** CLI scope (LQ-only, flat verb tree — §7) and bench hardware (two USB-RS-485 adapters — §5), both settled 2026-08-20.
