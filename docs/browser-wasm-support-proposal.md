# Proposal: Run the Control Panel in the Browser (Blazor WebAssembly)

**Status:** Implemented on branch `feature/browser-wasm` (changes 1-6, with change 6 as the `BouncyCastleAes` subclass), not yet merged. Released for wider timing testing as the NuGet prerelease `5.3.0-beta.1`. The prototype it came from was tested in the browser and on RS-485 hardware (see [Test results](#test-results)).
**Date:** 2026-10-30
**Requested by:** OSDP Bench, which is evaluating a browser version built on Blazor WebAssembly and the Web Serial API.

## Summary

OSDP Bench's Core library is already platform-neutral (`net10.0`, no WPF or Windows APIs), and its serial access is behind `IOsdpConnection`, so a Web Serial connection can be plugged in through JS interop. OSDP.Net has two blockers:

1. **Blocking waits in `Bus` and `ControlPanel`.** Default Blazor WebAssembly runs on a single thread, which is also the browser's event loop. The reply dispatcher dies at startup, so no command ever gets a result. The timed waits in the polling loop work, but each one freezes the page for its duration.
2. **AES is not available in browser WebAssembly.** `Aes.Create()` returns an object, but its first `CreateEncryptor()` or `CreateDecryptor()` throws `PlatformNotSupportedException`, so secure channel cannot be established in either direction.

The proposal is to replace the blocking waits with async equivalents and to route AES through an internal provider that falls back to BouncyCastle in the browser. Both changes are internal: `Bus`, `ReplyTracker` and `SecurityContext` are `internal`, and no public signatures need to change. The prototype establishes secure channel in the browser with the BouncyCastle fallback.

Secure Channel 2 (on `feature/osdp-sc2`) adds post-quantum pairing and AES-256-GCM. Its post-quantum primitives already use BouncyCastle and give the same results in the browser as on desktop; its AES-GCM and AES-CBC calls have the same gap as SC1. See [SC2 and post-quantum crypto](#sc2-and-post-quantum-crypto).

## Why it fails today

Reproduced with a Blazor WebAssembly app on .NET 10 in headless Chrome. The app runs `ControlPanel` against an in-process `Device` over a loopback `IOsdpConnection`.

What the single-threaded runtime does with each primitive:

| Primitive | Observed behavior |
|---|---|
| `WaitHandle.WaitOne(timeout)`, `SemaphoreSlim.Wait(timeout)`, `Thread.Sleep`, `BlockingCollection.TryTake(timeout)` | Work, but block the only thread for the full timeout. The page and all JS callbacks freeze meanwhile. |
| `BlockingCollection.Take()` / `GetConsumingEnumerable()` | Throws `PlatformNotSupportedException: Cannot wait on monitors on this runtime` |
| `Task.Factory.StartNew(..., LongRunning)` | Falls back to the thread pool, which is the same single thread. It does not throw. |
| `new Thread(...).Start()` | Throws `PlatformNotSupportedException` |
| `Aes.Create()` | Succeeds and returns an `AesImplementation` |
| `Aes.Create().CreateEncryptor()` / `CreateDecryptor()` | Throws `PlatformNotSupportedException` (`Cryptography_AlgorithmNotSupported`) |
| `AesGcm.IsSupported`, `Kmac256.IsSupported` | Both `false` |
| `SHA256`, `HMACSHA256`, `HKDF`, `RandomNumberGenerator` | Work |

The call sites (line numbers as of `v5.2.4-2-g960f26e`):

| Location | Code | Purpose | Observed effect in WASM |
|---|---|---|---|
| `ControlPanel.cs:62-68` | `Task.Factory.StartNew(... _replies.GetConsumingEnumerable() ..., LongRunning)` | Dispatches replies from all buses to `OnReplyReceived` | **Fails.** The loop throws on its first wait and the faulted task is never observed. Polling keeps running and the device comes online, but no reply is ever dispatched: `IdReport` and `DeviceCapabilities` time out. |
| `Bus.cs:180-192` | `Task.Factory.StartNew(async () => PollingLoop(...), LongRunning).Unwrap()` | Starts the polling loop | Runs on the single thread |
| `Bus.cs:221` | `delayTime.WaitOne(TimeSpan.FromSeconds(5))` | Back-off after the connection fails to open | Freezes the page for 5 seconds per failed open |
| `Bus.cs:234` | `delayTime.WaitOne(TimeSpan.FromMilliseconds(10))` | Paces polls to `_pollInterval` | Freezes the page in 10 ms slices for most of every poll interval |
| `Bus.cs:247` | `_commandAvailableEvent.WaitOne(TimeSpan.FromMilliseconds(10))` | Idle wait for a queued command when not polling; `SendCommand` (`Bus.cs:114`) sets the event | Freezes the page; `SendCommand` from the UI can't run until the wait ends |
| `Bus.cs:351` | `delayTime.WaitOne(IdleLineDelay(Connection, 2))` | Idle-line gap after processing a reply | Short freeze |
| `Bus.cs:567-568` | `delayTime.WaitOne(IdleLineDelay(Connection, commandData.Length))` | Waits for the command to leave the wire before reading the reply | Short freeze |
| `SecurityContext.cs:148` and the transforms created from it | `Aes.Create()`, then `CreateEncryptor()`/`CreateDecryptor()` | Session keys, MAC, encrypt/decrypt | **Fails.** Secure channel can't be set up. Reproduced on the PD side at `SecurityContext.GenerateKey` (`SecurityContext.cs:184`), and the ACU code path is the same |

These are not blockers:

- `ControlPanel.FileTransfer` (`ControlPanel.cs:701`) and `Device.cs:481` use `Task.Run` around async work. That runs on the single thread and is fine.
- `LineTiming.WaitAsync` spins with `Thread.SpinWait`, but only on the PD side (`PdMessageSecureChannel`). Spinning is allowed, though it blocks the thread like the waits above.
- The listener classes use `Task.Run` but are PD-side, so a browser control panel doesn't need them.
- `SerialPortOsdpConnection` references `System.IO.Ports`. The package still loads in WASM; the browser app just must never create that connection type.

The platform compatibility analyzer (CA1416, with `<SupportedPlatform Include="browser" />`) flags the `BlockingCollection` uses and the `Aes.Create()` call, plus the serial and TCP connection classes, which are expected. It does not flag `WaitOne`, so it can't replace a runtime test.

## Proposed changes

1. **Reply dispatch:** replace `BlockingCollection<ReplyTracker>` with an unbounded `Channel<ReplyTracker>` (`SingleReader = true`). `ControlPanel` drains it in an async loop started with `Task.Run`, using `WaitToReadAsync`/`TryRead`. `Bus` takes a `ChannelWriter<ReplyTracker>` and calls `TryWrite`.
   - `System.Threading.Channels` is in-box for `net8.0`/`net10.0` and needs a package reference for `netstandard2.0`. Its dependencies (`Microsoft.Bcl.AsyncInterfaces`, `System.Threading.Tasks.Extensions`) already come in through `System.Text.Json`, so it adds one package.
   - **Do not complete the channel on `Shutdown`.** `ControlPanel` is reused after `Shutdown`: ACUConsole calls `Shutdown` and then `StartConnection` on the same panel (`ACUConsolePresenter.cs:343`, `:360`). A completed channel would silently drop every reply after a reconnect. An idle async loop holds no thread, so leaving it running costs nothing.
   - Catch and log exceptions per reply inside the loop, so one bad handler can't stop all reply delivery.
2. **Polling loop start:** start `PollingLoop` with `Task.Run(...)` instead of `StartNew(..., LongRunning).Unwrap()`.
   - Don't call it as an ordinary async method. The awaits in `PollingLoop` (`Bus.cs:210`, `:358`, `:570`) don't use `ConfigureAwait(false)`, so a direct call would run `Connection.Open()` synchronously on the caller's thread and resume on the caller's `SynchronizationContext` (often a UI thread).
3. **Fixed delays** (`Bus.cs:221`, `:234`): use `await Task.Delay(..., cancellationToken)`, wrapped so that cancellation ends the wait quietly instead of throwing.
   - If `OperationCanceledException` escapes, `PollingLoop` skips its final `await Connection.Close()`. A serial port left open makes the next `StartConnection` on that port fail, and every stop would log "Unexpected exception in polling loop".
   - Also check the token in the inner pacing loop, so a stop doesn't wait out the poll interval.
   - Side benefit: `StopConnection` during the open back-off drops from about 4.5 s to about 1 ms.
4. **Command-available wait** (`Bus.cs:247`): replace the `AutoResetEvent` with `SemaphoreSlim(0, 1)` and `await WaitAsync(10 ms, token)`.
   - Repeated `Set()` calls on an `AutoResetEvent` collapse into one signal, but repeated `SemaphoreSlim.Release()` calls accumulate a count. Release only when `CurrentCount == 0`, and catch `SemaphoreFullException` for the race between the check and the release.
   - A `TaskCompletionSource` swapped each cycle also works, but it must be created with `TaskCreationOptions.RunContinuationsAsynchronously`. Otherwise `SendCommand`'s caller runs the polling loop inline.
5. **Idle-line delays** (`Bus.cs:351`, `:568`): use `await Task.Delay(IdleLineDelay(...), token)`, **not** `LineTiming.WaitAsync`.
   - `WaitOne(TimeSpan)` and `Task.Delay(TimeSpan)` both truncate to whole milliseconds, so timing is unchanged: sub-millisecond gaps (for example 2 characters at 115200) become 0 in both.
   - `LineTiming.WaitAsync` spins for the sub-millisecond tail. In the 16-bus test below that raised CPU from 21% to 130% of one core, and in a browser it blocks the thread on every poll. These waits only set a minimum gap, so the extra precision buys nothing.
6. **AES with a BouncyCastle fallback.** Implemented as an internal `BouncyCastleAes : Aes` subclass (`Messages/SecureChannel/BouncyCastleAes.cs`). It implements `CreateEncryptor`/`CreateDecryptor` with BouncyCastle: ECB and CBC, no padding or zero padding, whole blocks only. `SecurityContext.CreateCypher` gets its `Aes` from `BouncyCastleAes.CreateAes()`, which returns the platform AES where it works and the subclass otherwise, so every call site is unchanged. The `AesProvider` design below was the first plan and is kept for reference.
   - Callers only ever do three things, so an `AesProvider` would need three methods:
     - `EcbEncrypt(key, block)`: one block, for SC1 session keys and cryptograms (`PaddingMode.Zeros` on a full block is the same as no padding).
     - `CbcEncrypt(key, iv, data)` with no padding: SC1 MAC chain and encryption, SC2 handshake.
     - `CbcDecrypt(key, iv, data)` with no padding: SC1 decryption.
   - Each method uses `Aes.Create()` when it works and BouncyCastle's `AesEngine` (with `CbcBlockCipher` for CBC) otherwise. That is the same pattern `SC2CryptoProvider.Kmac256` already uses.
   - **Detect support by creating a transform, not by calling `Aes.Create()`.** In the browser `Aes.Create()` succeeds and only the transform throws, so a check on `Aes.Create()` alone never falls back (the prototype's first browser run failed this way). Probe once into a `static readonly bool`, which works on every target framework including `netstandard2.0`:

     ```csharp
     private static readonly bool NativeSupported = IsNativeSupported();

     // In browser WebAssembly Aes.Create() succeeds; the first transform throws.
     private static bool IsNativeSupported()
     {
         try
         {
             using var aes = Aes.Create();
             using var transform = aes.CreateEncryptor(new byte[16], new byte[16]);
             return true;
         }
         catch (PlatformNotSupportedException)
         {
             return false;
         }
     }
     ```

     `OperatingSystem.IsBrowser()` would also work on `net8.0`+, but the probe also covers other runtimes without AES and needs no `#if`.
   - Keep an internal switch that forces the BouncyCastle path, so CI can run the whole test suite through it without a browser.
   - Keep native AES on desktop: on Windows it is the FIPS-validated CNG implementation, and desktop behavior stays exactly as it is today. AES output is identical either way (confirmed below), so the single-code-path argument `PairingCryptoProvider` uses for ML-KEM/ML-DSA test vectors doesn't apply here.
   - Call sites (`main`, `v5.2.4-2-g960f26e`): `SecurityContext.CreateCypher`/`GenerateKey`, `MessageSecureChannel.cs:106`, `:129`, `:158`, `:161`, `:186`, `:189`, `PdMessageSecureChannel.cs:222`, `:272`, and `Tracing/MessageSpy.cs:133`, `:146`. On `feature/osdp-sc2` add `SC2SecurityContext.cs:142` and `:172`.
   - A smaller alternative, used in the prototype: an internal `BouncyCastleAes : Aes` subclass that implements `CreateEncryptor`/`CreateDecryptor` with BouncyCastle (ECB and CBC, no or zero padding on whole blocks), returned by `CreateCypher` when native AES is unsupported. It leaves every call site unchanged, at the cost of implementing `ICryptoTransform`.
   - The browser's SubtleCrypto is not an option: it is async-only and has no ECB mode.
   - BouncyCastle is only a dependency on `feature/osdp-sc2` today. Doing this there, and merging SC2 before the browser work, avoids adding the dependency to `main` separately. If `main` needs browser secure channel before SC2 merges, adding `BouncyCastle.Cryptography` to `main` is the simplest route; a hand-written AES-128 tested against FIPS-197 is the dependency-free alternative.
   - **Download size:** `BouncyCastle.Cryptography` is not marked trimmable, so with Blazor's default trimming the whole 4.9 MB assembly ships (2.1 MB Brotli). With `<TrimMode>full</TrimMode>` in the browser app it shrinks to 18 KB for AES only, or 265 KB (105 KB Brotli) with the SC2 and pairing primitives. OSDP Bench should set `TrimMode=full`, and the OSDP.Net docs should say so.

   Sketch of one method (BouncyCastle 2.5.1 API):

   ```csharp
   internal static byte[] CbcEncrypt(byte[] key, byte[] iv, byte[] data)
   {
       if (NativeSupported)
       {
           using var aes = Aes.Create();
           aes.Key = key;
           return aes.EncryptCbc(data, iv, PaddingMode.None);
       }

       var cipher = new CbcBlockCipher(new AesEngine());
       cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
       var output = new byte[data.Length];
       for (var offset = 0; offset < data.Length; offset += 16)
           cipher.ProcessBlock(data, offset, output, offset);
       return output;
   }
   ```

   `EncryptCbc` is `net6.0`+; the `netstandard2.0` build needs `CreateEncryptor().TransformFinalBlock` instead.

### Side benefits on desktop

- Each `ControlPanel` currently holds a dedicated thread for reply dispatch, and that thread never exits: nothing calls `CompleteAdding` on `_replies`, so `GetConsumingEnumerable` waits forever, even after `Shutdown`. The thread also keeps the `ControlPanel` reachable, so a discarded panel is never collected. With an async loop, an idle panel holds no thread and can be garbage-collected.
- The polling loop only starts on a dedicated thread. After its first real `await` it continues on thread-pool threads, so today its `WaitOne` calls block pool threads. Async waits free those threads; in the 16-bus test the process used 30 threads instead of 44.

## SC2 and post-quantum crypto

Read from `feature/osdp-sc2` in the `OSDP.Net-sc2` clone (commit `238207e`). The primitives were run directly in the browser test app with BouncyCastle 2.5.1; the SC2 channel and pairing code itself has not been run in a browser.

| Primitive | Used for | Current code | In the browser |
|---|---|---|---|
| ML-KEM-768, ML-DSA-44 | Pairing | BouncyCastle on every target (`Pairing/PairingCryptoProvider.cs:15`), because .NET's own `MLKem`/`MLDsa` depend on Windows CNG or OpenSSL 3.5+ | **Works.** Deterministic key generation and signatures match desktop |
| KMAC256 | SC2 key derivation | Native when `Kmac256.IsSupported`, BouncyCastle otherwise (`SC2CryptoProvider.cs:28`) | **Works.** `Kmac256.IsSupported` is `false`, so the existing BouncyCastle fallback runs and matches desktop |
| AES-256-GCM | SC2 messages | Native `AesGcm` with no support check on `net8.0`+ (`SC2CryptoProvider.cs:50`, `:72`); the BouncyCastle methods are compiled only for `netstandard2.0` | **Fails with the current code.** `AesGcm.IsSupported` is `false`. BouncyCastle GCM works and matches native output, so remove the `#if !NET8_0_OR_GREATER` around the BouncyCastle methods and check `AesGcm.IsSupported` at runtime |
| AES-256-CBC | SC2 handshake | `Aes.Create()` (`SC2SecurityContext.cs:142`, `:172`) | **Fails with the current code.** Covered by change 6 |
| HKDF, HMAC, SHA-256, `RandomNumberGenerator` | Pairing, both channels | Native .NET | **Works** natively |

OSDP-Embedded's SC2 branch was also checked. It takes crypto through ports for wolfCrypt and PQClean, which would have to be compiled to WebAssembly separately, so it doesn't make SC2 in the browser any easier.

## Compatibility and risks

- **Public API:** the prototype changes only `internal` and `private` members, so no change is expected. There is no automated check today: the ApiCompat package reference is commented out in `OSDP.Net.csproj` and not run in CI, and `scripts/generate-api-baseline.ps1` fails because it reflects over the `net8.0` assembly without loading `Microsoft.Extensions.Logging.Abstractions`. Fix the script before relying on `api-baseline.txt`.
- **New dependency:** on `main`, change 6 adds `BouncyCastle.Cryptography` for every consumer, although desktop never calls it. That is moot once SC2 merges, since SC2 already depends on it.
- **Timing on Windows:** measured, see below. Poll rate and reply latency are unchanged with `Task.Delay`, both on loopback and on RS-485.
- **Line quality:** the line quality test retunes the baud rate in place and is the most timing-sensitive feature. Run its full test plan (`docs/line-quality-test-plan.md`) against hardware before releasing.
- **Event ordering:** replies must still reach `OnReplyReceived` in the order the buses produced them, which a single consumer loop preserves.
- **Exceptions in the consumer loop:** the typed and raw reply events are already wrapped in try/catch (`ControlPanel.cs:1580`, `:1593`). The private `ReplyReceived` invoke (`ControlPanel.cs:1484`) and the result-setting path are not, so the per-reply catch in change 1 is still needed.
- **Inline continuations (unchanged):** the `SendCommand` result source (`ControlPanel.cs:1099`) is created without `RunContinuationsAsynchronously`, so consumer code awaiting a command runs inline on the reply dispatcher. That is true today and the proposal doesn't change it, but a slow consumer delays all reply delivery, in the browser as on desktop.

## Test results

"Prototype" is changes 1-5, plus change 6 implemented as the `BouncyCastleAes` subclass described above.

### Unit tests

- 711/711 pass on both `main` and the prototype (`net10.0`, Release).
- On `feature/browser-wasm`, which adds 13 tests (BouncyCastle AES vectors and comparisons, secure channel with BouncyCastle forced, `Shutdown` then restart, and stopping during the open back-off), 724/724 pass.
- With BouncyCastle forced for all AES, 711/711 pass on two full runs. The first forced run had five secure-channel integration tests time out waiting for the device to come online. They passed when run alone and in both later full runs, so this looks like a cold-start timing issue rather than a crypto fault.

### Browser

Blazor WebAssembly on .NET 10, headless Chrome, `ControlPanel` and `Device` in the same page over a loopback connection, default SCBK-D key.

| | `main` | Prototype |
|---|---|---|
| Device online | Yes, after about 2 s | Yes |
| `IdReport`, `DeviceCapabilities` | Time out; 0 replies dispatched, 100 left queued | Pass |
| Poll rate | About 5.3/s | About 4.7/s |
| `Shutdown` | — | 16-21 ms |
| Secure channel | Fails (AES transform) | **Pass:** secure after about 2 s; encrypted `IdReport` and `DeviceCapabilities` pass |

The secure-channel pass also holds for the published, `TrimMode=full` build, and with a custom SCBK. A minimal browser front end (connection settings, status, ID report and capabilities) also passes clear text, default key and custom key against a simulated device in the page, including a `Shutdown` and reconnect on the same `ControlPanel`.

### Desktop

16 loopback buses at 9600 baud with a 200 ms poll interval, measured over 10 s. `IdReport` latency is over 50 requests on one bus. The prototype columns use native AES.

| | `main` | Prototype, `LineTiming.WaitAsync` | Prototype, `Task.Delay` |
|---|---|---|---|
| Polls/s per bus | 4.90 | 5.00 | 4.90 |
| CPU (% of one core) | 20.6% | **130.4%** | 13.2% |
| Process threads (max) | 44 | 31 | 30 |
| `IdReport` median / p90 | 30.9 / 32.7 ms | 25.9 / 26.6 ms | 30.9 / 46.2 ms |
| `StopConnection` during open back-off | 4506 ms | 1 ms | 1 ms |
| `StopConnection` while polling | 14 ms | 0 ms | 0 ms |
| `Shutdown`, then `StartConnection` on the same panel | Pass | Pass | Pass |

In every variant, starting a connection from a UI-style `SynchronizationContext` raised `ConnectionStatusChanged` off the caller's thread, as today.

### RS-485 hardware

Windows ACU on one USB RS-485 adapter (COM11), the osdp-mcp virtual PD on a second adapter (COM8) on the same bus, in install mode (SCBK-D), secure channel on, 200 ms poll interval. Polling measured over 10 s; `IdReport` over 30 requests.

| Baud | Version | Secure channel ready | Polls/s, answered | `IdReport` median / p90 | CPU | `Shutdown` |
|---|---|---|---|---|---|---|
| 9600 | `main` | 2058 ms | 5.0, 100% | 107 / 147 ms | 1.1% | 219 ms |
| 9600 | Prototype, native AES | 2050 ms | 5.0, 100% | 106 / 124 ms | 2.2% | 130 ms |
| 9600 | Prototype, BouncyCastle forced | 2254 ms | 5.0, 100% | 102 / 122 ms | 3.6% | 158 ms |
| 115200 | `main` | 2062 ms | 5.0, 100% | 41 / 52 ms | 2.2% | 297 ms |
| 115200 | Prototype, native AES | 2039 ms | 4.9, 100% | 46 / 52 ms | 2.5% | 142 ms |
| 115200 | Prototype, BouncyCastle forced | 2043 ms | 5.0, 100% | 43 / 51 ms | 2.0% | 141 ms |

No `IdReport` failed in any run.

**From the browser:** the prototype front end in Chrome, using a Web Serial `IOsdpConnection` on COM11, against the same osdp-mcp PD at 115200. It connected in clear text and then with secure channel (default key): the PD reported the secure channel established, and polls, an ID report and a capabilities request (13 ms) were answered over it. The longest gap between browser frames stayed at 17 ms throughout, so the page never froze. That covers the browser AES fallback, the async polling loop and Web Serial together on real hardware.

Not yet tested: the line quality test plan, a custom SCBK and `osdp_KEYSET` on hardware, OSDP-Embedded as the PD, a .NET Framework consumer of the `netstandard2.0` build, and a real reader (rather than a virtual PD) from the browser.

## Evaluating BouncyCastle

Run on desktop and in the Blazor test app using a local BouncyCastle 2.5.1 build of the prototype. Remaining items are marked **To do**.

1. **Correctness on desktop:** done.
   - FIPS-197 (AES-128 and AES-256 ECB) and SP 800-38A (CBC-AES128 encrypt and decrypt) vectors pass with both native AES and BouncyCastle.
   - 2000 random ECB/CBC cases (AES-128 and AES-256, encrypt and decrypt, 1 to 16 blocks) give identical output from BouncyCastle and native AES.
   - 500 random AES-256-GCM cases (0 to 199 bytes, with and without associated data) give identical output from BouncyCastle and native `AesGcm`, and BouncyCastle rejects tampered tags.
   - The full test suite passes with the BouncyCastle path forced (see [Unit tests](#unit-tests)).
2. **Correctness in the browser:**
   - Done: the same vectors and random cases give the same results as on desktop, as do KMAC256, ML-KEM-768 key generation and ML-DSA-44 signatures. SC1 comes up with both the default key and a custom key, and encrypted ID report and capabilities replies decode.
   - **To do:** SC2 with a pre-shared SCBK, and SC2 after asymmetric pairing. These need the SC2 branch with change 6 and the AES-GCM fix applied.
3. **Speed:**

   | | SC1 message (MAC 4 blocks, encrypt and decrypt 3 blocks) | SC2 message (AES-GCM 64 bytes plus KMAC256) | One pairing (ML-KEM key generation, encapsulation and decapsulation; ML-DSA key generation, sign and verify for both sides) |
   |---|---|---|---|
   | Desktop, native AES | 0.005 ms | — | 5.7 ms |
   | Desktop, BouncyCastle | 0.010 ms | 0.020 ms | 4.9 ms |
   | Browser, debug build | 0.141 ms | 0.377 ms | 81 ms |
   | Browser, published (`TrimMode=full`) | 0.034 ms | 0.059 ms | 9.8 ms |

   A 64-byte message takes about 5.6 ms on the wire at 115200 baud, so crypto time is negligible even in the browser. Browser AOT was not measured; it isn't needed. **To do:** compare with the `OSDP-SC2-Benchmark` desktop measurements.
4. **Download size:** done; see the download size note in change 6.

The suggested bar for going ahead with BouncyCastle is met:
- Results match the native implementation byte for byte.
- Per-message crypto time is well under the time the message takes on the wire at 115200 baud.
- Pairing crypto takes about 10 ms in the browser.
- The trimmed size is 8 KB Brotli for AES only and 105 KB with the SC2 primitives, but only with `TrimMode=full`.

## Verification plan

1. **Reproduce:** done. See [Why it fails today](#why-it-fails-today).
2. **Fix:** changes 1-6 are on `feature/browser-wasm`, with change 6 as the `BouncyCastleAes` subclass. Still to do: the SC2 AES-GCM fix.
3. **Regression:** the test suite (native and BouncyCastle forced), the desktop loopback comparison, and secure channel on RS-485 at 9600 and 115200 baud are done. Still to do: the line quality test plan, and a run with OSDP-Embedded as the PD.
4. **WASM:** plain-text and SC1 ID report and device capabilities pass. SC2 needs the AES-GCM fix and a run on the SC2 branch.
5. **Hardware in the browser:** connect, ID request, capabilities and SC1 secure channel pass in Chrome with a prototype Web Serial `IOsdpConnection` against a virtual PD over RS-485 (see [RS-485 hardware](#rs-485-hardware)). Still to do: OSDP Bench builds its own Web Serial connection and tests against a real reader in Chrome and Edge.

## Alternatives considered

- **Multithreaded WebAssembly** (`WasmEnableThreads`): this would leave OSDP.Net's waits unchanged. It is still experimental, needs cross-origin isolation headers on the hosting site, still forbids blocking on the main thread, and doesn't fix AES, so it was not pursued.
- **OSDP-Embedded compiled to WebAssembly:**
  - Its ACU is non-blocking and tick-driven, which suits a browser well.
  - OSDP Bench Core is built on OSDP.Net types (29 Core files use them, including tracing, discovery, line quality and command/reply data). Switching would effectively mean a separate app.
  - OSDP-Embedded also lacks ACU-side file transfer, automatic polling, multi-baud discovery, and line quality, all of which Bench uses.
- **Local agent exposing the serial port over WebSocket:** works in every browser and leaves OSDP.Net unchanged, but requires an install, which removes most of the appeal of a web version.

## Open questions

- Is a browser target worth supporting as a first-class scenario, or should it be best-effort? A cheap middle ground is to build with `<SupportedPlatform Include="browser" />` in CI so CA1416 catches new browser-unsupported APIs, and to add the WASM smoke test only if Bench ships.
- ~~Should the reply queue use `System.Threading.Channels` or `ConcurrentQueue` + `SemaphoreSlim`?~~ Use Channels: on `netstandard2.0` it adds one package, since its dependencies are already present.
- ~~Should the BouncyCastle fallback be used only in the browser, or everywhere `Aes.Create()` fails?~~ Everywhere native AES fails: the transform probe in change 6 covers the browser and any other runtime without AES.
- ~~Should change 6 be the `AesProvider` methods or the `BouncyCastleAes` subclass?~~ The subclass: it is the version tested on hardware and leaves every call site unchanged.
- Should browser secure-channel support wait for SC2 to merge, so the AES fallback is written once on top of BouncyCastle?
- Does the PD side (`LineTiming.WaitAsync` spin, the connection listeners) need the same treatment for a future browser-hosted PD simulator, or is the control panel enough for now?
