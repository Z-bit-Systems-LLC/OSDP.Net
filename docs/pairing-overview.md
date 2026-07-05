# OSDP Asymmetric Device Pairing (SC2) Implementation Guide

A language-agnostic reference for the OSDP.Net **asymmetric device pairing** profile: an
EDHOC-style, post-quantum key agreement that establishes the 32-byte SC2 Secure Channel Base Key
(SCBK) out-of-band, replacing the shared-symmetric-key model and Installation Mode / SCBK-D.

> **Status: experimental.** This profile implements the direction proposed to the OSDP Technical
> Subcommittee (SC2 Pairing Discussion, June 2026). The command/reply codes and wire formats here
> are **not** SIA-assigned. It is intended for proof-of-concept and interoperability experiments.

## Table of Contents

1. [Introduction](#1-introduction)
2. [Relationship to SC2](#2-relationship-to-sc2)
3. [Cryptographic Primitives](#3-cryptographic-primitives)
4. [C.509 Certificate Profile](#4-c509-certificate-profile)
5. [Handshake Protocol](#5-handshake-protocol)
6. [Transcript and Key Schedule](#6-transcript-and-key-schedule)
7. [Transport and Framing](#7-transport-and-framing)
8. [Failure Handling](#8-failure-handling)
9. [Test Vectors](#9-test-vectors)
10. [API Usage](#10-api-usage)
11. [Console Demonstration](#11-console-demonstration)
12. [Implementation Checklist](#12-implementation-checklist)

---

## 1. Introduction

Symmetric SC2 (see [sc2-overview.md](sc2-overview.md)) proves that both the ACU and PD hold the
same secret (the SCBK) but provides no cryptographic device identity: a reader can be swapped for
another without detection, and the PD cannot verify it is talking to an acceptable ACU. Asymmetric
pairing addresses this by giving each device a certified key pair and running a mutually
authenticated key exchange whose only output is the SCBK. After pairing, the standard symmetric SC2
handshake runs unchanged with the derived key.

Design goals:

- **Cryptographic device identity** via per-device certificates (802.1AR IDevID subject).
- **Mutual authentication**: each side verifies the other's certificate and a signature over the
  session transcript.
- **Post-quantum from day one**: ML-KEM-768 (FIPS 203) for key agreement, ML-DSA-44 (FIPS 204) for
  signatures. No classical primitives.
- **No Installation Mode**: the SCBK is established out-of-band; there is no SCBK-D for SC2.

## 2. Relationship to SC2

Pairing is a **separate, cleartext exchange** that precedes any secure channel. It does not modify
the SC2 record layer or handshake. The complete flow is:

```
1. ACU adds the device unsecured and polls it in cleartext.
2. ACU runs the pairing exchange (osdp_PAIR / osdp_PAIRR) -> both sides derive a 32-byte SCBK.
3. ACU re-adds the device with SecureChannelVersion.V2 and the derived SCBK.
4. The standard SC2 handshake (CHLNG / CCRYPT / SCRYPT / RMAC_I) runs with the paired SCBK.
5. All subsequent traffic uses AES-256-GCM as described in sc2-overview.md.
```

Pairing is strictly opt-in. A PD with no pairing configuration behaves exactly as a symmetric-only
device and NAKs the pairing command; pure pre-shared-key SC2 (and SC1) deployments are unaffected.

### Deterministic cleartext-to-SC2 handoff (single connection)

Steps 2-4 above happen over the **same** connection, and the transition is deterministic — no
timing delays and no reconnect on the PD:

- When the PD processes message 3 it derives the SCBK, sends the single-fragment **Result reply in
  the clear**, and only then activates the derived key on its **running** SC2 channel in place
  (switching that channel to full security). Because the activation happens strictly after the
  Result is sent, the PD is guaranteed to be ready before the ACU issues its first `CHLNG`.
- The ACU receives the Result, completes pairing, and immediately re-adds the device under SC2. The
  protocol round-trip (PD activates -> Result -> ACU receives -> ACU challenges) enforces the
  ordering, so the SC2 handshake establishes without any sleep or PD restart.

To keep the switch to secure messaging fast, the ACU re-adds the paired device with a short
**fast-connect window** (`AddDevice(..., skipConnectBackoff: TimeSpan.FromSeconds(3))`). A freshly
added device is not yet "connected", so the bus normally throttles it with a one-second offline
back-off between each secure channel handshake step, stretching establishment across a couple of
seconds and making the transition look like a reconnect. Because pairing just proved the PD is
present, the window lets the `CHLNG`/`CCRYPT`/`SCRYPT` handshake run at the 200 ms poll cadence
instead. The window is bounded, so the normal back-off resumes automatically, and it is opt-in — a
device added the usual way (pre-shared-key SC2) behaves exactly as before.

The `Pairing_ThenSc2OnSameConnection_EstablishesInPlaceWithoutReconnect` and
`Pairing_ThenSc2WithFastConnectWindow_EstablishesPromptly` integration tests cover this live path.

## 3. Cryptographic Primitives

| Primitive | Algorithm | Sizes (bytes) |
|---|---|---|
| Key encapsulation | ML-KEM-768 (FIPS 203) | seed 64, public key 1184, ciphertext 1088, shared secret 32 |
| Signatures | ML-DSA-44 (FIPS 204), deterministic | seed 32, public key 1312, signature 2420 |
| Key derivation | HKDF-SHA256 (RFC 5869) | 32-byte outputs |
| Transcript hash | SHA-256 | 32 |
| Key confirmation | HMAC-SHA256 | 32 |

All primitives are provided by BouncyCastle on every target framework for a single deterministic
code path. ML-DSA signing is deterministic so demonstration certificates and test vectors are
reproducible.

## 4. C.509 Certificate Profile

A certificate is a deterministic (canonical) CBOR two-element array `[ TBS, signature ]`. The TBS is
an 8-element array:

```
TBS = [
  version:          1                     ; uint
  serialNumber:     bstr(8)               ; issuer-assigned
  issuer:           tstr                  ; CA common name, or "self" for self-signed
  validity:         [ notBefore, notAfter ]   ; two uints, Unix seconds
  subject:          [ manufacturer, model, serialNumber ]   ; three tstr (802.1AR IDevID)
  publicKeyAlg:     1                     ; uint, 1 = ML-DSA-44
  publicKey:        bstr(1312)            ; ML-DSA-44 subject public key
  signatureAlg:     1                     ; uint, 1 = ML-DSA-44
]
signature = ML-DSA-44.Sign(issuerPrivateKey, "OSDP-C509-v1" || TBS_encoded)   ; deterministic
```

- **Encoding** is canonical CBOR (definite lengths, shortest integer form) so the encoding of a
  certificate is unique. Signatures and thumbprints are computed over this encoding.
- **Thumbprint** = SHA-256 of the full canonical certificate encoding. Used for by-reference
  credential presentation once a peer has cached the certificate.
- **Self-signed** certificates set `issuer = "self"` and are signed by the subject key; a peer
  trusts them by pinning their thumbprint.
- **Demonstration CA**: `CertificateAuthority.Demo()` is derived from the fixed 32-byte seed
  `40 41 … 5F` (identical to the well-known SC2 demo key — never use in production). It lets a demo
  ACU and PD trust each other with no configuration.

## 5. Handshake Protocol

Three messages plus a result, ACU = initiator, PD = responder. `||` is concatenation.

```
Message 1  (ACU -> PD)
  version(1) | suite(1)=0x01 | nonce_A(16) | ek_A(1184 ML-KEM encaps key)
  | credType_A(1) | cred_A            ; credential: full cert or 32-byte thumbprint

Message 2  (PD -> ACU)
  core = [ nonce_P(16) | ct(1088 ML-KEM ciphertext) | credType_P(1) | cred_P ]
  sig_P(2420) = ML-DSA-44.Sign(sk_P, "OSDP-PAIR-v1-msg2" || TH2)
  mac_P(32)   = HMAC-SHA256(K_m2, TH2)

Message 3  (ACU -> PD)
  sig_A(2420) = ML-DSA-44.Sign(sk_A, "OSDP-PAIR-v1-msg3" || TH3)
  mac_A(32)   = HMAC-SHA256(K_m3, TH3)

Result     (PD -> ACU)
  status(1)   ; 0x00 success, 0x01 auth-fail, 0x02 persist-fail, 0x03 policy, 0x04 protocol
  mac_R(32)   = HMAC-SHA256(K_m4, TH4)   ; present only on success
```

Mutual authentication:

- The **PD** validates `cred_A` against its trust anchor when processing message 1, and verifies
  `sig_A` over TH3 in message 3.
- The **ACU** validates `cred_P` against its trust anchor and verifies `sig_P` over TH2 in message 2.
- The **MACs** prove possession of the ML-KEM shared secret: signatures bind identity, MACs bind the
  key. The PD encapsulates to `ek_A` (producing `ct` and the shared secret); the ACU decapsulates
  `ct`. A wrong or corrupted ciphertext yields a different shared secret (ML-KEM implicit rejection),
  which is caught when `mac_P` fails to verify.

Neither side commits the SCBK until the final confirmation: the PD commits only after persisting the
key and sending `status = success`; the ACU commits only after verifying `mac_R`.

## 6. Transcript and Key Schedule

```
TH1 = SHA-256(message1_wire_bytes)          ; includes the 1-byte message type tag
TH2 = SHA-256(TH1 || message2_core_bytes)   ; core = the CBOR array above, minus sig_P and mac_P
TH3 = SHA-256(TH2 || sig_P || mac_P)
TH4 = SHA-256(TH3 || sig_A || mac_A)

ss   = ML-KEM-768 shared secret (32 bytes)
PRK  = HKDF-Extract(salt = TH2, ikm = ss)
K_m2 = HKDF-Expand(PRK, "osdp-pair confirm2", 32)
K_m3 = HKDF-Expand(PRK, "osdp-pair confirm3", 32)
K_m4 = HKDF-Expand(PRK, "osdp-pair confirm4", 32)

SCBK = HKDF-Expand(HKDF-Extract(salt = TH4, ikm = ss), "osdp-pair scbk", 32)
```

The SCBK is bound to TH4, which covers the full transcript (both nonces, both certificates, both
signatures and confirmation MACs), so the derived key is unique to this exact pairing.

## 7. Transport and Framing

Pairing rides two experimental application-level commands, sent in cleartext:

| Code | Name | Direction |
|---|---|---|
| `0xB0` | osdp_PAIR | ACU -> PD (command) |
| `0x8A` | osdp_PAIRR | PD -> ACU (reply) |

> Codes `0xB0` / `0x8A` avoid the OSDP 3.0 PIV draft's tentative command block (`0xA6`–`0xAF`) and
> reply block (`0x84`–`0x89`). They are experimental and not SIA-assigned.

Each pairing message is fragmented using the same little-endian multi-part framing as osdp_CRAUTH /
osdp_CRAUTHR:

```
osdp_PAIR payload:              osdp_PAIRR payload:
  totalSize    (2, LE)            wholeMessageLength (2, LE)
  offset       (2, LE)            offset             (2, LE)
  fragmentSize (2, LE)            lengthOfFragment   (2, LE)
  fragment bytes                  data
```

The reassembled payload is `messageType(1) || CBOR body`, where messageType is 0x01–0x04. The PD
acknowledges each inbound fragment; once a message is complete it runs the responder state machine
and queues the fragmented response for delivery on subsequent polls (the ACU sets the bus into
fast-poll multipart mode during the exchange). A message-1 first fragment always resets the PD
session (retry-friendly); a 30-second inactivity timeout discards a stalled session.

Approximate sizes: message 1 ≈ 5.3 KB, message 2 ≈ 7.7 KB, message 3 ≈ 2.5 KB, result ≈ 60 B. On
TCP/loopback the full exchange completes in under two seconds; at 9600 baud it takes roughly
15–20 seconds.

## 8. Failure Handling

| Condition | Behavior |
|---|---|
| PD not configured for pairing | NAK (Unknown Command Code); ACU raises `PairingException(NotSupported)` |
| Peer certificate rejected by trust anchor | Verifying side aborts; ACU raises `PeerCertificateRejected` |
| Peer signature / MAC invalid | PD returns `Result(auth-fail)`; ACU raises the reported status |
| ML-KEM shared-secret mismatch | Caught by `mac_P` verification failure |
| PD persistence callback fails | PD returns `Result(persist-fail)`; neither side commits the key |
| Re-pairing while `RePairingPolicy.Deny` and a key exists | PD returns `Result(policy)` |
| Out-of-order or malformed message | `Result(protocol)` / `PairingException(ProtocolError)` |

## 9. Test Vectors

All values hexadecimal. These are asserted by the unit tests and guard against cryptographic-library
changes.

### 9.1 Demonstration CA

```
Demo CA seed  = 40 41 42 43 44 45 46 47 48 49 4A 4B 4C 4D 4E 4F
                50 51 52 53 54 55 56 57 58 59 5A 5B 5C 5D 5E 5F

SHA-256(demo CA ML-DSA-44 public key) =
  6C1C65071979225A139B3EC84688E2688EC30FABE8CC510CB688BC435F2D3CB9
```

### 9.2 ML-KEM public key (seed 0x00..0x3F)

```
SHA-256(ML-KEM-768 public key) =
  0B7934C83125C788995E2BA6BD761E33046B3E40571BE53E023309A29F398CC9
```

### 9.3 HKDF (RFC 5869 Test Case 1, SHA-256)

```
IKM  = 0B x22   salt = 000102030405060708090A0B0C   info = F0F1F2F3F4F5F6F7F8F9   L = 42
PRK  = 077709362C2E32DF0DDC3F0DC47BBA6390B6C73BB50F9C3122EC844AD7C2B3E5
OKM  = 3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865
```

### 9.4 Key schedule (fixed inputs)

```
ss  = 00 01 .. 1F    TH2 = 20 21 .. 3F    TH4 = 40 41 .. 5F

K_m2 = 94151F36DE9FEB1CC8C74D7D846FBE5EA7C5CA7FC18979623D94C890ECEAD7AB
K_m3 = BA43E76D8870ED58D77636D397D7D722513E879026A3021F6FDD07C023384829
K_m4 = E542E59444C0776CE69DEA4FABC862F2ABD6782A3B7D7297F7E5F418D5DDF87A
SCBK = 8EAF7FD9DE1332FD2F3F18378B8AFB81E90E83238BA324CB7BDC3F38146835D4
```

The end-to-end SCBK of a live exchange is not a fixed vector because ML-KEM encapsulation is
randomized; conformance is instead verified by (a) the deterministic key schedule above and (b) the
ACU and PD independently deriving identical SCBKs.

## 10. API Usage

```csharp
// Shared trust: a demo CA both sides trust (production supplies its own CA / trust anchor).
var ca = CertificateAuthority.Demo();

// ACU side
var acuConfig = new PairingConfiguration(
    PairingCredentials.Generate(new DeviceIdentity("ACME Controllers", "ACU-9", "ACU-0001"), ca),
    PairingTrustAnchor.FromCa(ca));

panel.AddDevice(connectionId, address, useCrc: true, useSecureChannel: false); // cleartext

// Optional IProgress<PairingProgress> drives a progress bar over the ~4s exchange.
var progress = new Progress<PairingProgress>(p =>
    Console.WriteLine($"{p.Stage}: {p.Fraction:P0}"));
PairingResult result = await panel.PairDevice(connectionId, address, acuConfig, progress: progress);

// Re-add under SC2. The fast-connect window lets the handshake run at poll speed instead of the
// per-step offline back-off, since pairing just proved the PD is present.
panel.AddDevice(connectionId, address, true, true, result.Scbk, SecureChannelVersion.V2,
    skipConnectBackoff: TimeSpan.FromSeconds(3)); // SC2

// PD side (on the DeviceConfiguration)
config.Pairing = new PairingConfiguration(
    PairingCredentials.Generate(new DeviceIdentity("ACME Access", "AR-200", "PD-0001"), ca),
    PairingTrustAnchor.FromCa(ca))
{
    // The callback receives the full PairingResult (derived SCBK plus the authenticated peer
    // identity/certificate), so the PD can report who it paired with. Return true once stored.
    OnScbkEstablished = async (pairingResult, ct) =>
    {
        await PersistScbk(pairingResult.Scbk);
        Log($"Paired with {pairingResult.PeerIdentity}");
        return true;
    }
};
```

Leaving `DeviceConfiguration.Pairing` null keeps the device symmetric-only. The PD activates the
derived key on its running SC2 channel in place after the exchange (see
[Section 2](#deterministic-cleartext-to-sc2-handoff-single-connection)), so no PD restart is needed.

## 11. Console Demonstration

1. **PDConsole**: choose **Device → Activate** and, in the activation dialog, set **Security** to
   **Pairing (asymmetric)** (optionally supply a device **Seed** for a reproducible identity), then
   start. The mode can also be preset via `Security.SecureChannelMode = "Pairing"` in
   `appsettings.json`. The PD listens in cleartext and waits for a pairing exchange; its Device Status
   shows `Security: Pairing`.
2. **ACUConsole**: start a connection, then choose **Devices → Pair (Asymmetric)** and select the
   device. A progress dialog shows the exchange advancing to 100%.
3. On success: the ACUConsole logs the authenticated PD identity and certificate thumbprint and
   switches the device to SC2 with the derived key; the PDConsole logs a pairing entry with the ACU
   identity/thumbprint and its Device Status flips to `Security: Secure`. The SC2 secure channel then
   establishes in place over the same link, and subsequent traffic is AES-256-GCM encrypted.

## 12. Implementation Checklist

- [ ] ML-KEM-768 encapsulate / decapsulate; deterministic seeded keygen for tests
- [ ] ML-DSA-44 deterministic sign / verify; seeded keygen (demo CA reproducibility)
- [ ] HKDF-SHA256 (RFC 5869) extract and expand
- [ ] Canonical CBOR writer/reader (definite lengths, shortest integer form)
- [ ] C.509 encode/decode, signature over `"OSDP-C509-v1" || TBS`, SHA-256 thumbprint
- [ ] Trust anchor validation: CA-signature and pinned-thumbprint modes
- [ ] Message 1–3 and Result encode/parse with the message-type tag
- [ ] Transcript hashes TH1–TH4 and the HKDF key schedule
- [ ] ACU state machine: create msg1 → process msg2 → process result
- [ ] PD state machine: process msg1 → process msg3 → build result after persistence
- [ ] osdp_PAIR (0xB0) / osdp_PAIRR (0x8A) fragmentation and reassembly
- [ ] Opt-in gating: NAK pairing when unconfigured; keep symmetric-only SC2 unchanged
- [ ] Re-pairing policy and 30-second session timeout
