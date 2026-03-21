# OSDP Secure Channel v2 (SC2) Implementation Guide

A language-agnostic reference for implementing OSDP Secure Channel v2. This document covers the complete SC2 protocol: cryptographic primitives, handshake flow, encrypted message format, and test vectors.

## Table of Contents

1. [Introduction](#1-introduction)
2. [SC2 vs SC1 at a Glance](#2-sc2-vs-sc1-at-a-glance)
3. [Cryptographic Primitives](#3-cryptographic-primitives)
4. [Key Derivation](#4-key-derivation)
5. [Handshake Protocol](#5-handshake-protocol)
6. [Nonce Computation](#6-nonce-computation)
7. [Encrypted Message Format](#7-encrypted-message-format)
8. [Counter Management](#8-counter-management)
9. [Constants Reference](#9-constants-reference)
10. [Test Vectors](#10-test-vectors)
11. [Implementation Checklist](#11-implementation-checklist)

---

## 1. Introduction

OSDP Secure Channel v2 (SC2) is a security upgrade for the Open Supervised Device Protocol. It replaces SC1's AES-128 CBC encryption and CBC-MAC authentication with AES-256 GCM authenticated encryption and KMAC256 key derivation.

Key improvements over SC1:
- **Stronger encryption**: AES-256 (vs AES-128)
- **Authenticated encryption**: GCM provides confidentiality and integrity in a single operation
- **Larger key material**: 32-byte keys and 16-byte random numbers
- **No padding required**: GCM handles arbitrary-length plaintext
- **Full 16-byte authentication tag**: GCM tag replaces the truncated 4-byte CBC-MAC
- **Associated Authenticated Data (AAD)**: Message headers are integrity-protected without encryption

SC2 follows the same 4-step handshake pattern as SC1 (CHLNG, CCRYPT, SCRYPT, RMAC_I) but with different field sizes, key derivation, and post-establishment message format.

## 2. SC2 vs SC1 at a Glance

| Aspect | SC1 | SC2 |
|---|---|---|
| Encryption | AES-128 CBC | AES-256 GCM |
| Key derivation | AES-128 ECB (4 derivations) | KMAC256 (2 derivations) |
| Base key (SCBK) | 16 bytes | 32 bytes |
| Session keys | 16 bytes each (S-ENC, S-MAC1, S-MAC2) | 32 bytes each (S-ENC, S-NONCE) |
| Random numbers | 8 bytes (RndA, RndB) | 16 bytes (RndA, RndB) |
| Cryptograms | 16 bytes | 32 bytes |
| Authentication | CBC-MAC, truncated to 4 bytes | GCM tag, 16 bytes |
| Message type byte | Clear in header | Encrypted inside ciphertext |
| Nonce/IV | Rolling RMAC/CMAC as IV | 12-byte nonce from UID + counter |
| Payload padding | Required (0x80 + 0x00 fill to AES block boundary) | Not needed |
| RMAC computation | Yes | No (nonce is deterministic) |
| Default key support | Yes (SCBK-D) | No |
| Session limit | None | 500,000,000 messages |

## 3. Cryptographic Primitives

SC2 uses three cryptographic operations:

### 3.1 KMAC256 (Key Derivation)

Used to derive session keys from the base key and handshake random numbers.

```
Output = KMAC256(Key, Data, OutputBitLength, CustomizationString)
```

| Parameter | Value |
|---|---|
| Algorithm | KMAC256 (Keccak Message Authentication Code, 256-bit security) |
| Key | SCBK (32 bytes) |
| Output length | 256 bits (32 bytes) |
| Customization string | `""` (empty) |

KMAC256 is defined in NIST SP 800-185. Common library implementations:
- .NET 10+: `System.Security.Cryptography.Kmac256` (native, where platform supports it)
- BouncyCastle: `KMac(256, customization)` with `KeyParameter`
- Python: `pycryptodome` or manual Keccak
- Go: `golang.org/x/crypto/sha3`

### 3.2 AES-256 GCM (Message Encryption)

Used for authenticated encryption of all post-handshake messages.

| Parameter | Value |
|---|---|
| Algorithm | AES-256 in GCM (Galois/Counter Mode) |
| Key | S-ENC (32 bytes) |
| Nonce | 12 bytes (computed deterministically, see [Section 6](#6-nonce-computation)) |
| Tag size | 16 bytes (128 bits, full GCM tag) |
| AAD | Message header bytes (SOM through security block) |
| Plaintext | Command/reply byte + payload (arbitrary length, no padding) |

```
Encrypt(key=S-ENC, nonce, plaintext, aad) -> (ciphertext, tag)
Decrypt(key=S-ENC, nonce, ciphertext, tag, aad) -> plaintext  (or authentication failure)
```

### 3.3 AES-256 CBC (Handshake Cryptograms and Nonce Derivation)

Used during the handshake to compute cryptograms and to derive GCM nonces.

| Parameter | Value |
|---|---|
| Algorithm | AES-256 in CBC mode |
| Key | S-ENC (for cryptograms) or S-NONCE (for nonce derivation) |
| IV | All zeros (`00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`) |
| Padding | None (input is always exactly 16 or 32 bytes) |

## 4. Key Derivation

Session keys are derived from the Secure Channel Base Key (SCBK) and the two random numbers exchanged during the handshake.

### Session Encryption Key (S-ENC)

```
S-ENC = KMAC256(SCBK, RndA || RndB, 256, "")
```

- Input: 32 bytes (RndA concatenated with RndB)
- Output: 32 bytes

### Session Nonce Key (S-NONCE)

```
S-NONCE = KMAC256(SCBK, RndB || RndA, 256, "")
```

- Input: 32 bytes (RndB concatenated with RndA, reversed order from S-ENC)
- Output: 32 bytes

### Cryptogram Computation

Both client (PD) and server (ACU) cryptograms are computed using AES-256-CBC:

```
ClientCryptogram = AES-256-CBC-Encrypt(key=S-ENC, iv=zeros, plaintext=RndA || RndB)
ServerCryptogram = AES-256-CBC-Encrypt(key=S-ENC, iv=zeros, plaintext=RndB || RndA)
```

- Input: 32 bytes (two 16-byte random numbers concatenated)
- Output: 32 bytes (two AES blocks)

## 5. Handshake Protocol

The SC2 handshake is a 4-message exchange that establishes session keys and mutually authenticates the ACU and PD.

### 5.1 Step 1: osdp_CHLNG (ACU -> PD)

The ACU initiates the secure channel by sending a challenge with a 16-byte random number.

**Wire format:**
```
[0]      SOM (0x53)
[1]      Address (0x00-0x7F)
[2:3]    Length (little-endian)
[4]      Control byte (SCB bit set)
[5]      SCB length = 0x03
[6]      SCB type = 0x11 (SCS_11: Begin New Secure Connection)
[7]      SCB data = 0x02 (SC2 indicator)
[8]      Command code = 0x76 (osdp_CHLNG)
[9:24]   RndA (16 bytes, server random number)
[25:26]  CRC-16
```

**Total length: 27 bytes**

The SC2 indicator byte (`0x02`) in `SCB data[0]` distinguishes SC2 from SC1:
- `0x00` = SC1 with default key (SCBK-D)
- `0x01` = SC1 with device-specific key
- `0x02` = SC2

**ACU actions:**
1. Generate a cryptographically secure 16-byte random number (RndA)
2. Store RndA for later use in key derivation

### 5.2 Step 2: osdp_CCRYPT (PD -> ACU)

The PD responds with its unique identifier, its own random number, and a cryptogram proving it holds the correct SCBK.

**Wire format:**
```
[0]      SOM (0x53)
[1]      Address (0x80 | pd_address)
[2:3]    Length (little-endian)
[4]      Control byte (SCB bit set)
[5]      SCB length = 0x03
[6]      SCB type = 0x12 (SCS_12: Sequence Step 2)
[7]      SCB data = 0x02
[8]      Reply code = 0x76 (osdp_CCRYPT)
[9:16]   Client UID (8 bytes)
[17:32]  RndB (16 bytes, client random number)
[33:64]  Client Cryptogram (32 bytes)
[65:66]  CRC-16
```

**Total length: 67 bytes** (payload = 56 bytes: 8 + 16 + 32)

**PD actions:**
1. Verify `SCB data[0] == 0x02` (SC2 supported)
2. Extract RndA from the CHLNG payload
3. Generate a cryptographically secure 16-byte random number (RndB)
4. Derive session keys: `S-ENC = KMAC256(SCBK, RndA || RndB, 256, "")` and `S-NONCE = KMAC256(SCBK, RndB || RndA, 256, "")`
5. Compute client cryptogram: `AES-256-CBC(S-ENC, iv=0, RndA || RndB)` (32 bytes)
6. Pre-compute expected server cryptogram: `AES-256-CBC(S-ENC, iv=0, RndB || RndA)` (32 bytes)
7. Send Client UID, RndB, and client cryptogram

### 5.3 Step 3: osdp_SCRYPT (ACU -> PD)

The ACU validates the client cryptogram and responds with its own cryptogram.

**Wire format:**
```
[0]      SOM (0x53)
[1]      Address (0x00-0x7F)
[2:3]    Length (little-endian)
[4]      Control byte (SCB bit set)
[5]      SCB length = 0x03
[6]      SCB type = 0x13 (SCS_13: Sequence Step 3)
[7]      SCB data = 0x02
[8]      Command code = 0x77 (osdp_SCRYPT)
[9:40]   Server Cryptogram (32 bytes)
[41:42]  CRC-16
```

**Total length: 43 bytes**

**ACU actions:**
1. Extract Client UID, RndB, and client cryptogram from osdp_CCRYPT
2. Derive session keys using stored RndA and received RndB (same formulas as PD)
3. Validate client cryptogram:
   - Compute expected: `AES-256-CBC(S-ENC, iv=0, RndA || RndB)`
   - Compare with received cryptogram; if mismatch, abort (send NAK)
4. Compute server cryptogram: `AES-256-CBC(S-ENC, iv=0, RndB || RndA)` (32 bytes)
5. Store Client UID (needed for nonce computation)
6. Send server cryptogram

### 5.4 Step 4: osdp_RMAC_I (PD -> ACU)

The PD validates the server cryptogram and confirms the secure channel is established.

**Wire format:**
```
[0]      SOM (0x53)
[1]      Address (0x80 | pd_address)
[2:3]    Length (little-endian)
[4]      Control byte (SCB bit set)
[5]      SCB length = 0x03
[6]      SCB type = 0x14 (SCS_14: Sequence Step 4)
[7]      SCB data = 0x01 (cryptogram accepted)
[8]      Reply code = 0x78 (osdp_RMAC_I)
         (no payload — SC2 does not compute an RMAC)
[9:10]   CRC-16
```

**Total length: 11 bytes** (empty payload for SC2; SC1 would have 16 bytes of RMAC)

**PD actions:**
1. Validate server cryptogram against the expected value computed in Step 2
2. If mismatch, send NAK with error code "Does not support security block"
3. If valid, establish the secure channel:
   - Set message counter = 0
   - Mark `IsSecurityEstablished = true`

**ACU actions (after receiving osdp_RMAC_I):**
1. Verify `SCB data[0] == 0x01` (cryptogram accepted)
2. Establish the secure channel:
   - Set message counter = 0
   - Mark `IsSecurityEstablished = true`

After Step 4, all subsequent messages are encrypted with AES-256 GCM.

## 6. Nonce Computation

SC2 uses a deterministic 12-byte GCM nonce derived from the Client UID and a monotonically increasing message counter. A new nonce is computed for every message (command or reply).

### Algorithm

```
Step 1: Build 16-byte input
  input[0:7]   = Client UID (8 bytes)
  input[8:11]  = Counter (4 bytes, little-endian)
  input[12]    = 0x80
  input[13:15] = 0x00, 0x00, 0x00

Step 2: Encrypt with AES-256-CBC
  encrypted = AES-256-CBC(key=S-NONCE, iv=zeros, plaintext=input)

Step 3: Truncate to 12 bytes
  nonce = encrypted[0:11]
```

### Counter Rules

- The counter starts at 0 after establishment (Step 4)
- The counter increments after each message is encrypted or decrypted
- Both ACU and PD maintain the same counter (it counts all messages in both directions)
- The counter is shared across directions: TX at counter=0, RX at counter=1, TX at counter=2, etc.

## 7. Encrypted Message Format

Once the secure channel is established, all commands and replies use the following format.

### 7.1 SC2 Command (ACU -> PD)

```
Byte layout:
[0]          SOM (0x53)
[1]          Address (0x00-0x7F)
[2:3]        Length (little-endian, total packet length)
[4]          Control byte (seq bits, CRC bit, SCB bit set)
[5]          SCB length = 0x02
[6]          SCB type = 0x17 (SCS_17: Command with Data Security)
[7:N-18]     Ciphertext (encrypted command byte + payload)
[N-18:N-2]   GCM authentication tag (16 bytes)
[N-2:N]      CRC-16
```

### 7.2 SC2 Reply (PD -> ACU)

```
Byte layout:
[0]          SOM (0x53)
[1]          Address (0x80 | pd_address)
[2:3]        Length (little-endian, total packet length)
[4]          Control byte (seq bits, CRC bit, SCB bit set)
[5]          SCB length = 0x02
[6]          SCB type = 0x18 (SCS_18: Reply with Data Security)
[7:N-18]     Ciphertext (encrypted reply byte + payload)
[N-18:N-2]   GCM authentication tag (16 bytes)
[N-2:N]      CRC-16
```

### 7.3 Encryption Details

**Plaintext composition:**
```
plaintext[0]     = Command or reply code (e.g., 0x60 for Poll, 0x40 for Ack)
plaintext[1:...] = Payload data (variable length, may be empty)
```

**Associated Authenticated Data (AAD):**
```
aad = bytes[0:6]  (SOM + Address + Length + Control + SCB)
```

The AAD covers the message header. It is authenticated by GCM but not encrypted, meaning a receiver can parse the header before decryption.

**Encryption call:**
```
nonce = ComputeNonce(counter)
(ciphertext, tag) = AES-256-GCM-Encrypt(S-ENC, nonce, plaintext, aad)
counter++
```

**Key difference from SC1:** In SC1, the command/reply code byte is in the clear header after the SCB. In SC2, the command/reply code is the first byte of the plaintext and becomes part of the ciphertext. This means the message type cannot be determined without decryption.

### 7.4 Minimum Packet Size

For a command with no payload (e.g., Poll):
```
SOM(1) + ADDR(1) + LEN(2) + CTRL(1) + SCB(2) + ciphertext(1) + tag(16) + CRC(2) = 26 bytes
```

## 8. Counter Management

| Property | Value |
|---|---|
| Initial value | 0 (set by `Establish()` after RMAC_I) |
| Increment | After each `EncodePayload` or `DecodePayload` call |
| Size | 32-bit unsigned integer |
| Byte order | Little-endian (in nonce computation) |
| Terminal count | 500,000,000 |

When the counter reaches 500,000,000, the session must be re-established by performing a new handshake. Implementations should throw an error or signal the application to re-key.

## 9. Constants Reference

### Sizes (bytes)

| Constant | Size | Description |
|---|---|---|
| SCBK | 32 | Secure Channel Base Key |
| S-ENC | 32 | Session encryption key |
| S-NONCE | 32 | Session nonce key |
| RndA | 16 | Server random number |
| RndB | 16 | Client random number |
| Client UID | 8 | PD unique identifier |
| GCM nonce | 12 | Per-message nonce |
| GCM tag | 16 | Authentication tag |
| Cryptogram | 32 | Handshake cryptogram |

### Security Block Types (SCS values)

| Hex | Name | Usage |
|---|---|---|
| `0x11` | SCS_11 | osdp_CHLNG (begin secure connection) |
| `0x12` | SCS_12 | osdp_CCRYPT (step 2) |
| `0x13` | SCS_13 | osdp_SCRYPT (step 3) |
| `0x14` | SCS_14 | osdp_RMAC_I (step 4) |
| `0x15` | SCS_15 | Command, MAC only (no encryption) |
| `0x16` | SCS_16 | Reply, MAC only (no encryption) |
| `0x17` | SCS_17 | Command with data security |
| `0x18` | SCS_18 | Reply with data security |

### SC2 Indicator Values (SCB data byte)

| Value | Meaning |
|---|---|
| `0x00` | SC1, default key (SCBK-D) |
| `0x01` | SC1, device-specific key |
| `0x02` | SC2 |

### OSDP Command/Reply Codes (Handshake)

| Code | Name | Direction |
|---|---|---|
| `0x76` | osdp_CHLNG / osdp_CCRYPT | Command / Reply |
| `0x77` | osdp_SCRYPT | Command |
| `0x78` | osdp_RMAC_I | Reply |

## 10. Test Vectors

These test vectors can be used to validate an SC2 implementation. All values are in hexadecimal.

### 10.1 Input Constants

```
SCBK  = 30 31 32 33 34 35 36 37 38 39 3A 3B 3C 3D 3E 3F
        40 41 42 43 44 45 46 47 48 49 4A 4B 4C 4D 4E 4F

RndA  = A0 A1 A2 A3 A4 A5 A6 A7 A8 A9 AA AB AC AD AE AF

RndB  = B0 B1 B2 B3 B4 B5 B6 B7 B8 B9 BA BB BC BD BE BF

cUID  = C0 C1 C2 C3 C4 C5 C6 C7
```

### 10.2 Derived Session Keys

```
S-ENC   = 11 50 9C 6D 52 76 21 68 11 B0 5A C7 50 1F 6E 82
          0F 34 74 5D FD 17 B0 45 79 8F B5 2E A4 63 47 8F

S-NONCE = 59 0D FE 02 A5 47 9B E0 92 61 A5 F4 2D C9 7A 18
          97 37 7E 2B 0D EC 09 1F 21 29 53 23 75 5F CE A7
```

### 10.3 GCM Nonce at Counter = 0

```
Nonce = 34 F8 D8 E7 B5 3E D9 F5 0D C2 F2 1C
```

### 10.4 Encrypted Message Test Vectors

Each test vector is a complete OSDP packet (SOM through CRC).

**Counter 0 — TX Poll command (ACU -> PD):**
```
Plaintext: 60 (Poll command, no payload)

Full packet: 53 00 1A 00 0D 02 17 80 19 8C BF BF 8D EA E0 7A
             F5 82 C0 57 44 F9 89 0E FF 00

Breakdown:
  Header+SCB: 53 00 1A 00 0D 02 17  (7 bytes AAD)
  Ciphertext: 80                     (1 byte, encrypted 0x60)
  GCM tag:    19 8C BF BF 8D EA E0 7A F5 82 C0 57 44 F9 89 0E
  CRC:        FF 00
```

**Counter 1 — RX Ack reply (PD -> ACU):**
```
Plaintext: 40 (Ack reply, no payload)

Full packet: 53 80 1A 00 0D 02 18 77 29 4E 82 C8 D3 77 90 49
             6B 94 F9 4E 6D 58 0E 8C 0E F4

Breakdown:
  Header+SCB: 53 80 1A 00 0D 02 18  (7 bytes AAD)
  Ciphertext: 77                     (1 byte, encrypted 0x40)
  GCM tag:    29 4E 82 C8 D3 77 90 49 6B 94 F9 4E 6D 58 0E 8C
  CRC:        0E F4
```

**Counter 2 — TX Poll command:**
```
Full packet: 53 00 1A 00 0E 02 17 62 FD 90 02 09 F9 92 84 CA
             87 EB D3 DC 1D 58 33 3C 16 70
```

**Counter 3 — RX Ack reply:**
```
Full packet: 53 80 1A 00 0E 02 18 5E 95 A0 72 CC 9F 22 8A 8B
             B0 84 6F C2 1F 2C 85 0E 15 58
```

### 10.5 Validation Order

To validate your implementation, verify in this order:

1. **KMAC256**: Derive S-ENC and S-NONCE from SCBK, RndA, RndB. Compare against Section 10.2.
2. **Nonce**: Compute nonce at counter=0 using S-NONCE and cUID. Compare against Section 10.3.
3. **Encrypt**: Encrypt plaintext `0x60` with counter=0, AAD=`53 00 1A 00 0D 02 17`. Verify ciphertext byte and GCM tag match Counter 0 TX.
4. **Decrypt**: Decrypt Counter 1 RX using counter=1. Verify plaintext is `0x40`.
5. **Full sequence**: Encrypt/decrypt all 4 test vectors in order (counter 0 through 3).

## 11. Implementation Checklist

Use this checklist to track your SC2 implementation progress:

### Cryptographic Primitives
- [ ] KMAC256 with empty customization string
- [ ] AES-256-GCM encrypt (with AAD support)
- [ ] AES-256-GCM decrypt (with AAD and tag verification)
- [ ] AES-256-CBC encrypt (zero IV, no padding, for cryptograms)
- [ ] AES-256-CBC encrypt (zero IV, no padding, for nonce derivation)

### Key Derivation
- [ ] S-ENC = KMAC256(SCBK, RndA || RndB, 256, "")
- [ ] S-NONCE = KMAC256(SCBK, RndB || RndA, 256, "")
- [ ] Validate derived keys against test vectors

### Nonce Computation
- [ ] Build 16-byte input: cUID(8) || counter_LE(4) || 0x80 || 0x00 0x00 0x00
- [ ] AES-256-CBC encrypt with S-NONCE key, zero IV
- [ ] Truncate to 12 bytes
- [ ] Validate nonce against test vector

### Handshake (ACU side)
- [ ] Generate 16-byte RndA
- [ ] Build and send osdp_CHLNG with SCB indicator 0x02
- [ ] Parse osdp_CCRYPT: extract cUID (8B), RndB (16B), client cryptogram (32B)
- [ ] Derive session keys
- [ ] Validate client cryptogram
- [ ] Compute and send server cryptogram in osdp_SCRYPT
- [ ] Receive osdp_RMAC_I, verify acceptance, establish channel

### Handshake (PD side)
- [ ] Detect SC2 from SCB indicator byte (0x02) in osdp_CHLNG
- [ ] Generate 16-byte RndB
- [ ] Derive session keys
- [ ] Compute client cryptogram and expected server cryptogram
- [ ] Send osdp_CCRYPT with cUID, RndB, client cryptogram
- [ ] Receive osdp_SCRYPT, validate server cryptogram
- [ ] Send osdp_RMAC_I with empty payload, establish channel

### Message Encryption/Decryption
- [ ] Compute nonce from counter before each message
- [ ] Encrypt: plaintext = command/reply byte + payload
- [ ] Use header bytes (SOM through SCB) as AAD
- [ ] Append 16-byte GCM tag after ciphertext
- [ ] Increment counter after each encrypt/decrypt
- [ ] Decrypt: split ciphertext and tag, verify GCM authentication

### Spy/Monitor (Optional)
- [ ] Detect SC2 from osdp_CHLNG SCB indicator
- [ ] Extract RndA from CHLNG, RndB + cUID from CCRYPT
- [ ] Derive session keys (requires knowing the SCBK)
- [ ] Share a single counter across both directions
- [ ] Decrypt captured traffic using derived keys

### Edge Cases
- [ ] Reject osdp_CHLNG with SCB indicator 0x02 if SC2 is not supported
- [ ] Handle session re-establishment (new CHLNG resets all state)
- [ ] Enforce terminal count (500M messages)
- [ ] Handle invalid cryptograms with NAK (error code: "Does not support security block")
