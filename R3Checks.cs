using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using FreshFoundation;
internal static class R3Checks
{
    private static readonly List<object> results=[];
    private static int failed;
    private static void Assert(bool condition,string message="Assertion failed") {if(!condition)throw new Exception(message);}
    private static void Case(string name,Action test) {try{test();results.Add(new{name,status="PASS"});}catch(Exception e){failed++;results.Add(new{name,status="FAIL",error=e.GetType().Name,message=e.Message});}}
    private static void Reject(Action test) {try{test();}catch(Exception e)when(e is ArgumentException or InvalidDataException or NotSupportedException){return;}throw new Exception("Expected rejection");}
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static byte[] Fixture(int tag,uint width,bool compressed)
    {
        var b=new SyntheticR2();b.Root(0xD00D,4,()=>
        {
            b.Field(0xAA,15,()=>b.Text("KEEP_RAW_TEXT\0"));
            b.Field(0xAA,1,()=>b.Primitive(1));
            b.Field(0xAA,tag,()=>b.Primitive(width));
            b.Field(0xBB,17,()=>b.Body(1,()=>b.Field(0xCC,7,()=>b.Primitive(4))));
        });
        var file=b.File(compressed);if(compressed){file[12]=0xE1;file[13]=0xA2;file[14]=0xB3;file[15]=0xC4;SyntheticR2.Recheck(file);}return file;
    }
    private static PrimitiveReplacement Request(Document doc,int index,byte mask=0x5A)
    {
        var value=doc.Structure.Events[index];var raw=doc.Structure.CopyPayload().AsSpan(value.DataCoordinate!.Value-12,(int)value.Width!.Value).ToArray();
        return new(index,raw,raw.Select(b=>(byte)(b^mask)).ToArray());
    }
    private static void Preserved(Document source,StructuralSerializationResult result,IReadOnlyList<PrimitiveReplacement> requests)
    {
        Assert(result.Status=="EXPERIMENTAL"&&result.SourceDigest==source.Digest);
        var output=result.CopyFileBytes();var reopened=Document.Inspect(output);Assert(reopened.Structure.Events.SequenceEqual(source.Structure.Events));
        var original=source.Structure.CopyPayload();var expected=(byte[])original.Clone();
        foreach(var r in requests)r.ReplacementBytes.CopyTo(expected,source.Structure.Events[r.EventIndex].DataCoordinate!.Value-12);
        Assert(reopened.Structure.CopyPayload().SequenceEqual(expected),"Bytes outside authorized spans changed");
        var first=source.SerializeUnchanged();
        if(source.Metadata.Flags==0){Assert(output.AsSpan(0,12).SequenceEqual(first.AsSpan(0,12)));Assert(output.Length==first.Length);}
        else
        {
            for(int i=0;i<40;i++)if(!(i>=16&&i<24)&&!(i>=28&&i<40))Assert(output[i]==first[i],"Opaque envelope byte changed");
            uint encoded=BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(28));
            Assert(BinaryPrimitives.ReadUInt64LittleEndian(output.AsSpan(16))==(ulong)encoded+16);
            Assert(BinaryPrimitives.ReadUInt64LittleEndian(output.AsSpan(32))==(ulong)expected.Length);
            int padding=output.Length-44-(int)encoded;Assert(padding is >=0 and <=3 && output.Length%4==0);
            Assert(output.AsSpan(40+(int)encoded,padding).IndexOfAnyExcept((byte)0)<0);
        }
        Assert(ObservedChecksum.Calculate(output.AsSpan(0,output.Length-4))==BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(output.Length-4)));
    }
    internal static int Run(bool retainedForR4=false)
    {
        results.Clear();failed=0;
        var historical=new[]{"test-results.json","test-results-r2.json","test-results-copy-r2.json"}.ToDictionary(p=>p,p=>Hash(File.ReadAllBytes(p)));
        foreach(bool compressed in new[]{false,true})
        {
            string flags=compressed?"8":"0";
            Case($"flags{flags} no requests and identical bytes preserve original",()=>{var bytes=Fixture(16,12,compressed);var d=Document.Inspect(bytes);Assert(ExperimentalStructuralSerializer.Replace(d,[]).CopyFileBytes().SequenceEqual(bytes));int i=d.Structure.Events.ToList().FindIndex(v=>v.DataCoordinate!=null);var r=Request(d,i,0);Assert(ExperimentalStructuralSerializer.Replace(d,[r]).CopyFileBytes().SequenceEqual(bytes));});
            foreach(var pair in new (int tag,uint width)[]{(1,1),(1,2),(1,4),(2,1),(3,1),(7,4),(8,4),(11,4),(9,8),(12,8),(16,4),(16,8),(16,12),(16,16),(16,116)})
                Case($"flags{flags} single primitive {pair.tag}/{pair.width}",()=>{var d=Document.Inspect(Fixture(pair.tag,pair.width,compressed));int index=d.Structure.Events.ToList().FindIndex(e=>e.Tag==pair.tag&&e.Width==pair.width&&e.Identifier==0xAA&&e.DataCoordinate!=null);var r=Request(d,index);var original=d.SerializeUnchanged();Preserved(d,ExperimentalStructuralSerializer.Replace(d,[r]),[r]);Assert(original.SequenceEqual(d.SerializeUnchanged()));});
            Case($"flags{flags} multiple spans preserve duplicate IDs text and nonzero padding",()=>{var d=Document.Inspect(Fixture(16,116,compressed));var requests=d.Structure.Events.Select((e,i)=>(e,i)).Where(p=>p.e.DataCoordinate!=null).Select(p=>Request(d,p.i)).ToArray();var result=ExperimentalStructuralSerializer.Replace(d,requests.Reverse().ToArray());Preserved(d,result,requests);Assert(d.Structure.Summary.NonzeroPaddingBytes>0);});
            foreach(var pair in new (int tag,uint width)[]{(2,1),(7,4),(16,12),(1,4),(8,4),(16,16),(11,4),(9,8),(1,1),(16,8),(16,116),(1,2)})
                Case($"flags{flags} primitive array occurrences {pair.tag}/{pair.width}",()=>{var b=new SyntheticR2();b.Root(8,1,()=>b.Field(9,-1,()=>b.Array(pair.tag,pair.width,3,0,()=>{for(int i=0;i<3;i++)b.Primitive(pair.width,true);})));var d=Document.Inspect(b.File(compressed));var indices=d.Structure.Events.Select((e,i)=>(e,i)).Where(p=>p.e.DataCoordinate!=null).Select(p=>p.i).ToArray();Assert(indices.Length==3);var requests=new[]{Request(d,indices[0]),Request(d,indices[2])};Preserved(d,ExperimentalStructuralSerializer.Replace(d,requests),requests);});
        }
        var source=Document.Inspect(Fixture(16,116,true));var sourceBefore=source.SerializeUnchanged();var primitiveIndex=source.Structure.Events.ToList().FindIndex(e=>e.DataCoordinate!=null);var good=Request(source,primitiveIndex);
        Case("nonprimitive roots objects arrays and text cannot be targeted",()=>
        {
            var b=new SyntheticR2();b.Root(1,3,()=>{b.Field(2,15,()=>b.Text("fixed"));b.Field(3,17,()=>b.Body(0,()=>{}));b.Field(4,-1,()=>b.Array(1,1,1,0,()=>b.Bytes.Add(8)));});
            var d=Document.Inspect(b.File());for(int i=0;i<d.Structure.Events.Count;i++)if(d.Structure.Events[i].DataCoordinate==null){int index=i;Reject(()=>ExperimentalStructuralSerializer.Replace(d,[new(index,[],[])]));}
        });
        Case("negative unknown and out-of-range event identities",()=>{foreach(int i in new[]{-1,source.Structure.Events.Count,int.MaxValue})Reject(()=>ExperimentalStructuralSerializer.Replace(source,[new(i,[],[])]));});
        Case("duplicate and overlapping identical occurrences rejected",()=>Reject(()=>ExperimentalStructuralSerializer.Replace(source,[good,good])));
        Case("wrong old bytes and replacement widths rejected",()=>{Reject(()=>ExperimentalStructuralSerializer.Replace(source,[good with{ExpectedBytes=[0xFF]}]));Reject(()=>ExperimentalStructuralSerializer.Replace(source,[good with{ExpectedBytes=[]}]));Reject(()=>ExperimentalStructuralSerializer.Replace(source,[good with{ReplacementBytes=[]}]));Reject(()=>ExperimentalStructuralSerializer.Replace(source,[good with{ReplacementBytes=new byte[2]}]));});
        Case("null and malformed requests rejected",()=>{Reject(()=>ExperimentalStructuralSerializer.Replace(null!,[]));Reject(()=>ExperimentalStructuralSerializer.Replace(source,null!));Reject(()=>ExperimentalStructuralSerializer.Replace(source,[null!]));Reject(()=>ExperimentalStructuralSerializer.Replace(source,[good with{ExpectedBytes=null!}]));Reject(()=>ExperimentalStructuralSerializer.Replace(source,[good with{ReplacementBytes=null!}]));});
        Case("valid first invalid second is transactional",()=>{var expectedBefore=(byte[])good.ExpectedBytes.Clone();var replacementBefore=(byte[])good.ReplacementBytes.Clone();Reject(()=>ExperimentalStructuralSerializer.Replace(source,[good,new(-1,[],[])]));Assert(source.SerializeUnchanged().SequenceEqual(sourceBefore));Assert(good.ExpectedBytes.SequenceEqual(expectedBefore)&&good.ReplacementBytes.SequenceEqual(replacementBefore));});
        Case("independent results and defensive request/result buffers",()=>{var first=Request(source,primitiveIndex,1);var second=Request(source,primitiveIndex,2);var a=ExperimentalStructuralSerializer.Replace(source,[first]);var saved=a.CopyFileBytes();var b=ExperimentalStructuralSerializer.Replace(source,[second]);first.ExpectedBytes[0]^=9;first.ReplacementBytes[0]^=9;var returned=a.CopyFileBytes();returned[0]^=1;var payload=a.InspectedDocument.Structure.CopyPayload();payload[0]^=1;Assert(a.CopyFileBytes().SequenceEqual(saved));Assert(!a.CopyFileBytes().SequenceEqual(b.CopyFileBytes()));Assert(source.SerializeUnchanged().SequenceEqual(sourceBefore));});
        Case("expected bytes always refer to original not prior result",()=>{var r=ExperimentalStructuralSerializer.Replace(source,[good]);Reject(()=>ExperimentalStructuralSerializer.Replace(source,[good with{ExpectedBytes=good.ReplacementBytes}]));Assert(!r.CopyFileBytes().SequenceEqual(sourceBefore));});
        Case("header padding IDs counts and tags have no target identity",()=>{Assert(source.Structure.Events.Where(e=>e.DataCoordinate!=null).All(e=>e.DataCoordinate>=12&&e.Width!=null));Reject(()=>ExperimentalStructuralSerializer.Replace(source,[new(0,new byte[40],new byte[40])]));var i=source.Structure.Events.ToList().FindIndex(e=>e.Tag==16);var target=source.Structure.Events[i];Assert(target.DataCoordinate>target.Coordinate);Reject(()=>ExperimentalStructuralSerializer.Replace(source,[new(i,new byte[(int)target.Width!+1],new byte[(int)target.Width!+1])]));});
        Case("growth shrinkage and raw-deflate reconstruction",()=>
        {
            var random=new byte[4096];uint state=0xDADABEEF;for(int i=0;i<random.Length;i++){state^=state<<13;state^=state>>17;state^=state<<5;random[i]=(byte)state;}
            Document ArrayFile(byte[] data){var b=new SyntheticR2();b.Root(1,1,()=>b.Field(2,-1,()=>b.Array(1,1,(uint)data.Length,0,()=>b.Bytes.AddRange(data))));return Document.Inspect(b.File(true));}
            var d=ArrayFile(new byte[random.Length]);var requests=d.Structure.Events.Select((e,i)=>(e,i)).Where(p=>p.e.DataCoordinate!=null).Select((p,n)=>new PrimitiveReplacement(p.i,[0],[random[n]])).ToArray();
            var grown=ExperimentalStructuralSerializer.Replace(d,requests);Preserved(d,grown,requests);Assert(grown.InspectedDocument.Metadata.EncodedLength>d.Metadata.EncodedLength);
            var r=grown.InspectedDocument;var shrink=r.Structure.Events.Select((e,i)=>(e,i)).Where(p=>p.e.DataCoordinate!=null).Select((p,n)=>new PrimitiveReplacement(p.i,[random[n]],[0])).ToArray();var shrunk=ExperimentalStructuralSerializer.Replace(r,shrink);Preserved(r,shrunk,shrink);Assert(shrunk.InspectedDocument.Metadata.EncodedLength<r.Metadata.EncodedLength);
        });
        Case("all four poststream pad lengths observed in bounded synthetic search",()=>
        {
            var pads=new HashSet<int>();
            for(int n=1;n<=256&&pads.Count<4;n++)
            {
                var b=new SyntheticR2();b.Root(1,1,()=>b.Field(2,-1,()=>b.Array(1,1,(uint)n,0,()=>{for(int i=0;i<n;i++)b.Bytes.Add((byte)((i*37+n)%251));})));
                var d=Document.Inspect(b.File(true));int index=d.Structure.Events.ToList().FindIndex(e=>e.DataCoordinate!=null);var request=Request(d,index,(byte)(n%255+1));var r=ExperimentalStructuralSerializer.Replace(d,[request]);Preserved(d,r,[request]);pads.Add(r.CopyFileBytes().Length-44-(int)r.InspectedDocument.Metadata.EncodedLength!.Value);
            }
            Assert(pads.SetEquals(new[]{0,1,2,3}),"Not all pad lengths reached");
        });
        Case("actual encoded write cap boundary and overflow rejected",()=>{using var buffer=new ExperimentalStructuralSerializer.CappedBuffer(Document.Limit-44);buffer.Write(new byte[Document.Limit-44]);Assert(buffer.Length==Document.Limit-44);Reject(()=>buffer.WriteByte(0));Assert(buffer.Length==Document.Limit-44);});
        Case("replacement policy rejects oversized collection before access",()=>Reject(()=>ExperimentalStructuralSerializer.Replace(source,new OversizedRequests())));
        Case("bad checksum unsupported version cannot enter and game edits stay denied",()=>{var bad=(byte[])sourceBefore.Clone();bad[^1]^=1;Reject(()=>Document.Inspect(bad));bad=(byte[])sourceBefore.Clone();bad[4]=3;SyntheticR2.Recheck(bad);Reject(()=>Document.Inspect(bad));Assert(!Profiles.Observed.CanWrite);Reject(()=>source.RequestEdits(new Dictionary<string,string>{{"currency","1"}}));});
        Case("retained R2 and copy suites run into R3-specific files",()=>Assert(R2Checks.Run(true,retainedForR4)==0));
        Case("historical R1 R2 reports unchanged",()=>{foreach(var pair in historical)Assert(Hash(File.ReadAllBytes(pair.Key))==pair.Value,pair.Key+" changed");});
        Case("source remains unchanged after every R3 operation",()=>Assert(source.SerializeUnchanged().SequenceEqual(sourceBefore)));
        var sourceHashes=Directory.GetFiles(Directory.GetCurrentDirectory(),"*.cs").Concat(Directory.GetFiles(Directory.GetCurrentDirectory(),"*.csproj")).Order().Select(p=>new{file=Path.GetFileName(p),sha256=Hash(File.ReadAllBytes(p))}).ToArray();
        File.WriteAllText(retainedForR4?"test-results-r3-retained-r4.json":"test-results-r3.json",JsonSerializer.Serialize(new{executedAtUtc=DateTimeOffset.UtcNow,scope="Synthetic experimental primitive byte serialization only; game acceptance UNVERIFIED",runtime=Environment.Version.ToString(),cases=results,failed,historicalReportHashes=historical,inputHashes=new{before=Hash(sourceBefore),after=Hash(source.SerializeUnchanged())},sourceHashes},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"R3 executed {results.Count} cases; failures={failed}. See {(retainedForR4?"test-results-r3-retained-r4.json":"test-results-r3.json")}.");return failed==0?0:1;
    }
    private sealed class OversizedRequests : IReadOnlyList<PrimitiveReplacement>
    {
        public int Count=>StructureWalker.EventLimit+1;
        public PrimitiveReplacement this[int index]=>throw new Exception("Must reject before indexing");
        public IEnumerator<PrimitiveReplacement> GetEnumerator()=>throw new Exception("Must reject before enumeration");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}

