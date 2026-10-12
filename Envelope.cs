using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
namespace FreshFoundation;

public sealed record Capability(string EvidenceId, string Version, bool CanRead, bool CanWrite);
public static class Profiles
{
    public static Capability Observed { get; } = new("F1-F9/revision-2", "2", true, false);
    public static Capability Resolve(string evidence, string version) => evidence == Observed.EvidenceId && version == Observed.Version
        ? Observed : throw new NotSupportedException("UNSUPPORTED profile: admission required.");
}
public sealed record Envelope(uint Version, uint Flags, uint? EncodedLength, ulong? DecodedLength);
public sealed class Document
{
    public const int Limit = 64 * 1024 * 1024;
    private readonly byte[] original;
    public Envelope Metadata { get; }
    public string Status => "READ_ONLY";
    public string Integrity => "UNVERIFIED";
    public string ChecksumStatus => "CHECKSUM_MATCHED";
    public uint Checksum {get;}
    public StructuralReading Structure {get;}
    public string GameSemantics => "UNVERIFIED";
    public string GameIdentity => "UNVERIFIED";
    public string Digest { get; }
    private Document(byte[] bytes, Envelope metadata, uint checksum, StructuralReading structure)
    { original = bytes; Metadata = metadata; Checksum=checksum;Structure=structure;Digest = Convert.ToHexString(SHA256.HashData(bytes)); }
    public byte[] SerializeUnchanged() => (byte[])original.Clone();
    public void RequestEdits(IReadOnlyDictionary<string, string> edits) => throw new NotSupportedException("UNSUPPORTED: no game field mappings admitted.");
    public static Document Inspect(ReadOnlySpan<byte> input)
    {
        if (input.Length > Limit || input.Length < 16) throw new InvalidDataException("Input length outside policy.");
        var bytes = input.ToArray();
        uint checksum=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length-4));
        if(ObservedChecksum.Calculate(bytes.AsSpan(0,bytes.Length-4))!=checksum)throw new InvalidDataException("CHECKSUM_MISMATCH: payload not processed.");
        if (!bytes.AsSpan(0,4).SequenceEqual(new byte[]{0x44,0x53,0x53,0x53})) throw new InvalidDataException("Unknown envelope identifier.");
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        if(version != 2 || (flags != 0 && flags != 8)) throw new InvalidDataException("Unknown observational profile.");
        if(flags == 0) return new(bytes, new(version,flags,null,null),checksum,StructureWalker.Read(bytes.AsSpan(12,bytes.Length-16).ToArray()));
        if(bytes.Length < 44 || bytes.Length%4!=0) throw new InvalidDataException("Incomplete or unaligned compressed envelope.");
        uint encoded = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28));
        ulong decoded = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(32));
        if(encoded == 0 || encoded > bytes.Length - 44 || decoded > Limit) throw new InvalidDataException("Compressed length outside bounds.");
        if(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(16))!=(ulong)encoded+16 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24))!=1)throw new InvalidDataException("Unsupported compressed wrapper relationship.");
        int padding=bytes.Length-44-(int)encoded;
        if(padding>3 || bytes.AsSpan(40+(int)encoded,padding).IndexOfAnyExcept((byte)0)>=0)throw new InvalidDataException("Invalid poststream padding.");
        using var source = new ExactRegion(bytes, 40, (int)encoded);
        using var inflater = new DeflateStream(source, CompressionMode.Decompress);
        byte[] payload=new byte[(int)decoded];
        int total=0;
        while(total<payload.Length)
        {
            int count=inflater.Read(payload.AsSpan(total));
            if(count==0)break;total+=count;
        }
        if(inflater.ReadByte()!=-1)throw new InvalidDataException("Decoded expansion exceeds declared bounds.");
        if(total != (long)decoded || source.Consumed != encoded) throw new InvalidDataException("Decoded size or consumed region mismatch.");
        return new(bytes, new(version, flags, encoded, decoded),checksum,StructureWalker.Read(payload));
    }
    // One-byte reads prevent inflater buffering from concealing unused region bytes.
    // An attempted read past the region is truncation, not an ordinary stream EOF.
    private sealed class ExactRegion(byte[] bytes, int offset, int length) : Stream
    {
        public int Consumed {get; private set;}
        public override int Read(byte[] buffer, int start, int count) => Read(buffer.AsSpan(start,count));
        public override int Read(Span<byte> buffer) { if(buffer.Length == 0) return 0; if(Consumed == length) throw new InvalidDataException("Incomplete DEFLATE stream."); buffer[0] = bytes[offset + Consumed++]; return 1; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => length; public override long Position {get => Consumed; set => throw new NotSupportedException();}
        public override void Flush() {} public override long Seek(long x, SeekOrigin y) => throw new NotSupportedException(); public override void SetLength(long x) => throw new NotSupportedException(); public override void Write(byte[] b,int o,int c) => throw new NotSupportedException();
    }
}


