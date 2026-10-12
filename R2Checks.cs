using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using FreshFoundation;
internal static class R2Checks
{
    private static readonly List<object> results=[];
    private static int failures;
    private static void Assert(bool value,string detail="Assertion failed") {if(!value)throw new Exception(detail);}
    private static void Reject(Action action,string? text=null)
    {
        try{action();}catch(InvalidDataException e){if(text!=null)Assert(e.Message.Contains(text),e.Message);return;}catch(NotSupportedException){if(text!=null)throw;return;}
        throw new Exception("Expected rejection");
    }
    private static void Case(string name,Action action) {try{action();results.Add(new{name,status="PASS"});}catch(Exception e){failures++;results.Add(new{name,status="FAIL",error=e.GetType().Name,message=e.Message});}}
    internal static byte[] SafeFixture() {var b=new SyntheticR2();b.Root(13,1,()=>b.Field(22,1,()=>b.Primitive(1)));return b.File(true);}
    private static byte[] Primitive(int tag,uint width,bool compressed=false) {var b=new SyntheticR2();b.Root(13,1,()=>b.Field(22,tag,()=>b.Primitive(width)));return b.File(compressed);}
    private static byte[] Array(int tag,uint width,uint count,uint mode,Action<SyntheticR2>? contents=null) {var b=new SyntheticR2();b.Root(13,1,()=>b.Field(22,-1,()=>b.Array(tag,width,count,mode,()=>contents?.Invoke(b))));return b.File();}
    private static byte[] Mutate(byte[] input,Action<byte[]> action){var b=(byte[])input.Clone();action(b);SyntheticR2.Recheck(b);return b;}
    private static Document Read(byte[] bytes)=>Document.Inspect(bytes);
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    internal static int Run(bool retainedForR3=false,bool retainedForR4=false)
    {
        results.Clear();failures=0;
        using(var vectors=JsonDocument.Parse(File.ReadAllText("checksum-vectors-r2.json")))
        {
            int index=0;
            foreach(var v in vectors.RootElement.GetProperty("vectors").EnumerateArray())
            {
                int n=++index;var bytes=Convert.FromHexString(v.GetProperty("bytes_hex").GetString()!);uint seed=v.GetProperty("seed").GetUInt32(),expected=v.GetProperty("x86_32_unsigned").GetUInt32();
                Case($"checksum independent vector {n}: seed={seed}, bytes={bytes.Length}",()=>Assert(ObservedChecksum.Calculate(bytes,seed)==expected));
            }
            Assert(index==21);
        }
        var baseline=SafeFixture();var before=Hash(baseline);
        Case("checksum matched without game-validity claim",()=>{var d=Read(baseline);Assert(d.ChecksumStatus=="CHECKSUM_MATCHED"&&d.Integrity=="UNVERIFIED"&&d.GameIdentity=="UNVERIFIED"&&d.GameSemantics=="UNVERIFIED"&&d.Structure.Status=="EXPERIMENTAL");});
        Case("content and trailer corruption rejected before payload",()=>{foreach(int p in new[]{0,4,8,12,40,baseline.Length-5,baseline.Length-1}){var b=(byte[])baseline.Clone();b[p]^=1;Reject(()=>Read(b),"CHECKSUM_MISMATCH");}});
        Case("bad checksum precedes malicious decoded allocation",()=>{var b=(byte[])baseline.Clone();BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),ulong.MaxValue);Reject(()=>Read(b),"CHECKSUM_MISMATCH");});
        foreach(var field in new[]{(offset:0,value:0u),(offset:4,value:3u),(offset:8,value:9u)})Case($"unknown header field {field.offset} with valid checksum",()=>Reject(()=>Read(Mutate(baseline,b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(field.offset),field.value)))));
        var combinations=new (int tag,uint width)[]{(1,1),(1,2),(1,4),(2,1),(3,1),(7,4),(8,4),(11,4),(9,8),(12,8),(16,4),(16,8),(16,12),(16,16),(16,116)};
        foreach(var pair in combinations)Case($"primitive admitted {pair.tag}/{pair.width}",()=>{var d=Read(Primitive(pair.tag,pair.width));Assert(d.Structure.Summary.WidthCounts[$"{pair.tag}:{pair.width}"]==1&&d.Structure.Summary.ValueEvents==2);});
        var arrayPairs=new (int tag,uint width)[]{(2,1),(7,4),(16,12),(1,4),(8,4),(16,16),(11,4),(9,8),(1,1),(16,8),(16,116),(1,2)};
        foreach(var pair in arrayPairs)Case($"primitive array admitted {pair.tag}/{pair.width}",()=>{var d=Read(Array(pair.tag,pair.width,3,0,b=>{for(int i=0;i<3;i++)b.Primitive(pair.width,true);}));Assert(d.Structure.Summary.ValueEvents==5&&d.Structure.Summary.WidthCounts[$"{pair.tag}:{pair.width}"]==3);});
        Case("empty tag4 array admitted only as prefix",()=>{var d=Read(Array(4,1,0,0));Assert(d.Structure.Summary.ValueEvents==2&&!d.Structure.Summary.TagCounts.ContainsKey(4));Reject(()=>Read(Array(4,1,1,0,b=>b.Bytes.Add(0))));});
        Case("mode1 array object bodies variable length and implied tags",()=>{var d=Read(Array(17,8,2,1,b=>{b.Body(0,()=>{});b.Body(1,()=>b.Field(9,7,()=>b.Primitive(4)));}));Assert(d.Structure.Summary.ValueEvents==5&&d.Structure.Summary.MaximumDepth==3&&d.Structure.Summary.TagCounts[17]==3&&d.Structure.Events.Count(e=>e.Tag==17&&e.ImpliedTag)==3);});
        Case("mode0 text array uses length not declared width32",()=>{var d=Read(Array(15,32,2,0,b=>{b.Text("a\0");b.Text(new string('b',40));}));Assert(d.Structure.Summary.StringCount==2&&d.Structure.Summary.ValueEvents==4);});
        Case("nested object counts, duplicate identifiers and order retained",()=>{var b=new SyntheticR2();b.Root(5,2,()=>{b.Field(8,17,()=>b.Body(1,()=>b.Field(8,1,()=>b.Primitive(2))));b.Field(8,15,()=>b.Text("synthetic\0"));});b.Root(5,0,()=>{});var d=Read(b.File());Assert(d.Structure.Summary.Records==2&&d.Structure.Summary.ValueEvents==5&&d.Structure.Summary.MaximumDepth==2);Assert(d.Structure.Events.Count(e=>e.Identifier==8)==3);Assert(d.SerializeUnchanged().SequenceEqual(b.File()));});
        Case("raw UTF16 terminators retained and malformed sequence rejected",()=>{var b=new SyntheticR2();b.Root(1,1,()=>b.Field(2,15,()=>b.Text("X\0\ud83d\ude00")));var d=Read(b.File());Assert(d.Structure.Events.Last().Count==4);var bad=Mutate(b.File(),x=>{x[36]=0;x[37]=0xD8;x[38]=0;x[39]=0;});Reject(()=>Read(bad),"UTF-16");});
        Case("width12 golden coordinate52 differs from modulo60",()=>{var b=new SyntheticR2();b.Root(1,2,()=>{b.Field(2,1,()=>b.Primitive(1));b.Field(3,16,()=>b.Primitive(12));});var d=Read(b.File());var raw=d.Structure.CopyPayload();Assert(raw[52-12]==1&&raw[63-12]==12&&raw.Length==52);Assert(raw[37-12]==0xA7&&d.Structure.Summary.NonzeroPaddingBytes==3);});
        Case("width116 golden coordinate132 differs from modulo116",()=>{var file=Primitive(16,116);var d=Read(file);var raw=d.Structure.CopyPayload();Assert(raw[36-12]==0xA7&&raw[131-12]==0xA7&&raw[132-12]==1&&raw[247-12]==116);Assert(d.Structure.Summary.NonzeroPaddingBytes==96);Assert(d.SerializeUnchanged().SequenceEqual(file));});
        Case("compressed virtual coordinates equal uncompressed",()=>{var a=Primitive(16,116);var plain=Read(a);var compressed=Read(SyntheticR2.Wrap(plain.Structure.CopyPayload(),true));Assert(compressed.Structure.Events.SequenceEqual(plain.Structure.Events)&&compressed.Structure.Summary.ConsumedCoordinate==248);});
        Case("wrapper-only bytes are not accepted as complete structure",()=>{Reject(()=>Read(SyntheticR2.Wrap(Enumerable.Repeat((byte)0xFF,12).ToArray())),"policy");});
        Case("last 0 to 3 structural zero bytes and nonzero rejection",()=>{for(int i=0;i<=3;i++){var b=new SyntheticR2();b.Root(1,0,()=>{});b.Bytes.AddRange(new byte[i]);Assert(Read(b.File()).Structure.Summary.TrailingZeroBytes==i);}Reject(()=>Read(SyntheticR2.Wrap([1])));Reject(()=>Read(SyntheticR2.Wrap(new byte[4])));});
        Case("all nonempty truncated prefixes of one record rejected after checksum",()=>{var payload=Read(Primitive(16,116)).Structure.CopyPayload();for(int length=1;length<payload.Length;length++)Reject(()=>Read(SyntheticR2.Wrap(payload[..length])));});
        Case("every prefix shorter than minimum file rejected",()=>{for(int i=0;i<16;i++)Reject(()=>Read(new byte[i]));});
        foreach(var pair in new (int tag,uint width)[]{(0,1),(4,1),(18,4),(1,3),(2,4),(3,2),(7,8),(8,1),(11,8),(9,4),(12,4),(16,20)})Case($"unknown primitive {pair.tag}/{pair.width} rejected",()=>{var b=Primitive(1,1);b=Mutate(b,x=>{BinaryPrimitives.WriteInt32LittleEndian(x.AsSpan(28),pair.tag);BinaryPrimitives.WriteUInt32LittleEndian(x.AsSpan(32),pair.width);});Reject(()=>Read(b));});
        foreach(var pair in new (int tag,uint width,uint mode)[]{(17,8,0),(-1,8,0),(15,4,0),(3,1,0),(12,8,0),(16,4,0),(17,4,1),(1,8,1),(17,8,2)})Case($"unknown array prefix {pair.mode}/{pair.tag}/{pair.width}",()=>Reject(()=>Read(Array(pair.tag,pair.width,0,pair.mode))));
        Case("object field exact policy boundary10000 and10001",()=>{var b=new SyntheticR2();b.Root(1,10000,()=>{for(int i=0;i<10000;i++)b.Field((uint)i,1,()=>b.Primitive(1));});Assert(Read(b.File()).Structure.Summary.ValueEvents==10001);Reject(()=>Read(Mutate(b.File(),x=>BinaryPrimitives.WriteUInt32LittleEndian(x.AsSpan(16),10001))),"field policy");});
        Case("array exact policy boundary500000 and500001",()=>{var b=Array(1,1,500000,0,w=>{w.Bytes.AddRange(new byte[500000]);});Assert(Read(b).Structure.Summary.ValueEvents==500002);Reject(()=>Read(Mutate(b,x=>BinaryPrimitives.WriteUInt32LittleEndian(x.AsSpan(40),500001))),"Array element policy");});
        Case("UTF16 exact policy boundary100000 and100001",()=>{var b=new SyntheticR2();b.Root(1,1,()=>b.Field(2,15,()=>b.Text(new string('x',100000))));Assert(Read(b.File()).Structure.Events.Last().Count==100000);Reject(()=>Read(Mutate(b.File(),x=>BinaryPrimitives.WriteUInt32LittleEndian(x.AsSpan(32),100001))),"UTF-16 length policy");});
        Case("unsigned count and width overflow reject without allocation",()=>{foreach(int offset in new[]{16,32})Reject(()=>Read(Mutate(Primitive(1,1),b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset),uint.MaxValue))));Reject(()=>Read(Mutate(Array(1,1,0,0),b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(40),uint.MaxValue))));var t=new SyntheticR2();t.Root(1,1,()=>t.Field(2,15,()=>t.Text("")));Reject(()=>Read(Mutate(t.File(),b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(32),uint.MaxValue))));});
        Case("nesting exact boundary128 and129",()=>{byte[] Nested(int depth){var b=new SyntheticR2();void Body(int remaining){b.Body(remaining==0?0u:1u,()=>{if(remaining>0)b.Field(1,17,()=>Body(remaining-1));});}b.Word(1);Body(depth);return b.File();}Assert(Read(Nested(128)).Structure.Summary.MaximumDepth==128);Reject(()=>Read(Nested(129)),"Nesting policy");});
        Case("value event exact boundary2000000 and2000001",()=>{byte[] Many(uint last){var b=new SyntheticR2();b.Root(1,4,()=>{foreach(uint n in new uint[]{500000,500000,500000,last})b.Field(1,-1,()=>b.Array(1,1,n,0,()=>b.Bytes.AddRange(new byte[(int)n])));});return b.File();}Assert(Read(Many(499995)).Structure.Summary.ValueEvents==2000000);GC.Collect();Reject(()=>Read(Many(499996)),"Value-event policy");});
        Case("decoded cap, overflow and encoded overflow",()=>{Reject(()=>Read(Mutate(baseline,b=>BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),Document.Limit+1UL))));Reject(()=>Read(Mutate(baseline,b=>BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),ulong.MaxValue))));Reject(()=>Read(Mutate(baseline,b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(28),uint.MaxValue))));});
        Case("oversized source rejects before checksum allocation",()=>Reject(()=>Read(new byte[Document.Limit+1]),"Input length"));
        Case("source exact64MiB reaches structural validation",()=>
        {
            var b=new byte[Document.Limit];new byte[]{68,83,83,83,2}.CopyTo(b,0);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(16),10001);SyntheticR2.Recheck(b);
            Reject(()=>Read(b),"Object field policy");
        });
        Case("decoded exact64MiB reaches structure and expansion64MiBplus1 rejects",()=>
        {
            var payload=new byte[Document.Limit];BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4),10001);
            Reject(()=>Read(SyntheticR2.Wrap(payload,true)),"Object field policy");
            payload=new byte[Document.Limit+1];var file=SyntheticR2.Wrap(payload,true);BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(32),Document.Limit);SyntheticR2.Recheck(file);
            Reject(()=>Read(file),"Decoded expansion");
        });
        Case("wrapper length relation and discriminator enforced",()=>{Reject(()=>Read(Mutate(baseline,b=>BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(16),0))));Reject(()=>Read(Mutate(baseline,b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(24),2))));});
        Case("decoded declared size too short or long",()=>{var length=BinaryPrimitives.ReadUInt64LittleEndian(baseline.AsSpan(32));foreach(ulong n in new[]{length-1,length+1})Reject(()=>Read(Mutate(baseline,b=>BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),n))));});
        Case("compressed truncation and unused region bytes with valid wrapper",()=>
        {
            int encoded=(int)BinaryPrimitives.ReadUInt32LittleEndian(baseline.AsSpan(28));
            byte[] Region(int length)
            {
                var b=new byte[((40+length+3)&~3)+4];baseline.AsSpan(0,40).CopyTo(b);
                baseline.AsSpan(40,Math.Min(length,encoded)).CopyTo(b.AsSpan(40));
                BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(28),(uint)length);BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(16),(ulong)length+16);SyntheticR2.Recheck(b);return b;
            }
            for(int length=1;length<encoded;length++)Reject(()=>Read(Region(length)),"DEFLATE");
            Reject(()=>Read(Region(encoded+1)),"consumed region mismatch");
        });
        Case("poststream nonzero padding and excessive zeros reject",()=>{var b=SafeFixture();int end=40+(int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(28));if(end==b.Length-4){b=SyntheticR2.Wrap([],true);end=42;}Reject(()=>Read(Mutate(b,x=>x[end]=1)));var longer=new byte[baseline.Length+4];baseline.AsSpan(0,baseline.Length-4).CopyTo(longer);SyntheticR2.Recheck(longer);Reject(()=>Read(longer));});
        Case("empty DEFLATE output and empty structural sequence",()=>{var d=Read(SyntheticR2.Wrap([],true));Assert(d.Structure.Summary.Records==0&&d.Structure.Summary.ValueEvents==0);});
        Case("exact serialization, immutable snapshots and reinspection",()=>{var b=SafeFixture();var original=(byte[])b.Clone();var d=Read(b);b[0]=0;var copy=d.SerializeUnchanged();copy[0]=0;var payload=d.Structure.CopyPayload();if(payload.Length>0)payload[0]^=1;Assert(d.SerializeUnchanged().SequenceEqual(original));var again=Read(d.SerializeUnchanged());Assert(again.Metadata==d.Metadata&&again.Structure.Events.SequenceEqual(d.Structure.Events));});
        Case("single and multiple edits remain unsupported",()=>{var d=Read(baseline);Reject(()=>d.RequestEdits(new Dictionary<string,string>{{"a","1"}}));Reject(()=>d.RequestEdits(new Dictionary<string,string>{{"a","1"},{"b","2"}}));Assert(d.SerializeUnchanged().SequenceEqual(baseline));});
        Case("r2 capability explicitly admitted without writes",()=>{Assert(Profiles.Resolve("F1-F9/revision-2","2").CanRead&&!Profiles.Observed.CanWrite);Reject(()=>Profiles.Resolve("unknown","2"));Reject(()=>Profiles.Resolve("F1-F9/revision-2","3"));});
        Case("retained safe-copy cases migrated to checked structural fixture",()=>Assert(Checks.RunCopyOnly(retainedForR4?"test-results-copy-retained-r4.json":retainedForR3?"test-results-copy-retained-r3.json":"test-results-copy-r2.json")==0));
        Case("CLI checksum structural aggregate and no raw text",()=>{var b=new SyntheticR2();b.Root(1,1,()=>b.Field(2,15,()=>b.Text("SYNTHETIC_PRIVATE_TEXT")));var path=Path.Combine(Directory.GetCurrentDirectory(),"cli-r2.synthetic");File.WriteAllBytes(path,b.File());var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in new[]{Assembly.GetExecutingAssembly().Location,"inspect",path})start.ArgumentList.Add(arg);using var p=Process.Start(start)!;string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();p.WaitForExit();Assert(p.ExitCode==0&&output.Contains("CHECKSUM_MATCHED")&&output.Contains("Structure=EXPERIMENTAL")&&output.Contains("Records=1")&&!output.Contains("SYNTHETIC_PRIVATE_TEXT"));File.Delete(path);});
        var after=Hash(baseline);Case("synthetic input hash remains unchanged",()=>Assert(before==after));
        var sourceHashes=Directory.GetFiles(Directory.GetCurrentDirectory(),"*.cs").Concat(Directory.GetFiles(Directory.GetCurrentDirectory(),"*.csproj")).Order().Select(p=>new {file=Path.GetFileName(p),sha256=Hash(File.ReadAllBytes(p))}).ToArray();
        File.WriteAllText(retainedForR4?"test-results-r2-retained-r4.json":retainedForR3?"test-results-r2-retained-r3.json":"test-results-r2.json",JsonSerializer.Serialize(new{executedAtUtc=DateTimeOffset.UtcNow,scope="Synthetic only; experimental structural reading, no game validity",runtime=Environment.Version.ToString(),cases=results,failed=failures,inputHashes=new{before,after},sourceHashes},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"R2 executed {results.Count} cases; failures={failures}. See {(retainedForR4?"test-results-r2-retained-r4.json":retainedForR3?"test-results-r2-retained-r3.json":"test-results-r2.json")}.");return failures==0?0:1;
    }
}



