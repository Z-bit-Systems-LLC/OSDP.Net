# osdp-linequality

A command-line tester for RS-485 line quality on an OSDP bus.

It bounces known bit patterns off a responder at a dedicated test address, at every supported baud
rate, and reports what came back. Use it to decide whether a cable run will carry OSDP reliably and
at what speed — before a reader goes on the end of it and starts dropping card reads.

Implements the [OSDP Line Quality Test Procedure v1.1](https://gist.github.com/bytedreamer/94d039c3eb033ec62b4806161f690125).

---

## What it actually measures

For each baud rate it sends 16 combinations of test pattern and payload size, then counts the
outcome of every packet in one of five buckets:

| Outcome | Meaning | Usually indicates |
|---|---|---|
| Received | Correct echo, right sequence number, right bytes | — |
| Timeout | No reply inside the 200 ms window | Open or unpowered line, wrong address, termination fault |
| Integrity error | Reply arrived but failed its CRC | Marginal signal integrity, EMI, reflections |
| NAK | Responder refused the command | Check the error code in the log |
| Pattern mismatch | Valid CRC, wrong bytes back | Bit error that survived the CRC, or a responder defect |

**Keeping these separate is the point.** A tool that reports one "failed" number tells you the link
is bad; this tells you *how* it is bad, which is what narrows down where to look.

The six patterns (all-zeros, all-ones, 0xAA, 0x55, sequential, walking-one) stress different
electrical characteristics — DC balance, maximum current draw, worst-case transition density, and
single-bit isolation. Pattern-dependent failures are a signature of reflections or stub cables.

---

## Requirements

- Two RS-485 interfaces on the bus: one running `run` (the controller) and one running `respond`.
  Two USB-to-RS-485 adapters on the cable under test is the normal setup.
- .NET 10 SDK to build.
- The responder must be reachable at address **125 (0x7D)**, which is reserved for this test and
  will not disturb production devices on the same bus.

---

## Build

```
dotnet build src/OSDP.Net.LineQuality.Cli -c Release
```

The executable lands in `src/OSDP.Net.LineQuality.Cli/bin/Release/net10.0/osdp-linequality.exe`.

---

## Quick start

Find your ports:

```
osdp-linequality ports
```

Terminal 1 — the responder, on the far end of the cable:

```
osdp-linequality respond --port COM7
```

Terminal 2 — the controller:

```
osdp-linequality run --port COM3
```

That sweeps all six OSDP baud rates at the Screening profile: 960 packets, about 90 seconds.

---

## Commands

### `run` — act as the controller

| Option | Default | Description |
|---|---|---|
| `--port <name>` | *required* | Serial port, e.g. `COM3` |
| `--profile <name>` | `screening` | `screening`, `qualification`, or `extended` |
| `--rates <list>` | the six OSDP rates | Comma-separated baud rates to sweep |
| `--address <n>` | `125` | Responder address |
| `--timeout-ms <n>` | `200` | Reply window |
| `--json <path>` | — | Full report as JSON |
| `--markdown <path>` | — | Commissioning report as Markdown |
| `--osdpcap <path>` | — | Packet capture, readable in Wireshark |
| `--no-return` | off | Leave the responder at the last rate tested |
| `--quiet` | off | Suppress progress output |

Optional details that appear in the Markdown report: `--tester`, `--location`, `--cable`,
`--responder`, `--notes`, and `--latency-timer-adjusted` (see [Timing](#timing-read-this-before-quoting-response-times)).

### `respond` — act as the test responder

| Option | Default | Description |
|---|---|---|
| `--port <name>` | *required* | Serial port, e.g. `COM7` |
| `--address <n>` | `125` | Address to answer at |
| `--baud <n>` | `9600` | Starting baud rate |
| `--auto-revert-seconds <n>` | `30` | Idle time before falling back to 9600 |
| `--no-auto-revert` | off | Never fall back to 9600 |
| `--quiet` | off | Suppress progress output |

The responder follows the controller through baud rate changes and prints each one. If it is left
stranded on a high rate — because the controller was interrupted — it returns to 9600 by itself
after the idle timeout, so the next run can find it without a power cycle.

### `ports` — list serial ports

---

## Profiles and what a PASS is worth

A run that observes zero failures does not prove zero loss; it puts an upper bound on it. With N
packets and no failures, the 95% upper bound on the loss rate is roughly 3/N.

| Profile | Iterations per combination | Packets per rate | Detects loss above | Time per rate at 9600 |
|---|---:|---:|---:|---:|
| `screening` | 10 | 160 | ~1.9% | ~30s |
| `qualification` | 60 | 960 | ~0.31% | ~3m |
| `extended` | 200 | 3200 | ~0.094% | ~10m |

**Only `extended` can substantiate the 99.9% success criterion.** Screening is for finding gross
faults quickly and narrowing the candidate rates; qualification is the sensible default for a
commissioning report; run extended once at the rate you intend to use.

The reported detection limit appears next to every result, so a screening PASS cannot quietly be
filed as a commissioning result.

### Verdicts

| Verdict | Criteria |
|---|---|
| PASS | Zero failures, every response inside 200 ms |
| MARGINAL | Success ≥ 99.0% with at least one failure, or a response over 150 ms |
| FAIL | Success < 99.0%, a response over 200 ms, or the rate change did not complete |
| UNTESTED | The responder would not switch to that rate |

The bands overlap deliberately and are applied worst-first.

---

## Output

Console output is a summary table plus a failure breakdown when anything failed.

- `--json` writes the complete report, including per-combination timing, for tooling.
- `--markdown` writes the section 7 commissioning report: test information, summary, failure
  breakdown, per-rate detail, measurement notes, and a recommendations section with blanks for the
  technician to complete.
- `--osdpcap` writes a Wireshark-compatible capture of every packet, which is the fastest way to
  see what actually went out when something looks wrong.

### Exit codes

| Code | Meaning |
|---:|---|
| 0 | Every tested rate passed |
| 1 | Best result was marginal |
| 2 | One or more rates failed |
| 3 | The test could not run |

Suitable for gating a CI job or a commissioning script.

---

## Timing: read this before quoting response times

Response time is measured from the end of the command to the first byte of the reply, matching
REPLY_DELAY in OSDP section 5.7.

**On a PC, the number you get is dominated by the serial adapter, not the cable.** FTDI parts ship
with a 16 ms latency timer; at 230400 baud the entire exchange is about 1 ms of wire time, so a
reported 35 ms is essentially all adapter. In bench testing, maximum response time came out at
31–52 ms at *every* baud rate — that flatness is the tell.

So:

- Treat the 200 ms window as a **pass/fail gate**, which is what it is for.
- Treat average and maximum times as **indicative only** unless you lowered the latency timer
  (Device Manager → Ports → Advanced → Latency Timer → 1 ms) and recorded that with
  `--latency-timer-adjusted`.
- A native UART — an embedded controller, or a Pi's on-board serial — does not have this problem.

---

## Troubleshooting

**"Access to the path 'COM7' is denied"** — another process holds the port. Close the other
terminal program, or find the holder with `Get-PnpDevice -Class Ports` and Task Manager.

**"No line quality responder answered ... at any baud rate"** — check that `respond` is running,
that both ends are on the same bus with A/B the right way round, and that the addresses match. The
controller probes the baseline three times and then searches every rate before giving up, so this
message means it genuinely found nothing.

**Before believing that message, look at the responder's exchange counter.** The controller cannot
tell whether its commands were heard; it only knows that nothing came back. If `run` reports no
responder while `respond` is counting exchanges, the commands are arriving and the *replies* are
not — the fault is in the return direction, not in wiring, power, or addressing. Observed on the
bench with a capacitor across the pair: the responder logged four exchanges at 9600 while the
controller reported nothing at any rate. Suspect an asymmetric fault: a driver failing under load
at one end, a capacitor or stub nearer one end than the other, or a receiver biased differently at
the two ends.

**Everything fails above a certain rate** — that is the expected shape of a cable length or quality
limit. Use the highest rate that passed.

**Failures only at 113-byte payloads** — check the responder's receive buffer. 113 bytes is chosen
so the frame is exactly 128 bytes, the smallest buffer OSDP guarantees.

**Pattern mismatches with valid CRCs** — suspect the responder implementation before the cable.

---

## Interoperability

Test traffic uses address **125 (0x7D)** and vendor code **02-00-0A** (a locally-administered OUI,
so it can never collide with a registered manufacturer). It is sent in clear text with CRC-16;
secure channel is deliberately not used, because encrypting the payload would turn all six test
patterns into statistically identical noise and defeat the entire measurement.

Production devices ignore packets addressed to 125, and this tool ignores replies from any other
address, so the test can run on a live bus.
