using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using FreshFoundation;
internal static class R4Checks
{
    private static readonly List<object> results=[];
    private static int failed;
    private static string root="";
    private static void Assert(bool value,string message="Assertion failed"){if(!value)throw new Exception(message);}
    private static void Case(string name,Action action,string mechanism="real filesystem"){try{action();results.Add(new{name,status="PASS",mechanism});}catch(Exception e){failed++;results.Add(new{name,status="FAIL",mechanism,error=e.GetType().Name,message=e.Message});}}
    private static ExperimentalExportException Failure(Action action){try{action();}catch(ExperimentalExportException e){return e;}throw new Exception("Expected experimental failure");}
    private static void Reject(Action action){try{action();}catch(Exception e)when(e is IOException or InvalidDataException or NotSupportedException or InvalidOperationException or ArgumentException or UnauthorizedAccessException){return;}throw new Exception("Expected rejection");}
    private static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
    private static string FileHash(string path)=>Hash(File.ReadAllBytes(path));
    private static byte[] Fixture(bool compressed){var b=new SyntheticR2();b.Root(0xEC01,3,()=>{b.Field(4,1,()=>b.Primitive(1));b.Field(4,16,()=>b.Primitive(116));b.Field(5,15,()=>b.Text("PRESERVE_SYNTHETIC_TEXT\0"));});return b.File(compressed);}
    private static string NewDirectory(){var p=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
    private static (string folder,string input,ExperimentalExportSession session) Setup(bool compressed=true,Action<ExportPart,CopyPoint,string,FileStream?>? probe=null){var folder=NewDirectory();var input=Path.Combine(folder,"original.synthetic");File.WriteAllBytes(input,Fixture(compressed));var session=probe is null?new ExperimentalExportSession():new ExperimentalExportSession(probe);session.Select(input);return(folder,input,session);}
    private static PrimitiveReplacement[] Requests(Document doc,int count=1){var payload=doc.Structure.CopyPayload();return doc.Structure.Events.Select((e,i)=>(e,i)).Where(p=>p.e.DataCoordinate!=null).Take(count).Select(p=>{var old=payload.AsSpan(p.e.DataCoordinate!.Value-12,(int)p.e.Width!.Value).ToArray();return new PrimitiveReplacement(p.i,old,old.Select(x=>(byte)(x^0x35)).ToArray());}).ToArray();}
    private static void VerifySuccess(string input,string before,ExperimentalExportReceipt receipt,IReadOnlyList<PrimitiveReplacement> requests,Document original)
    {
        Assert(FileHash(input)==before&&receipt.OriginalDigest==before&&FileHash(receipt.BackupPath)==before);
        Assert(receipt.Status=="EXPERIMENTAL"&&receipt.GameAcceptance=="UNVERIFIED"&&FileHash(receipt.OutputPath)==receipt.ModifiedDigest);
        var output=Document.Inspect(File.ReadAllBytes(receipt.OutputPath));Assert(output.Structure.Events.SequenceEqual(original.Structure.Events));
        var expected=original.Structure.CopyPayload();foreach(var r in requests)r.ReplacementBytes.CopyTo(expected,original.Structure.Events[r.EventIndex].DataCoordinate!.Value-12);Assert(output.Structure.CopyPayload().SequenceEqual(expected));
        Assert(receipt.BackupPath!=receipt.OutputPath&&receipt.OutputPath!=input);
    }
    internal static int Run()
    {
        results.Clear();failed=0;root=Path.Combine(Directory.GetCurrentDirectory(),"synthetic-run-r4-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var history=Directory.GetFiles(Directory.GetCurrentDirectory(),"test-results*.json").Where(p=>!Path.GetFileName(p).Contains("r4")).ToDictionary(p=>Path.GetFileName(p),FileHash);
        var baseline=Setup();string baselineBefore=FileHash(baseline.input);
        foreach(bool compressed in new[]{false,true})foreach(int count in new[]{0,1,2})Case($"flags{(compressed?8:0)} export {(count==0?"no-op":count+" primitives")}",()=>
        {
            var t=Setup(compressed);var d=t.session.Selected!;var requests=Requests(d,count);string before=FileHash(t.input);var receipt=t.session.Export(requests,Path.Combine(t.folder,"modified.copy"),t.folder);
            Assert(t.session.State==ExperimentalExportState.ExportCompleted);VerifySuccess(t.input,before,receipt,requests,d);if(count==0)Assert(receipt.ModifiedDigest==before);
            var restored=t.session.RestoreOriginal(receipt.Backup,Path.Combine(t.folder,"restored.copy"));Assert(FileHash(restored)==before&&t.session.State==ExperimentalExportState.RestoreCompleted);
        });
        Case("identical requests no-op remains exact with original backup",()=>{var t=Setup();var requests=Requests(t.session.Selected!);requests= requests.Select(r=>r with{ReplacementBytes=(byte[])r.ExpectedBytes.Clone()}).ToArray();var receipt=t.session.Export(requests,Path.Combine(t.folder,"identical.copy"),t.folder);Assert(receipt.OriginalDigest==receipt.ModifiedDigest&&FileHash(receipt.BackupPath)==receipt.OriginalDigest);});
        Case("no selection creates no files",()=>{var folder=NewDirectory();var s=new ExperimentalExportSession();Assert(s.State==ExperimentalExportState.NoSelection);var e=Failure(()=>s.Export([],Path.Combine(folder,"no.copy"),folder));Assert(e.CompletedBackup is null&&s.State==ExperimentalExportState.Failed&&!Directory.EnumerateFileSystemEntries(folder).Any());});
        Case("failed reselection clears stale state",()=>{var t=Setup();Reject(()=>t.session.Select(Path.Combine(t.folder,"missing")));Assert(t.session.Selected is null&&t.session.SourcePath is null);Failure(()=>t.session.Export([],Path.Combine(t.folder,"stale.copy"),t.folder));Assert(Directory.GetFiles(t.folder).Length==1);});
        Case("invalid request transaction finishes before any side effect",()=>{var t=Setup();var valid=Requests(t.session.Selected!)[0];var e=Failure(()=>t.session.Export([valid,new(-1,[],[])],Path.Combine(t.folder,"invalid.copy"),t.folder));Assert(e.FailureStage==ExperimentalExportState.Preparing&&e.CompletedBackup is null&&Directory.GetDirectories(t.folder).Length==0&&Directory.GetFiles(t.folder).Length==1);});
        foreach(bool viaCount in new[]{true,false})Case($"reentrant selection through {(viaCount?"Count":"indexer")} cannot change binding",()=>
        {
            var t=Setup();var other=Path.Combine(t.folder,"other.synthetic");File.WriteAllBytes(other,Fixture(false));var selected=t.session.Selected;var requests=Requests(selected!);
            var attempted=new ReentrantRequests(requests,viaCount,()=>t.session.Select(other));
            var e=Failure(()=>t.session.Export(attempted,Path.Combine(t.folder,"reentrant.copy"),t.folder));
            Assert(e.CompletedBackup is null&&t.session.State==ExperimentalExportState.Failed&&ReferenceEquals(t.session.Selected,selected)&&t.session.SourcePath==t.input&&Directory.GetDirectories(t.folder).Length==0);
            var retry=t.session.Export(requests,Path.Combine(t.folder,"retry.copy"),t.folder);Assert(retry.OriginalDigest==FileHash(t.input));
        },"adversarial caller-provided collection on public API");
        Case("caught reentry still exports only original selection",()=>
        {
            var t=Setup();var other=Path.Combine(t.folder,"other.synthetic");File.WriteAllBytes(other,Fixture(false));var requests=Requests(t.session.Selected!);bool rejected=false;
            var attempted=new ReentrantRequests(requests,true,()=>{try{t.session.Select(other);}catch(InvalidOperationException){rejected=true;}});
            var receipt=t.session.Export(attempted,Path.Combine(t.folder,"bound.copy"),t.folder);Assert(rejected&&receipt.OriginalDigest==FileHash(t.input)&&FileHash(receipt.BackupPath)==FileHash(t.input)&&t.session.SourcePath==t.input);
        },"adversarial caller-provided collection catches rejection");
        Case("same file changed content detected before backup",()=>{var t=Setup();using(var file=new FileStream(t.input,FileMode.Open,FileAccess.Write,FileShare.None)){file.Position=12;file.WriteByte(0xEE);}var current=FileHash(t.input);var e=Failure(()=>t.session.Export([],Path.Combine(t.folder,"changed.copy"),t.folder));Assert(e.CompletedBackup is null&&FileHash(t.input)==current&&Directory.GetDirectories(t.folder).Length==0);});
        Case("equal bytes different file identity detected",()=>{var t=Setup();var bytes=File.ReadAllBytes(t.input);File.Move(t.input,t.input+".prior");File.WriteAllBytes(t.input,bytes);var e=Failure(()=>t.session.Export([],Path.Combine(t.folder,"replaced.copy"),t.folder));Assert(e.CompletedBackup is null&&Directory.GetDirectories(t.folder).Length==0);});
        Case("source locked against ordinary write and delete during output",()=>{bool checkedLease=false;string input="";var t=Setup(probe:(part,point,path,stream)=>{if(part==ExportPart.Output&&point==CopyPoint.BeforeCreate){Reject(()=>{using var writer=new FileStream(input,FileMode.Open,FileAccess.Write,FileShare.ReadWrite);});Reject(()=>File.Delete(input));checkedLease=true;}});input=t.input;t.session.Export([],Path.Combine(t.folder,"leased.copy"),t.folder);Assert(checkedLease&&File.Exists(input));},"real filesystem observation through internal hook");
        Case("input dot-path alias refused unchanged",()=>{var t=Setup();var before=FileHash(t.input);Failure(()=>t.session.Export([],Path.Combine(t.folder,".","original.synthetic"),t.folder));Assert(FileHash(t.input)==before&&Directory.GetDirectories(t.folder).Length==0);});
        Case("existing output preserved",()=>{var t=Setup();var output=Path.Combine(t.folder,"existing.copy");File.WriteAllBytes(output,[1,2,3]);var before=FileHash(output);Failure(()=>t.session.Export([],output,t.folder));Assert(FileHash(output)==before&&Directory.GetDirectories(t.folder).Length==0);});
        Case("previous backup cannot become output",()=>{var t=Setup();var r=t.session.Export([],Path.Combine(t.folder,"first.copy"),t.folder);var before=FileHash(r.BackupPath);Failure(()=>t.session.Export([],r.BackupPath,t.folder));Assert(FileHash(r.BackupPath)==before);});
        Case("directory target and source-file backup parent rejected",()=>{var t=Setup();Failure(()=>t.session.Export([],t.folder,t.folder));Failure(()=>t.session.Export([],Path.Combine(t.folder,"none.copy"),t.input));Assert(!File.Exists(Path.Combine(t.folder,"none.copy")));});
        Case("ADS device and network paths rejected before backup",()=>{var t=Setup();foreach(string path in new[]{t.input+":alternate",Path.Combine(t.folder,"NUL"),Path.Combine(t.folder,"CON.txt"),Path.Combine(t.folder,"COM1"),Path.Combine(t.folder,"LPT².log"),@"\\server\share\file",@"\\?\C:\unsafe"})Failure(()=>t.session.Export([],path,t.folder));Assert(Directory.GetDirectories(t.folder).Length==0);});
        Case("hard-linked selection and existing hard-link output denied",()=>{var t=Setup();var alias=Path.Combine(t.folder,"alias.synthetic");Assert(CreateHardLink(alias,t.input,IntPtr.Zero));Reject(()=>new ExperimentalExportSession().Select(alias));Reject(()=>new ExperimentalExportSession().Select(t.input));Failure(()=>t.session.Export([],alias,t.folder));Assert(File.Exists(t.input)&&File.Exists(alias));});
        Case("junction input output and backup parents denied",()=>{var t=Setup();var target=Path.Combine(t.folder,"target");Directory.CreateDirectory(target);File.WriteAllBytes(Path.Combine(target,"input.synthetic"),Fixture(false));var link=Path.Combine(t.folder,"junction");var start=new ProcessStartInfo("cmd.exe"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in new[]{"/c","mklink","/J",link,target})start.ArgumentList.Add(arg);using var p=Process.Start(start)!;p.WaitForExit();Assert(p.ExitCode==0);try{Reject(()=>new ExperimentalExportSession().Select(Path.Combine(link,"input.synthetic")));Failure(()=>t.session.Export([],Path.Combine(link,"out.copy"),t.folder));Failure(()=>t.session.Export([],Path.Combine(t.folder,"out.copy"),link));}finally{Directory.Delete(link);}});
        Case("missing output and backup parents fail with no output",()=>{var t=Setup();Failure(()=>t.session.Export([],Path.Combine(t.folder,"missing","out"),t.folder));Failure(()=>t.session.Export([],Path.Combine(t.folder,"out"),Path.Combine(t.folder,"missing")));Assert(Directory.GetDirectories(t.folder).Length==0);});
        Case("unwritable output parent retains completed backup",()=>
        {
            if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
            var t=Setup();var blocked=Path.Combine(t.folder,"blocked");Directory.CreateDirectory(blocked);var directory=new DirectoryInfo(blocked);var originalAcl=directory.GetAccessControl();var deny=directory.GetAccessControl();
            deny.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,FileSystemRights.CreateFiles,AccessControlType.Deny));directory.SetAccessControl(deny);
            try{var e=Failure(()=>t.session.Export([],Path.Combine(blocked,"out.copy"),t.folder));Assert(e.CompletedBackup is not null&&File.Exists(e.CompletedBackup.Path)&&!File.Exists(Path.Combine(blocked,"out.copy")));}finally{directory.SetAccessControl(originalAcl);}
        });
        foreach(var point in new[]{CopyPoint.BeforeCreate,CopyPoint.AfterCreate,CopyPoint.DuringWrite,CopyPoint.BeforeFlush})Case($"output fault {point} retains backup removes owned partial and allows retry",()=>
        {
            bool inject=true,hit=false;var t=Setup(probe:(part,p,path,stream)=>{if(inject&&part==ExportPart.Output&&p==point){hit=true;if(p==CopyPoint.DuringWrite)Assert(stream!.Length>0);throw new IOException("Injected output "+point);}});
            var output=Path.Combine(t.folder,"fault.copy");string before=FileHash(t.input);var requests=Requests(t.session.Selected!);var e=Failure(()=>t.session.Export(requests,output,t.folder));Assert(hit&&t.session.State==ExperimentalExportState.Failed&&e.FailureStage==ExperimentalExportState.WritingOutput&&e.CompletedBackup is not null&&e.IncompletePath is null&&!File.Exists(output));Assert(FileHash(e.CompletedBackup!.Path)==before&&FileHash(t.input)==before&&e.InnerException!.Message.Contains("Injected"));
            inject=false;var r=t.session.Export(requests,output,t.folder);Assert(r.BackupPath!=e.CompletedBackup.Path&&File.Exists(e.CompletedBackup.Path)&&t.session.State==ExperimentalExportState.ExportCompleted);VerifySuccess(t.input,before,r,requests,t.session.Selected!);
        },"injected IO exception in actual exclusive-create/write/flush workflow");
        Case("real readback verification detects corrupted output and preserves backup",()=>{var t=Setup(probe:(part,point,path,stream)=>{if(part==ExportPart.Output&&point==CopyPoint.BeforeVerify){stream!.Position=0;stream.WriteByte(0);}});var output=Path.Combine(t.folder,"corrupt.copy");var e=Failure(()=>t.session.Export(Requests(t.session.Selected!),output,t.folder));Assert(e.CompletedBackup is not null&&File.Exists(e.CompletedBackup.Path)&&!File.Exists(output)&&e.InnerException!.Message.Contains("verification"));},"internal hook corrupts actual file before real digest comparison");
        Case("late exclusive-create collision never deletes pre-existing output",()=>{var t=Setup(probe:(part,point,path,stream)=>{if(part==ExportPart.Output&&point==CopyPoint.BeforeCreate)File.WriteAllBytes(path,[7,8,9]);});var output=Path.Combine(t.folder,"collision.copy");var e=Failure(()=>t.session.Export([],output,t.folder));Assert(e.CompletedBackup is not null&&File.ReadAllBytes(output).SequenceEqual(new byte[]{7,8,9})&&e.IncompletePath is null);},"internal timing hook creates real pre-existing collision");
        Case("cleanup failure reports exact incomplete path and both errors",()=>
        {
            bool inject=true;var t=Setup(probe:(part,point,path,stream)=>{if(inject&&part==ExportPart.Output){if(point==CopyPoint.DuringWrite)throw new IOException("Injected primary write failure");if(point==CopyPoint.BeforeCleanup)throw new IOException("Injected cleanup failure");}});
            var output=Path.Combine(t.folder,"incomplete.copy");var e=Failure(()=>t.session.Export(Requests(t.session.Selected!),output,t.folder));Assert(e.IncompletePath==output&&File.Exists(output)&&e.CompletedBackup is not null&&File.Exists(e.CompletedBackup.Path));Assert(e.InnerException!.InnerException is AggregateException aggregate&&aggregate.InnerExceptions.Count==2&&aggregate.InnerExceptions[0].Message.Contains("primary")&&aggregate.InnerExceptions[1].Message.Contains("cleanup"));var partialHash=FileHash(output);inject=false;var retry=Failure(()=>t.session.Export([],output,t.folder));Assert(FileHash(output)==partialHash&&retry.CompletedBackup is null);var restored=t.session.RestoreOriginal(e.CompletedBackup!,Path.Combine(t.folder,"from-failure.copy"));Assert(FileHash(restored)==e.CompletedBackup!.OriginalDigest);
        },"injected primary and cleanup exceptions in actual owned-file cleanup workflow");
        foreach(var point in new[]{CopyPoint.BeforeCreate,CopyPoint.DuringWrite,CopyPoint.BeforeFlush,CopyPoint.BeforeVerify})Case($"backup fault {point} prevents output",()=>
        {
            bool outputStarted=false;string? backupPath=null;var t=Setup(probe:(part,p,path,stream)=>{if(part==ExportPart.Output)outputStarted=true;if(part==ExportPart.Backup){backupPath=path;if(p==point)throw new IOException("Injected backup failure");}});
            var output=Path.Combine(t.folder,"never.copy");var e=Failure(()=>t.session.Export([],output,t.folder));Assert(e.FailureStage==ExperimentalExportState.BackingUp&&e.CompletedBackup is null&&!outputStarted&&!File.Exists(output)&&backupPath is not null&&!File.Exists(backupPath));
        },"injected backup failure in actual file workflow");
        Case("backup cleanup failure reports incomplete backup never output",()=>{var t=Setup(probe:(part,point,path,stream)=>{if(part==ExportPart.Backup&&point is CopyPoint.DuringWrite or CopyPoint.BeforeCleanup)throw new IOException("Injected backup partial/cleanup");});var output=Path.Combine(t.folder,"never.copy");var e=Failure(()=>t.session.Export([],output,t.folder));Assert(e.CompletedBackup is null&&e.IncompletePath is not null&&File.Exists(e.IncompletePath)&&!File.Exists(output));},"injected backup write and cleanup failures");
        Case("repeated exports never overwrite old backup or output",()=>{var t=Setup();var first=t.session.Export(Requests(t.session.Selected!),Path.Combine(t.folder,"one.copy"),t.folder);var hash=FileHash(first.OutputPath);var second=t.session.Export([],Path.Combine(t.folder,"two.copy"),t.folder);Assert(first.BackupPath!=second.BackupPath&&FileHash(first.OutputPath)==hash&&File.Exists(first.BackupPath)&&File.Exists(second.BackupPath));Failure(()=>t.session.Export([],first.OutputPath,t.folder));Assert(FileHash(first.OutputPath)==hash);});
        Case("restore rejects source backup existing targets and modified backup digest",()=>{var t=Setup();var r=t.session.Export(Requests(t.session.Selected!),Path.Combine(t.folder,"modified.copy"),t.folder);foreach(string path in new[]{t.input,r.BackupPath,r.OutputPath})Failure(()=>t.session.RestoreOriginal(r.Backup,path));File.WriteAllBytes(r.BackupPath,File.ReadAllBytes(r.OutputPath));var destination=Path.Combine(t.folder,"not-original.copy");var error=Failure(()=>t.session.RestoreOriginal(r.Backup,destination));Assert(!File.Exists(destination)&&error.BackupValidation==BackupValidationState.Mismatch&&t.session.State==ExperimentalExportState.Failed&&ReferenceEquals(error.CompletedBackup,r.Backup)&&!error.Message.Contains("backup retained")&&error.InnerException!.Message.Contains("Current backup digest validation failed"));});
        Case("semantic editing and game status remain restricted",()=>{var t=Setup();Reject(()=>t.session.Selected!.RequestEdits(new Dictionary<string,string>{{"currency","1"}}));Assert(!Profiles.Observed.CanWrite&&t.session.Selected!.GameSemantics=="UNVERIFIED"&&t.session.Selected.GameIdentity=="UNVERIFIED");});
        Case("retained R1-R3 cases run with R4-specific reports",()=>Assert(R3Checks.Run(true)==0),"retained synthetic suites");
        Case("all historical reports unchanged",()=>{foreach(var h in history)Assert(FileHash(h.Key)==h.Value,h.Key+" changed");},"SHA-256 comparison");
        Case("baseline original source hash unchanged",()=>Assert(FileHash(baseline.input)==baselineBefore),"SHA-256 comparison");
        var sourceHashes=Directory.GetFiles(Directory.GetCurrentDirectory(),"*.cs").Concat(Directory.GetFiles(Directory.GetCurrentDirectory(),"*.csproj")).Order().Select(p=>new{file=Path.GetFileName(p),sha256=FileHash(p)}).ToArray();
        File.WriteAllText("test-results-r4.json",JsonSerializer.Serialize(new{executedAtUtc=DateTimeOffset.UtcNow,scope="Synthetic experimental modified-copy export; game acceptance UNVERIFIED",runtime=Environment.Version.ToString(),cases=results,failed,historicalReportHashes=history,inputHashes=new{before=baselineBefore,after=FileHash(baseline.input)},sourceHashes},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"R4 executed {results.Count} cases; failures={failed}. See test-results-r4.json.");return failed==0?0:1;
    }
    private sealed class ReentrantRequests(IReadOnlyList<PrimitiveReplacement> inner,bool viaCount,Action callback) : IReadOnlyList<PrimitiveReplacement>
    {
        public int Count {get{if(viaCount)callback();return inner.Count;}}
        public PrimitiveReplacement this[int index] {get{if(!viaCount)callback();return inner[index];}}
        public IEnumerator<PrimitiveReplacement> GetEnumerator()=>inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CreateHardLink(string name,string existing,IntPtr reserved);
}

