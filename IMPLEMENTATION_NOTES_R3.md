# Revision 3 implementation notes — experimental in-memory serialization

Author credit: 花木错. GPT-assisted implementation from the explicitly admitted R3 engineering design and prior reviewed byte relationships. No new game facts, game semantics or game acceptance are claimed. Earlier notes/results remain historical snapshots; this document describes the separately named experimental API that extends them.

## Public contract for reviewer use

The normal inspector and file-copy workflow remain read-only. This new public API performs no file I/O:

```csharp
using FreshFoundation;

Document source = Document.Inspect(inputBytes);
int index = Enumerable.Range(0, source.Structure.Events.Count)
    .First(i => source.Structure.Events[i].DataCoordinate is not null);
StructuralEvent occurrence = source.Structure.Events[index];
byte[] expected = source.Structure.CopyPayload()
    .AsSpan(occurrence.DataCoordinate!.Value - 12,
            checked((int)occurrence.Width!.Value)).ToArray();
byte[] replacement = (byte[])expected.Clone();
replacement[0] ^= 1; // Synthetic/research raw byte choice; no game meaning.

StructuralSerializationResult result = ExperimentalStructuralSerializer.Replace(
    source, new[] { new PrimitiveReplacement(index, expected, replacement) });
byte[] separateOutput = result.CopyFileBytes();
Document reinspectedResult = result.InspectedDocument;
```

The caller must handle sources with no primitive occurrences instead of assuming `First` succeeds. An event index is stable within the chosen immutable source's ordered `Structure.Events` list. `DataCoordinate` identifies only an admitted primitive's raw data start in the virtual header-plus-payload coordinate system; subtract 12 for the index into `CopyPayload()`. Text, object and array events have null DataCoordinate and cannot be targeted. This property was added to the independent reader during R3; it does not grant a mapping to named game fields. Primitive elements inside arrays have their own events and can be targeted; array metadata cannot.

Each request contains exact expected old data and a same-width replacement. The serializer snapshots these buffers, validates every request and sorts authorized spans before touching its private payload copy. It rejects null/malformed requests, out-of-range/negative identities, nonprimitive events, wrong expected bytes, changed widths, duplicate/overlapping targets and request-count policy violations. The count is sampled once, checked and used for the entire iteration. No intermediate result is returned on failure. Distinct primitive spans from a valid inspected document cannot overlap; duplicate identities are the directly constructible overlapping-request case, and an independent span-overlap guard remains.

The source, requests and earlier results are never modified. Results are separate immutable holders; a no-op result may safely reference the immutable source document internally. Every returned byte buffer is a clone. Zero requests and entirely identical replacements return the exact original file bytes without recompression. No semantic `RequestEdits` support or CanWrite capability was enabled.

## Reconstruction and bounds

Flags 0 preserves the first 12 bytes and all non-target payload bytes, then refreshes the checksum. Flags 8 preserves the original 40-byte wrapper except the three admitted length fields, compresses the private payload using .NET raw DEFLATE with `CompressionLevel.Optimal`, pads with 0–3 zero bytes and refreshes the checksum. The level is an engineering choice, not a format requirement. Changed compressed output is not required to match the source compressor's bytes.

The compressed writer limits actual encoded writes to 64 MiB minus the 40-byte wrapper and four-byte checksum before growing its backing stream. Final alignment and total size are checked with checked arithmetic. Payload replacement never resizes the already bounded decoded input. The reconstructed container is inspected again, then the event sequence, positions, record/value counts, consumed coordinate and complete decoded payload are compared to the intended result. Unknown wrapper meanings and game compatibility remain UNVERIFIED even when these checks pass.

No modified-file export command was added. `CopySession` and `Copies.cs` remain byte-identical to their reviewed pre-R3 implementation and copy only their explicitly selected input snapshots. A reviewer may save returned bytes as private validation artifacts; that is separate from an application export workflow.

## Executed validation and evidence

`dotnet build --configuration Release` succeeded with zero warnings/errors. `dotnet run --configuration Release -- --self-test-r3` passed:

- 75 R3 cases, one of which invokes the retained R2 suite.
- 108 retained R2 cases, one of which invokes twenty retained copy cases.
- 20 retained copy cases.

These are nested reporting counts: 74 direct R3 cases + 107 direct R2 cases + 20 copy cases = 201 direct cases. Do not add the wrapper cases as extra independent test families. There were no failed R3 test runs.

R3 coverage includes flags 0/8 no-op identity; all fifteen admitted primitive tag/width combinations; all twelve non-text primitive array prefixes; multi-target transactions; preserved text, duplicate IDs, ordering, nonzero padding and wrapper opaque bytes; malformed/nonprimitive/duplicate/wrong-byte/wrong-size requests; source/result/request immutability; growth and shrinkage; every poststream pad length in a bounded synthetic search; actual encoded-cap boundary; policy excess; bad original checksum/version; unchanged semantic restrictions; and historical result preservation. Newly constructed fixtures provide differing padding and neighboring bytes, and every non-target decoded byte is compared exactly.

Files `test-results-r3.json`, `test-results-r2-retained-r3.json` and `test-results-copy-retained-r3.json` contain actual results. R3 results record the source snapshot, synthetic source hashes before/after and SHA-256 values proving the original R1/R2 result files remained unchanged. `spec-inputs-r3.json` records the two admitted R3 input hashes. No original saves, audit-side probes, legacy sources or reference implementations were accessed by this implementer.

MurmurHash3 attribution and public-domain algorithm provenance remain as documented in R2. This is experimental engineering derived from admitted measurements, not a claim of algorithm invention, strict legal clean-room certification, universal format correctness or completed game editing. The reviewer independently controls real-input private artifact validation. No commits, deployment, upload or public release bundle were created.
