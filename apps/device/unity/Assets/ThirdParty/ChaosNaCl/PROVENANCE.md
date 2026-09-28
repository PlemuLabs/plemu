# Chaos.NaCl — Vendored Provenance

Third-party cryptography library vendored to provide Ed25519 signature verification,
which .NET Standard 2.1 / Unity does not offer natively. This is the same Ed25519
implementation the Solana Unity SDK depends on.

## Source

- Repository: https://github.com/CodesInChaos/Chaos.NaCl
- Commit: `2c861348dc45369508e718aa08611c53b53553db`
- Vendored on: 2026-07-12
- License: Public Domain (see `License.txt`)

## Entry point used by this project

```csharp
Chaos.NaCl.Ed25519.Verify(byte[] signature, byte[] message, byte[] publicKey)
```

`ManifestVerifier` (Plemu.Content) calls this to verify the detached
Ed25519 signature over the raw manifest bytes per SPEC §2.3 step 3.

## Files taken

The entire `Chaos.NaCl/` managed library subtree from the upstream repository was copied,
preserving its internal folder structure:

- Top level: `CryptoBytes.cs`, `Ed25519.cs`, `MontgomeryCurve25519.cs`, `OneTimeAuth.cs`,
  `Poly1305.cs`, `Sha512.cs`, `XSalsa20Poly1305.cs`
- `Internal/`: `Array16.cs`, `Array8.cs`, `ByteIntegerConverter.cs`, `InternalAssert.cs`,
  `Poly1305Donna.cs`, `Sha512Internal.cs`
- `Internal/Salsa/`: `Salsa20.cs`, `SalsaCore.cs`
- `Internal/Ed25519Ref10/`: full djb Ref10 field/group/scalar arithmetic port (57 files)

Total: 64 `.cs` source files. The library is pure managed C# (no `DllImport`, no
`unsafe`, no platform interop), so it compiles unchanged on Unity's .NET Standard 2.1
scripting backend.

## Deviations from upstream

- The upstream `Properties/AssemblyInfo.cs` / `AssemblyInfoFull.cs` were NOT copied — Unity
  generates assembly attributes for the `ChaosNaCl` asmdef, and importing hand-authored
  `[assembly: ...]` attributes would conflict.
- A non-source scratch file (`Internal/Salsa/replace regex.txt`) present in the upstream
  repo was omitted.
- No source file was otherwise modified.

## Assembly wiring

`ChaosNaCl.asmdef` (`noEngineReferences: true`, `autoReferenced: false`) packages these
sources into a `ChaosNaCl` assembly referenced only by `Plemu`.
