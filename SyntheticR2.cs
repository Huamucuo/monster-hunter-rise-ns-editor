using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
// Synthetic construction only. This is not a document serializer and accepts no saves.
internal sealed class SyntheticR2
{
    internal readonly List<byte> Bytes=[];
    internal int Position=>Bytes.Count+12;
    internal void Word(uint value) { Span<byte> b=stackalloc byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,value);Bytes.AddRange(b.ToArray()); }
    internal void Tag(int value)=>Word(unchecked((uint)value));
    internal void Pad(int width) { long target=((long)Position+width-1)&~((long)width-1);while(Position<target)Bytes.Add(0xA7); }
    internal void Root(uint id,uint fields,Action values) {Word(id);Body(fields,values);}
    internal void Body(uint fields,Action values) {Word(fields);Word(0xDEADBEEF);values();}
    internal void Field(uint id,int tag,Action value) {Word(id);Tag(tag);value();Pad(4);}
    internal void Primitive(uint width,bool supplied=false) {if(!supplied){Pad(4);Word(width);}Pad((int)width);for(uint i=0;i<width;i++)Bytes.Add((byte)(i+1));}
    internal void Text(string text) {Pad(4);var b=Encoding.Unicode.GetBytes(text);Word((uint)b.Length/2);Bytes.AddRange(b);}
    internal void Array(int tag,uint width,uint count,uint mode,Action values) {Pad(4);Tag(tag);Word(width);Word(count);Word(mode);values();Pad(4);}
    internal byte[] File(bool compressed=false)=>Wrap(Bytes.ToArray(),compressed);
    internal static byte[] Wrap(byte[] payload,bool compressed=false)
    {
        byte[] body;
        if(compressed)
        {
            using var memory=new MemoryStream();using(var encoder=new DeflateStream(memory,CompressionLevel.Optimal,true))encoder.Write(payload);
            var encoded=payload.Length==0?new byte[]{3,0}:memory.ToArray();
            body=new byte[(40+encoded.Length+3)&~3];body[8]=8;body[12]=0xCC;
            BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(16),(ulong)encoded.Length+16);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(24),1);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(28),(uint)encoded.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(32),(ulong)payload.Length);
            encoded.CopyTo(body,40);
        }
        else {body=new byte[12+payload.Length];payload.CopyTo(body,12);}
        new byte[]{68,83,83,83,2}.CopyTo(body,0);
        var file=new byte[body.Length+4];body.CopyTo(file,0);Recheck(file);return file;
    }
    internal static void Recheck(byte[] bytes)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length-4),FreshFoundation.ObservedChecksum.Calculate(bytes.AsSpan(0,bytes.Length-4)));
}
