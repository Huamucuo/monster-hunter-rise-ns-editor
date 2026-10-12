using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using FreshFoundation;
internal static class Checks
{
    private static readonly List<object> results = [];
    private static int failed;
    private static bool copyOnly;
    private static string copyReport="test-results-copy-r2.json";
    internal static int RunCopyOnly(string report="test-results-copy-r2.json") {copyReport=report;copyOnly=true;results.Clear();failed=0;return Run();}
    private static string root = "";
    private static byte[] Opaque() { var b = new byte[37]; new byte[]{68,83,83,83,2}.CopyTo(b,0); b[16]=173; b[36]=99; return b; }
    private static byte[] Compressed(byte[]? content = null)
    {
        if(copyOnly)return R2Checks.SafeFixture();
        content ??= Enumerable.Range(0, 513).Select(x => (byte)(x % 251)).ToArray();
        using var memory = new MemoryStream(); using(var encoder = new DeflateStream(memory, CompressionLevel.Optimal,true)) encoder.Write(content);
        var compressed = content.Length == 0 ? new byte[] { 3, 0 } : memory.ToArray(); var b = new byte[40 + compressed.Length + 3]; Opaque().AsSpan(0,12).CopyTo(b); b[8]=8;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(28), (uint)compressed.Length); BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),(ulong)content.Length);
        compressed.CopyTo(b,40); b[^1]=201; return b;
    }
    private static void Assert(bool value) { if(!value) throw new Exception("Assertion failed"); }
    private static void Deny(Action action) { try { action(); } catch(Exception e) when(e is IOException or InvalidDataException or NotSupportedException or InvalidOperationException or ArgumentException) { return; } throw new Exception("Expected rejection"); }
    private static void Case(string name, Action action) { if(copyOnly && !CopyCase(name))return; try { action(); results.Add(new {name, status="PASS"}); } catch(Exception e) { failed++; results.Add(new{name,status="FAIL",error=e.GetType().Name,message=e.Message}); } }
    private static string Source(string name, byte[]? bytes = null) { var path=Path.Combine(root,name); File.WriteAllBytes(path,bytes ?? Compressed()); return path; }
    private static CopySession Select(string path) { var session=new CopySession(); session.Select(path); return session; }
    private static bool CopyCase(string name) => name is "oversized file rejects before allocation" or "no selection state and export failure" or "selection clears stale document on failure" or "export backup restore byte identity and source untouched" or "source alias export denied" or "existing output remains untouched" or "changed source detected" or "replaced same-byte source detected" or "backup digest mismatch creates nothing" or "restore over selected input denied" or "restore over backup denied" or "existing backup cannot be overwritten by copy primitive" or "hard-linked input denied" or "hard-linked destination denied" or "directory junction source and destination denied" or "failed output retains completed backup and no output" or "partially written output cleanup" or "alternate stream path denied" or "CLI no selection and read-only status" or "baseline source digest unchanged after all cases";
    public static int Run()
    {
        root=Path.Combine(Directory.GetCurrentDirectory(),"synthetic-run-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var baseline = Source("baseline.synthetic"); var before=SafeFiles.Digest(File.ReadAllBytes(baseline));
        Case("opaque profile retains unparsed remainder",()=> {var d=Document.Inspect(Opaque());Assert(d.Metadata.Flags==0 && d.Metadata.DecodedLength is null);});
        Case("compressed bounded decode",()=> {var d=Document.Inspect(Compressed());Assert(d.Metadata.DecodedLength==513 && d.Status=="READ_ONLY" && d.Integrity=="UNVERIFIED" && d.GameSemantics=="UNVERIFIED");});
        Case("empty DEFLATE output",()=>Assert(Document.Inspect(Compressed([])).Metadata.DecodedLength==0));
        Case("immutable input and returned bytes",()=> {var b=Opaque();var d=Document.Inspect(b);b[16]=0;var output=d.SerializeUnchanged();output[16]=1;Assert(d.SerializeUnchanged()[16]==173);});
        Case("opaque exact serialization and reparse",()=> {var b=Opaque();var d=Document.Inspect(b);var r=d.SerializeUnchanged();Assert(b.SequenceEqual(r)&&Document.Inspect(r).Metadata==d.Metadata);});
        Case("compressed exact serialization includes opaque trailing bytes",()=> {var b=Compressed();var d=Document.Inspect(b);var r=d.SerializeUnchanged();Assert(b.SequenceEqual(r)&&Document.Inspect(r).Metadata==d.Metadata);});
        Case("single edit rejects without mutation",()=> {var b=Opaque();var d=Document.Inspect(b);Deny(()=>d.RequestEdits(new Dictionary<string,string>{{"field","1"}}));Assert(b.SequenceEqual(d.SerializeUnchanged()));});
        Case("multiple edits reject as whole",()=> {var b=Compressed();var d=Document.Inspect(b);Deny(()=>d.RequestEdits(new Dictionary<string,string>{{"a","1"},{"b","2"}}));Assert(b.SequenceEqual(d.SerializeUnchanged()));});
        Case("all short initial headers reject",()=> {for(int i=0;i<12;i++) Deny(()=>Document.Inspect(new byte[i]));});
        Case("compressed envelope headers truncated",()=> {var b=Compressed();for(int i=12;i<40;i++) Deny(()=>Document.Inspect(b.AsSpan(0,i)));});
        Case("wrong magic",()=>{var b=Opaque();b[0]=0;Deny(()=>Document.Inspect(b));});
        Case("wrong version",()=>{var b=Opaque();b[4]=3;Deny(()=>Document.Inspect(b));});
        Case("wrong flags",()=>{var b=Opaque();b[8]=9;Deny(()=>Document.Inspect(b));});
        Case("compressed region beyond input",()=>{var b=Compressed();BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(28),(uint)b.Length);Deny(()=>Document.Inspect(b));});
        Case("encoded length overflow",()=>{var b=Compressed();BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(28),uint.MaxValue);Deny(()=>Document.Inspect(b));});
        Case("decoded length overflow",()=>{var b=Compressed();BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),ulong.MaxValue);Deny(()=>Document.Inspect(b));});
        Case("decoded policy cap",()=>{var b=Compressed();BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),Document.Limit+1UL);Deny(()=>Document.Inspect(b));});
        Case("declared decoded length too large",()=>{var b=Compressed();BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),514);Deny(()=>Document.Inspect(b));});
        Case("decoded expansion exceeds declared size",()=>{var b=Compressed(new byte[100000]);BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),10);Deny(()=>Document.Inspect(b));});
        Case("all shortened DEFLATE regions reject",()=>{var b=Compressed();var n=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(28));for(uint i=0;i<n;i++){var copy=(byte[])b.Clone();BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(28),i);Deny(()=>Document.Inspect(copy));}});
        Case("unused bytes within compressed region reject",()=>{var b=Compressed();var n=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(28));BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(28),n+1);Deny(()=>Document.Inspect(b));});
        Case("invalid DEFLATE block rejects",()=>{var b=Compressed();b[40]=7;Deny(()=>Document.Inspect(b));});
        Case("oversized memory source rejects",()=>Deny(()=>Document.Inspect(new byte[Document.Limit+1])));
        Case("oversized file rejects before allocation",()=>{var p=Path.Combine(root,"large.synthetic");using(var f=File.Create(p))f.SetLength(Document.Limit+1L);Deny(()=>Select(p));});
        Case("profile admission gating",()=>{Assert(Profiles.Resolve("F1-F4/revision-1","1").CanRead&&!Profiles.Observed.CanWrite);Deny(()=>Profiles.Resolve("unknown","1"));Deny(()=>Profiles.Resolve("F1-F4/revision-1","2"));});
        Case("no selection state and export failure",()=>{var s=new CopySession();Assert(s.State==Stage.NoSelection);Deny(()=>s.Export(Path.Combine(root,"absent"),root));Assert(s.State==Stage.Failed);});
        Case("selection clears stale document on failure",()=>{var s=Select(baseline);Assert(s.State==Stage.SelectedReadOnly);Deny(()=>s.Select(Path.Combine(root,"missing")));Assert(s.Selected is null&&s.State==Stage.Failed);});
        Case("export backup restore byte identity and source untouched",()=>{var s=Select(baseline);var r=s.Export(Path.Combine(root,"export.copy"),root);Assert(s.State==Stage.ExportSucceeded);Assert(SafeFiles.Digest(File.ReadAllBytes(r.BackupPath))==before && SafeFiles.Digest(File.ReadAllBytes(r.OutputPath))==before);var restored=s.Restore(r.BackupPath,r.Digest,Path.Combine(root,"restored.copy"));Assert(SafeFiles.Digest(File.ReadAllBytes(restored))==before);});
        Case("source alias export denied",()=>Deny(()=>Select(baseline).Export(Path.Combine(root,".","baseline.synthetic"),root)));
        Case("existing output remains untouched",()=>{var p=Source("existing.copy",Opaque());var hash=SafeFiles.Digest(File.ReadAllBytes(p));Deny(()=>Select(baseline).Export(p,root));Assert(SafeFiles.Digest(File.ReadAllBytes(p))==hash);});
        Case("changed source detected",()=>{var p=Source("changed.synthetic");var s=Select(p);File.WriteAllBytes(p,Opaque());var output=Path.Combine(root,"changed.copy");Deny(()=>s.Export(output,root));Assert(!File.Exists(output));});
        Case("replaced same-byte source detected",()=>{var p=Source("replaced.synthetic");var s=Select(p);File.Move(p,p+".old");File.WriteAllBytes(p,Compressed());Deny(()=>s.Export(Path.Combine(root,"replaced.copy"),root));});
        Case("backup digest mismatch creates nothing",()=>{var p=Path.Combine(root,"wrong-digest.copy");Deny(()=>Select(baseline).Restore(baseline,new string('0',64),p));Assert(!File.Exists(p));});
        Case("restore over selected input denied",()=>Deny(()=>Select(baseline).Restore(baseline,before,baseline)));
        Case("restore over backup denied",()=>{var p=Source("restore-backup.synthetic");Deny(()=>Select(baseline).Restore(p,SafeFiles.Digest(File.ReadAllBytes(p)),p));});
        Case("existing backup cannot be overwritten by copy primitive",()=>{var p=Source("existing-backup.copy",Opaque());var b=File.ReadAllBytes(p);Deny(()=>SafeFiles.CreateCopy(p,Compressed()));Assert(b.SequenceEqual(File.ReadAllBytes(p)));});
        Case("hard-linked input denied",()=>{var p=Source("hardlink-source.synthetic");var link=Path.Combine(root,"hardlink.synthetic");Assert(CreateHardLink(link,p,IntPtr.Zero));Deny(()=>Select(p));Deny(()=>Select(link));});
        Case("hard-linked destination denied",()=>{var p=Source("hardlink-dest-source.synthetic");var link=Path.Combine(root,"hardlink-dest.synthetic");Assert(CreateHardLink(link,p,IntPtr.Zero));Deny(()=>Select(baseline).Export(link,root));});
        Case("directory junction source and destination denied",()=>{var target=Path.Combine(root,"junction-target");Directory.CreateDirectory(target);var link=Path.Combine(root,"junction-link");var start=new ProcessStartInfo("cmd.exe"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("/c");start.ArgumentList.Add("mklink");start.ArgumentList.Add("/J");start.ArgumentList.Add(link);start.ArgumentList.Add(target);using var p=Process.Start(start)!;p.WaitForExit();Assert(p.ExitCode==0);File.WriteAllBytes(Path.Combine(target,"input.synthetic"),Opaque());Deny(()=>Select(Path.Combine(link,"input.synthetic")));Deny(()=>Select(baseline).Export(Path.Combine(link,"output.copy"),root));Directory.Delete(link);});
        Case("failed output retains completed backup and no output",()=>{var s=Select(baseline);var prior=Directory.GetDirectories(root,"backup-*").Length;var path=Path.Combine(root,new string('x',300));Deny(()=>s.Export(path,root));Assert(s.State==Stage.Failed && !File.Exists(path));Assert(Directory.GetDirectories(root,"backup-*").Length==prior+1);});
        Case("partially written output cleanup",()=>{var p=Path.Combine(root,"partial.copy");Deny(()=>SafeFiles.CreateCopy(p,new byte[Document.Limit+1]));Assert(!File.Exists(p));});
        Case("alternate stream path denied",()=>Deny(()=>Select(baseline).Export(baseline+":stream",root)));
        Case("CLI no selection and read-only status",()=>{var no=Cli([]);Assert(no.code==0&&no.text.Contains("NO_SELECTION"));var selected=Cli(["inspect",baseline]);Assert(selected.code==0&&selected.text.Contains("READ_ONLY Integrity=UNVERIFIED GameSemantics=UNVERIFIED"));var bad=Cli(["edit",baseline]);Assert(bad.code==1&&bad.text.Contains("FAILED"));});
        var after=SafeFiles.Digest(File.ReadAllBytes(baseline));Case("baseline source digest unchanged after all cases",()=>Assert(before==after));
        File.WriteAllText(copyOnly?copyReport:"test-results.json",JsonSerializer.Serialize(new {executedAtUtc=DateTimeOffset.UtcNow,scope="Synthetic envelope and copy tests only; game validity UNVERIFIED",runtime=Environment.Version.ToString(),cases=results,failed,sourceHashes=new{before,after}},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Executed {results.Count} synthetic cases; failures={failed}. See {(copyOnly?copyReport:"test-results.json")}.");
        return failed==0?0:1;
    }
    private static (int code,string text) Cli(string[] args) {var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);foreach(var a in args)start.ArgumentList.Add(a);using var p=Process.Start(start)!;var text=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();p.WaitForExit();return(p.ExitCode,text);}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CreateHardLink(string name,string existing,IntPtr reserved);
}




