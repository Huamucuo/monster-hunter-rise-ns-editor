namespace FreshFoundation;
internal enum CopyPoint { BeforeCreate, AfterCreate, DuringWrite, BeforeFlush, BeforeVerify, BeforeCleanup }
internal sealed class IncompleteCopyException : IOException
{
    internal string IncompletePath {get;}
    internal IncompleteCopyException(string path,Exception original,Exception cleanup)
        :base("Incomplete copy cleanup failed: "+path,new AggregateException(original,cleanup)){IncompletePath=path;}
}
internal enum ExportPart { Backup, Output, Restore }
public enum ExperimentalExportState { NoSelection, Inspecting, Selected, Preparing, BackingUp, BackupCompleted, WritingOutput, ExportCompleted, Restoring, RestoreCompleted, Failed }
public enum BackupValidationState { NotChecked, Matched, Mismatch }
public sealed class CompletedOriginalBackup
{
    public string Path {get;}
    public string OriginalDigest {get;}
    public string CreationVerification=>"VERIFIED_AT_CREATION";
    internal string SourcePath {get;}
    internal CompletedOriginalBackup(string path,string digest,string sourcePath){Path=path;OriginalDigest=digest;SourcePath=sourcePath;}
}
public sealed record ExperimentalExportReceipt(string OriginalDigest,string ModifiedDigest,CompletedOriginalBackup Backup,string OutputPath)
{
    public string Status=>"EXPERIMENTAL";
    public string GameAcceptance=>"UNVERIFIED";
    public string BackupPath=>Backup.Path;
}
public sealed class ExperimentalExportException : IOException
{
    public ExperimentalExportState FailureStage {get;}
    public CompletedOriginalBackup? CompletedBackup {get;}
    public string? IncompletePath {get;}
    public BackupValidationState BackupValidation {get;}
    internal ExperimentalExportException(ExperimentalExportState stage,CompletedOriginalBackup? backup,Exception error,BackupValidationState validation=BackupValidationState.NotChecked)
        :base("Experimental operation failed at "+stage+(backup is null?"; no completed original backup.":"; verified-at-creation backup record: "+backup.Path+"; current backup validation: "+validation)+(error is IncompleteCopyException c?"; incomplete file retained: "+c.IncompletePath:""),error)
    {FailureStage=stage;CompletedBackup=backup;BackupValidation=validation;IncompletePath=(error as IncompleteCopyException)?.IncompletePath;}
}
public sealed class ExperimentalExportSession
{
    public ExperimentalExportState State {get;private set;}=ExperimentalExportState.NoSelection;
    public Document? Selected {get;private set;}
    public string? SourcePath {get;private set;}
    private FileIdentity identity;
    private readonly object gate=new();
    private bool running;
    private T Execute<T>(Func<T> action) {lock(gate){if(running)throw new InvalidOperationException("Reentrant session operation rejected.");running=true;try{return action();}finally{running=false;}}}
    private readonly Action<ExportPart,CopyPoint,string,FileStream?>? probe;
    public ExperimentalExportSession() {}
    // Internal deterministic fault seam exercises the same real file workflow.
    internal ExperimentalExportSession(Action<ExportPart,CopyPoint,string,FileStream?> probe){this.probe=probe;}
    public void Select(string sourcePath) => Execute(()=>{SelectCore(sourcePath);return 0;});
    private void SelectCore(string sourcePath)
    {
        Selected=null;SourcePath=null;State=ExperimentalExportState.Inspecting;
        try
        {
            var path=SafeFiles.Normal(sourcePath);using var parent=SafeFiles.PinParents(path);using var file=SafeFiles.OpenExisting(path);
            var candidateIdentity=SafeFiles.Identity(file);var snapshot=SafeFiles.ReadBounded(file);var document=Document.Inspect(snapshot);
            identity=candidateIdentity;SourcePath=path;Selected=document;State=ExperimentalExportState.Selected;
        }
        catch{State=ExperimentalExportState.Failed;throw;}
    }
    public ExperimentalExportReceipt Export(IReadOnlyList<PrimitiveReplacement> requests,string newOutputPath,string backupParent) => Execute(()=>ExportCore(requests,newOutputPath,backupParent));
    private ExperimentalExportReceipt ExportCore(IReadOnlyList<PrimitiveReplacement> requests,string newOutputPath,string backupParent)
    {
        CompletedOriginalBackup? backup=null;
        try
        {
            State=ExperimentalExportState.Preparing;
            if(Selected is null || SourcePath is null)throw new InvalidOperationException("Explicit selection required.");
            // Validate all requests and produce an immutable in-memory result first.
            var result=ExperimentalStructuralSerializer.Replace(Selected,requests);var modified=result.CopyFileBytes();
            using var sourceParent=SafeFiles.PinParents(SourcePath);
            using var outputParent=SafeFiles.PinParents(newOutputPath);
            using var backupParentLease=SafeFiles.PinParents(System.IO.Path.Combine(backupParent,"reserved"));
            var output=SafeFiles.NewTarget(newOutputPath,SourcePath);
            SafeFiles.CheckPath(backupParent);
            using var originalFile=SafeFiles.OpenExisting(SourcePath);
            var originalBytes=SafeFiles.ReadBounded(originalFile);
            if(SafeFiles.Identity(originalFile)!=identity || SafeFiles.Digest(originalBytes)!=Selected.Digest)throw new IOException("Selected source identity or digest changed.");
            State=ExperimentalExportState.BackingUp;
            var privateDirectory=SafeFiles.CreatePrivateDirectory(backupParent);
            var backupPath=System.IO.Path.Combine(privateDirectory,"original.copy");
            if(string.Equals(backupPath,output,StringComparison.OrdinalIgnoreCase))throw new IOException("Backup and output must differ.");
            SafeFiles.CreateCopy(backupPath,originalBytes,(point,path,stream)=>probe?.Invoke(ExportPart.Backup,point,path,stream));
            backup=new(backupPath,Selected.Digest,SourcePath);State=ExperimentalExportState.BackupCompleted;
            State=ExperimentalExportState.WritingOutput;
            SafeFiles.CreateCopy(output,modified,(point,path,stream)=>probe?.Invoke(ExportPart.Output,point,path,stream));
            State=ExperimentalExportState.ExportCompleted;
            return new(Selected.Digest,result.InspectedDocument.Digest,backup,output);
        }
        catch(Exception error)
        {
            var stage=State;State=ExperimentalExportState.Failed;
            throw new ExperimentalExportException(stage,backup,error);
        }
    }
    public string RestoreOriginal(CompletedOriginalBackup backup,string newDestination) => Execute(()=>RestoreCore(backup,newDestination));
    private string RestoreCore(CompletedOriginalBackup backup,string newDestination)
    {
        var validation=BackupValidationState.NotChecked;
        try
        {
            State=ExperimentalExportState.Restoring;ArgumentNullException.ThrowIfNull(backup);
            using var backupParent=SafeFiles.PinParents(backup.Path);using var destinationParent=SafeFiles.PinParents(newDestination);
            var destination=SafeFiles.NewTarget(newDestination,backup.SourcePath);
            if(SourcePath is not null && string.Equals(destination,SourcePath,StringComparison.OrdinalIgnoreCase))throw new IOException("Cannot restore over selected source.");
            if(string.Equals(destination,SafeFiles.Normal(backup.Path),StringComparison.OrdinalIgnoreCase))throw new IOException("Cannot restore over backup.");
            using var file=SafeFiles.OpenExisting(backup.Path);var bytes=SafeFiles.ReadBounded(file);
            if(SafeFiles.Digest(bytes)!=backup.OriginalDigest){validation=BackupValidationState.Mismatch;throw new InvalidDataException("Current backup digest validation failed; do not restore as original.");}
            validation=BackupValidationState.Matched;
            SafeFiles.CreateCopy(destination,bytes,(point,path,stream)=>probe?.Invoke(ExportPart.Restore,point,path,stream));
            State=ExperimentalExportState.RestoreCompleted;return destination;
        }
        catch(Exception error)
        {
            var stage=State;State=ExperimentalExportState.Failed;throw new ExperimentalExportException(stage,backup,error,validation);
        }
    }
}


