using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
namespace FreshFoundation;

// A request identifies one primitive occurrence in an immutable inspected source.
// It never identifies arbitrary bytes or named game fields.
public sealed record PrimitiveReplacement(int EventIndex, byte[] ExpectedBytes, byte[] ReplacementBytes);
// Raw UTF-16LE bytes make the immutable original expectation explicit.
public sealed record TextReplacement(int EventIndex, byte[] ExpectedBytes, byte[] ReplacementBytes);
public sealed class StructuralSerializationResult
{
    public string Status => "EXPERIMENTAL";
    public Document InspectedDocument
    {
        get;
    }
    public string SourceDigest
    {
        get;
    }
    internal StructuralSerializationResult(Document document, string sourceDigest)
    {
        InspectedDocument = document;
        SourceDigest = sourceDigest;
    }
    public byte[] CopyFileBytes() => InspectedDocument.SerializeUnchanged();
}
public static class ExperimentalStructuralSerializer
{
    public static StructuralSerializationResult Replace(Document source, IReadOnlyList<PrimitiveReplacement> requests)
        => Replace(source, requests, []);

    public static StructuralSerializationResult Replace(Document source, IReadOnlyList<PrimitiveReplacement> requests, IReadOnlyList<TextReplacement> texts)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(texts);
        int requestCount = requests.Count;
        if ((long)requestCount + texts.Count > StructureWalker.EventLimit)
            throw new ArgumentException("Replacement count exceeds policy.", nameof(requests));
        var payload = source.Structure.CopyPayload();
        var seen = new HashSet<int>();
        var plans = new List<(int Offset, byte[] Bytes)>();
        for (int i = 0; i < requestCount; i++)
        {
            var request = requests[i] ?? throw new ArgumentException("Null replacement request.", nameof(requests));
            if (request.EventIndex < 0 || request.EventIndex >= source.Structure.Events.Count)
                throw new ArgumentException("Unknown primitive occurrence.", nameof(requests));
            if (!seen.Add(request.EventIndex))
                throw new ArgumentException("Duplicate target occurrence.", nameof(requests));
            var value = source.Structure.Events[request.EventIndex];
            if (value.DataCoordinate is not int coordinate || value.Width is not uint width)
                throw new ArgumentException("Only primitive data occurrences can be replaced.", nameof(requests));
            if (request.ExpectedBytes is null || request.ReplacementBytes is null || request.ExpectedBytes.Length != width || request.ReplacementBytes.Length != width)
                throw new ArgumentException("Expected and replacement bytes must match the primitive width.", nameof(requests));
            int offset = coordinate - 12;
            if (offset < 0 || width > payload.Length - offset)
                throw new InvalidDataException("Primitive span outside inspected payload.");
            var expected = (byte[])request.ExpectedBytes.Clone();
            var replacement = (byte[])request.ReplacementBytes.Clone();
            if (!payload.AsSpan(offset, (int)width).SequenceEqual(expected))
                throw new ArgumentException("Expected original primitive bytes differ.", nameof(requests));
            plans.Add((offset, replacement));
        }
        plans.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        int previousEnd = -1;
        foreach (var plan in plans)
        {
            if (plan.Offset < previousEnd)
                throw new ArgumentException("Overlapping primitive targets.", nameof(requests));
            previousEnd = checked(plan.Offset + plan.Bytes.Length);
        }
        bool changed = false;
        foreach (var plan in plans)
        {
            if (!payload.AsSpan(plan.Offset, plan.Bytes.Length).SequenceEqual(plan.Bytes))
                changed = true;
            plan.Bytes.CopyTo(payload, plan.Offset);
        }
        var textPlans = new Dictionary<int, byte[]>();
        foreach (var request in texts)
        {
            if (request is null || request.EventIndex < 0 || request.EventIndex >= source.Structure.Events.Count)
                throw new ArgumentException("Unknown text occurrence.", nameof(texts));
            if (!seen.Add(request.EventIndex))
                throw new ArgumentException("Duplicate target occurrence.", nameof(texts));
            var value = source.Structure.Events[request.EventIndex];
            if (value.Tag != 15 || value.Count is not uint units)
                throw new ArgumentException("Only tag-15 text occurrences can be replaced.", nameof(texts));
            if (request.ExpectedBytes is null || request.ExpectedBytes.Length != (long)units * 2 || request.ReplacementBytes is null)
                throw new ArgumentException("Expected bytes must match original text length.", nameof(texts));
            ValidateTextBytes(request.ReplacementBytes);
            int offset = ((value.Coordinate + 3) & ~3) - 12 + 4;
            if (!payload.AsSpan(offset, request.ExpectedBytes.Length).SequenceEqual(request.ExpectedBytes))
                throw new ArgumentException("Expected original text bytes differ.", nameof(texts));
            var replacement = (byte[])request.ReplacementBytes.Clone();
            textPlans.Add(request.EventIndex, replacement);
            if (!replacement.AsSpan().SequenceEqual(request.ExpectedBytes))
                changed = true;
        }
        if (!changed)
            return new(source, source.Digest);
        if (textPlans.Count > 0)
            payload = new TextRewriter(source.Structure, payload, textPlans).Rewrite();
        var original = source.SerializeUnchanged();
        byte[] output;
        if (source.Metadata.Flags == 0)
        {
            int size = checked(12 + payload.Length + 4);
            if (size > Document.Limit)
                throw new InvalidDataException("Reconstructed source exceeds size policy.");
            output = new byte[size];
            original.AsSpan(0, 12).CopyTo(output);
            payload.CopyTo(output, 12);
        }
        else
        {
            // Optimal is an engineering choice, not a format requirement.
            using var encoded = new CappedBuffer(Document.Limit - 44);
            using (var compressor = new DeflateStream(encoded, CompressionLevel.Optimal, true))
                compressor.Write(payload);
            int length = checked((int)encoded.Length);
            int beforeChecksum = checked((40 + length + 3) & ~3);
            int size = checked(beforeChecksum + 4);
            if (size > Document.Limit)
                throw new InvalidDataException("Reconstructed source exceeds size policy.");
            output = new byte[size];
            original.AsSpan(0, 40).CopyTo(output);
            encoded.CopyBytesTo(output.AsSpan(40, length));
            BinaryPrimitives.WriteUInt64LittleEndian(output.AsSpan(16), (ulong)length + 16);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(28), (uint)length);
            BinaryPrimitives.WriteUInt64LittleEndian(output.AsSpan(32), (ulong)payload.Length);
        }
        uint checksum = ObservedChecksum.Calculate(output.AsSpan(0, output.Length - 4));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(output.Length - 4), checksum);
        var result = Document.Inspect(output);
        ValidateLogicalResult(source, result, requests, textPlans);
        if (!result.Structure.CopyPayload().AsSpan().SequenceEqual(payload))
            throw new InvalidDataException("Reconstructed payload differs.");
        return new(result, source.Digest);
    }

    private static readonly UnicodeEncoding StrictUnicode = new(false, false, true);

    internal static void ValidateTextBytes(byte[] bytes)
    {
        if (bytes.Length % 2 != 0 || bytes.Length / 2 > StructureWalker.TextLimit)
            throw new ArgumentException("UTF-16 bytes must have an even bounded length.");
        try
        {
            StrictUnicode.GetCharCount(bytes);
        }
        catch (DecoderFallbackException e) { throw new ArgumentException("Malformed UTF-16 replacement.", e); }
    }

    private static byte[] ValueBytes(StructuralReading structure, int index)
    {
        var value = structure.Events[index];
        var bytes = structure.CopyPayload();
        if (value.DataCoordinate is int position && value.Width is uint width)
            return bytes.AsSpan(position - 12, (int)width).ToArray();
        if (value.Tag == 15 && value.Count is uint units)
            return bytes.AsSpan(((value.Coordinate + 3) & ~3) - 12 + 4, checked((int)units * 2)).ToArray();
        return [];
    }

    private static void ValidateLogicalResult(Document source, Document result, IReadOnlyList<PrimitiveReplacement> primitives, IReadOnlyDictionary<int, byte[]> texts)
    {
        var before = source.Structure.Events;
        var after = result.Structure.Events;
        if (before.Count != after.Count || source.Structure.Summary.Records != result.Structure.Summary.Records)
            throw new InvalidDataException("Reconstructed structural identities changed.");
        var replacements = primitives.ToDictionary(p => p.EventIndex, p => p.ReplacementBytes);
        var originalPayload = source.Structure.CopyPayload();
        var newPayload = result.Structure.CopyPayload();
        byte[] Read(byte[] payload, StructuralEvent value)
        {
            if (value.DataCoordinate is int coordinate && value.Width is uint width)
                return payload.AsSpan(coordinate - 12, (int)width).ToArray();
            return payload.AsSpan(((value.Coordinate + 3) & ~3) - 12 + 4, checked((int)value.Count!.Value * 2)).ToArray();
        }
        for (int i = 0; i < before.Count; i++)
        {
            var a = before[i];
            var b = after[i];
            if (a.Tag != b.Tag || a.Depth != b.Depth || a.Identifier != b.Identifier || a.ClassIdentifier != b.ClassIdentifier ||
               a.Width != b.Width || a.Mode != b.Mode || a.ImpliedTag != b.ImpliedTag || a.ElementTag != b.ElementTag ||
               (a.Tag != 15 && a.Count != b.Count))
                throw new InvalidDataException("Reconstructed logical field/array metadata changed.");
            if (a.DataCoordinate is null && a.Tag != 15)
                continue;
            byte[] expected = replacements.TryGetValue(i, out var primitive) ? primitive : texts.TryGetValue(i, out var text) ? text : Read(originalPayload, a);
            if (!Read(newPayload, b).AsSpan().SequenceEqual(expected))
                throw new InvalidDataException("Reconstructed targeted or untouched value differs.");
        }
    }

    // Consume the inspected grammar and retain original padding wherever the new
    // alignment has a common span. Newly required padding is zero, never text data.
    private sealed class TextRewriter(StructuralReading structure, byte[] original, IReadOnlyDictionary<int, byte[]> texts)
    {
        private readonly MemoryStream output = new();
        private int cursor, eventIndex;
        private int Position => checked((int)output.Length + 12);

        internal byte[] Rewrite()
        {
            while (original.Length - cursor > 3)
            {
                Copy(4);
                Emit();
            }
            Copy(original.Length - cursor);
            if (eventIndex != structure.Events.Count)
                throw new InvalidDataException("Structural emission did not consume every event.");
            return output.ToArray();
        }

        private void Emit()
        {
            int index = eventIndex++;
            var value = structure.Events[index];
            if (cursor != value.Coordinate - 12)
                throw new InvalidDataException("Structural emission cursor mismatch.");
            if (value.Tag == 17)
            {
                Copy(8);
                for (uint i = 0; i < value.Count; i++)
                {
                    Copy(8);
                    Emit();
                    Align(4);
                }
            }
            else if (value.Tag == -1)
            {
                Align(4);
                Copy(16);
                for (uint i = 0; i < value.Count; i++)
                    Emit();
                Align(4);
            }
            else if (value.Tag == 15)
            {
                Align(4);
                int length = checked((int)value.Count!.Value * 2);
                if (texts.TryGetValue(index, out var replacement))
                {
                    cursor += 4;
                    Span<byte> word = stackalloc byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(word, (uint)replacement.Length / 2);
                    Write(word);
                    cursor += length;
                    Write(replacement);
                }
                else
                    Copy(checked(4 + length));
            }
            else
            {
                if (!value.ImpliedTag)
                {
                    Align(4);
                    Copy(4);
                }
                Align(value.Width!.Value);
                Copy((int)value.Width.Value);
            }
            if (cursor != value.EndCoordinate - 12)
                throw new InvalidDataException("Structural emission span mismatch.");
        }

        private void Align(uint width)
        {
            int oldPosition = checked(cursor + 12);
            int oldCount = checked((int)(((ulong)oldPosition + width - 1) & ~((ulong)width - 1))) - oldPosition;
            int newCount = checked((int)(((ulong)Position + width - 1) & ~((ulong)width - 1))) - Position;
            int common = Math.Min(oldCount, newCount);
            Write(original.AsSpan(cursor, common));
            for (int i = common; i < newCount; i++)
                Write([0]);
            cursor += oldCount;
        }

        private void Copy(int count)
        {
            Write(original.AsSpan(cursor, count));
            cursor += count;
        }
        private void Write(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > Document.Limit - output.Length)
                throw new InvalidDataException("Reconstructed payload exceeds size policy.");
            output.Write(bytes);
        }
    }
    // Bounds actual encoded writes before the stream grows past output policy.
    internal sealed class CappedBuffer(int limit) : Stream
    {
        private readonly MemoryStream bytes = new();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > limit - bytes.Length)
                throw new InvalidDataException("Encoded output exceeds size policy.");
            bytes.Write(buffer);
        }
        internal void CopyBytesTo(Span<byte> destination) => bytes.GetBuffer().AsSpan(0, (int)bytes.Length).CopyTo(destination);
        public override long Length => bytes.Length;
        public override long Position
        {
            get => bytes.Position; set => throw new NotSupportedException();
        }
        public override bool CanRead => false; public override bool CanWrite => true; public override bool CanSeek => false;
        public override void Flush() => bytes.Flush();
        public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                bytes.Dispose();
            base.Dispose(disposing);
        }
    }
}

