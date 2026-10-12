using System.Buffers.Binary;
using System.IO.Compression;
namespace FreshFoundation;

// A request identifies one primitive occurrence in an immutable inspected source.
// It never identifies arbitrary bytes or named game fields.
public sealed record PrimitiveReplacement(int EventIndex, byte[] ExpectedBytes, byte[] ReplacementBytes);
public sealed class StructuralSerializationResult
{
    public string Status => "EXPERIMENTAL";
    public Document InspectedDocument {get;}
    public string SourceDigest {get;}
    internal StructuralSerializationResult(Document document,string sourceDigest) {InspectedDocument=document;SourceDigest=sourceDigest;}
    public byte[] CopyFileBytes()=>InspectedDocument.SerializeUnchanged();
}
public static class ExperimentalStructuralSerializer
{
    public static StructuralSerializationResult Replace(Document source,IReadOnlyList<PrimitiveReplacement> requests)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(requests);
        int requestCount=requests.Count;
        if(requestCount<0 || requestCount>StructureWalker.EventLimit)throw new ArgumentException("Replacement count exceeds policy.",nameof(requests));
        var payload=source.Structure.CopyPayload();
        var seen=new HashSet<int>();
        var plans=new List<(int Offset,byte[] Bytes)>();
        for(int i=0;i<requestCount;i++)
        {
            var request=requests[i]??throw new ArgumentException("Null replacement request.",nameof(requests));
            if(request.EventIndex<0 || request.EventIndex>=source.Structure.Events.Count)throw new ArgumentException("Unknown primitive occurrence.",nameof(requests));
            if(!seen.Add(request.EventIndex))throw new ArgumentException("Duplicate target occurrence.",nameof(requests));
            var value=source.Structure.Events[request.EventIndex];
            if(value.DataCoordinate is not int coordinate || value.Width is not uint width)throw new ArgumentException("Only primitive data occurrences can be replaced.",nameof(requests));
            if(request.ExpectedBytes is null || request.ReplacementBytes is null || request.ExpectedBytes.Length!=width || request.ReplacementBytes.Length!=width)
                throw new ArgumentException("Expected and replacement bytes must match the primitive width.",nameof(requests));
            int offset=coordinate-12;
            if(offset<0 || width>payload.Length-offset)throw new InvalidDataException("Primitive span outside inspected payload.");
            var expected=(byte[])request.ExpectedBytes.Clone();var replacement=(byte[])request.ReplacementBytes.Clone();
            if(!payload.AsSpan(offset,(int)width).SequenceEqual(expected))throw new ArgumentException("Expected original primitive bytes differ.",nameof(requests));
            plans.Add((offset,replacement));
        }
        plans.Sort((a,b)=>a.Offset.CompareTo(b.Offset));
        int previousEnd=-1;
        foreach(var plan in plans)
        {
            if(plan.Offset<previousEnd)throw new ArgumentException("Overlapping primitive targets.",nameof(requests));
            previousEnd=checked(plan.Offset+plan.Bytes.Length);
        }
        bool changed=false;
        foreach(var plan in plans)
        {
            if(!payload.AsSpan(plan.Offset,plan.Bytes.Length).SequenceEqual(plan.Bytes))changed=true;
            plan.Bytes.CopyTo(payload,plan.Offset);
        }
        if(!changed)return new(source,source.Digest);
        var original=source.SerializeUnchanged();
        byte[] output;
        if(source.Metadata.Flags==0)
        {
            int size=checked(12+payload.Length+4);
            if(size>Document.Limit)throw new InvalidDataException("Reconstructed source exceeds size policy.");
            output=new byte[size];original.AsSpan(0,12).CopyTo(output);payload.CopyTo(output,12);
        }
        else
        {
            // Optimal is an engineering choice, not a format requirement.
            using var encoded=new CappedBuffer(Document.Limit-44);
            using(var compressor=new DeflateStream(encoded,CompressionLevel.Optimal,true))compressor.Write(payload);
            int length=checked((int)encoded.Length);
            int beforeChecksum=checked((40+length+3)&~3);
            int size=checked(beforeChecksum+4);
            if(size>Document.Limit)throw new InvalidDataException("Reconstructed source exceeds size policy.");
            output=new byte[size];original.AsSpan(0,40).CopyTo(output);encoded.CopyBytesTo(output.AsSpan(40,length));
            BinaryPrimitives.WriteUInt64LittleEndian(output.AsSpan(16),(ulong)length+16);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(28),(uint)length);
            BinaryPrimitives.WriteUInt64LittleEndian(output.AsSpan(32),(ulong)payload.Length);
        }
        uint checksum=ObservedChecksum.Calculate(output.AsSpan(0,output.Length-4));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(output.Length-4),checksum);
        var result=Document.Inspect(output);
        if(!result.Structure.Events.SequenceEqual(source.Structure.Events) || result.Structure.Summary.Records!=source.Structure.Summary.Records || result.Structure.Summary.ValueEvents!=source.Structure.Summary.ValueEvents || result.Structure.Summary.ConsumedCoordinate!=source.Structure.Summary.ConsumedCoordinate)
            throw new InvalidDataException("Reconstructed structural identities changed.");
        if(!result.Structure.CopyPayload().AsSpan().SequenceEqual(payload))throw new InvalidDataException("Reconstructed payload differs.");
        return new(result,source.Digest);
    }
    // Bounds actual encoded writes before the stream grows past output policy.
    internal sealed class CappedBuffer(int limit) : Stream
    {
        private readonly MemoryStream bytes=new();
        public override void Write(byte[] buffer,int offset,int count)=>Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if(buffer.Length>limit-bytes.Length)throw new InvalidDataException("Encoded output exceeds size policy.");
            bytes.Write(buffer);
        }
        internal void CopyBytesTo(Span<byte> destination)=>bytes.GetBuffer().AsSpan(0,(int)bytes.Length).CopyTo(destination);
        public override long Length=>bytes.Length;
        public override long Position {get=>bytes.Position;set=>throw new NotSupportedException();}
        public override bool CanRead=>false;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override void Flush()=>bytes.Flush();
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();
        public override void SetLength(long v)=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){if(disposing)bytes.Dispose();base.Dispose(disposing);}
    }
}

