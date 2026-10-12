using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
namespace FreshFoundation;
public enum Stage { NoSelection, Inspecting, SelectedReadOnly, ExportSucceeded, Failed }
public sealed record CopyReceipt(string BackupPath, string Digest, string OutputPath);
public sealed class CopySession
{
    public Stage State {get; private set;} = Stage.NoSelection;
    public Document? Selected {get; private set;}
    private string? inputPath;
    private FileIdentity inputIdentity;
    public void Select(string path)
    {
        Selected = null; inputPath = null; State = Stage.Inspecting;
        try { var full = SafeFiles.Normal(path); using var lease = SafeFiles.PinParents(full); using var stream = SafeFiles.OpenExisting(full); var identity = SafeFiles.Identity(stream); var bytes = SafeFiles.ReadBounded(stream); var document = Document.Inspect(bytes); inputPath = full; inputIdentity = identity; Selected = document; State = Stage.SelectedReadOnly; }
        catch { State = Stage.Failed; throw; }
    }
    public CopyReceipt Export(string outputPath, string backupParent)
    {
        try
        {
            if(Selected is null || inputPath is null) throw new InvalidOperationException("Explicit file selection required.");
            using var sourceLease = SafeFiles.PinParents(inputPath);
            using var outputLease = SafeFiles.PinParents(outputPath);
            using var backupLease = SafeFiles.PinParents(Path.Combine(backupParent,"reserved"));
            var output = SafeFiles.NewTarget(outputPath, inputPath);
            SafeFiles.CheckPath(backupParent);
            using var input = SafeFiles.OpenExisting(inputPath);
            var bytes = SafeFiles.ReadBounded(input);
            if(SafeFiles.Identity(input) != inputIdentity || SafeFiles.Digest(bytes) != Selected.Digest) throw new IOException("Selected source changed.");
            var backupDirectory = SafeFiles.CreatePrivateDirectory(backupParent);
            var backup = Path.Combine(backupDirectory, "original.copy");
            SafeFiles.CreateCopy(backup, bytes);
            // A verified completed backup is deliberately retained if the later output fails.
            SafeFiles.CreateCopy(output, bytes);
            State = Stage.ExportSucceeded;
            return new(backup, Selected.Digest, output);
        }
        catch { State = Stage.Failed; throw; }
    }
    public string Restore(string backupPath, string expectedDigest, string destination)
    {
        try
        {
            if(inputPath is null || Selected is null) throw new InvalidOperationException("Explicit selection required.");
            using var backupLease = SafeFiles.PinParents(backupPath);
            using var outputLease = SafeFiles.PinParents(destination);
            var output = SafeFiles.NewTarget(destination, inputPath);
            if(string.Equals(output, SafeFiles.Normal(backupPath), StringComparison.OrdinalIgnoreCase)) throw new IOException("Backup cannot be its own destination.");
            using var backup = SafeFiles.OpenExisting(SafeFiles.Normal(backupPath));
            var bytes = SafeFiles.ReadBounded(backup);
            if(!string.Equals(SafeFiles.Digest(bytes), expectedDigest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Backup digest mismatch.");
            SafeFiles.CreateCopy(output, bytes);
            return output;
        }
        catch { State = Stage.Failed; throw; }
    }
}
internal readonly record struct FileIdentity(uint Volume, uint High, uint Low);
internal static class SafeFiles
{
    internal static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static string Normal(string path)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("File copy safety currently supports Windows only.");
        var full = Path.GetFullPath(path);
        if(full.StartsWith(@"\\", StringComparison.Ordinal) || full[2..].Contains(':')) throw new IOException("Network, device and alternate stream paths are unsupported.");
        foreach(var part in full[3..].Split(Path.DirectorySeparatorChar))
        {
            if(part.EndsWith(' ') || part.EndsWith('.')) throw new IOException("Ambiguous Windows path component.");
            var name=part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if(name is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" || (name.Length==4 && (name.StartsWith("COM") || name.StartsWith("LPT")) && "123456789¹²³".Contains(name[3])))
                throw new IOException("Reserved device paths are unsupported.");
        }
        return full;
    }
    internal static void CheckPath(string path)
    {
        var current = Normal(path);
        while(!string.IsNullOrEmpty(current))
        {
            if(File.Exists(current) || Directory.Exists(current))
                if((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked paths are unsupported.");
            current = Path.GetDirectoryName(current)!;
        }
    }
    internal static string NewTarget(string path, string forbidden)
    {
        var full = Normal(path); CheckPath(full);
        if(string.Equals(full, forbidden, StringComparison.OrdinalIgnoreCase) || File.Exists(full) || Directory.Exists(full)) throw new IOException("Destination must be a new file outside the source.");
        if(!Directory.Exists(Path.GetDirectoryName(full))) throw new DirectoryNotFoundException("Destination parent must exist.");
        return full;
    }
    internal static FileStream OpenExisting(string path)
    {
        CheckPath(path);
        var handle = CreateFile(path, 0x80000000, 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if(handle.IsInvalid) { handle.Dispose(); throw new IOException("Cannot open regular input safely."); }
        var stream = new FileStream(handle, FileAccess.Read);
        try { Identity(stream); CheckPath(path); return stream; } catch { stream.Dispose(); throw; }
    }
    internal static byte[] ReadBounded(FileStream stream)
    {
        if(stream.Length > Document.Limit) throw new InvalidDataException("Source exceeds 64 MiB policy.");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
        if(stream.ReadByte() != -1) throw new IOException("Source length changed.");
        return bytes;
    }
    internal static FileIdentity Identity(FileStream stream)
    {
        if(!GetFileInformationByHandle(stream.SafeFileHandle, out var info)) throw new IOException("Cannot verify file identity.");
        if(info.Links != 1 || (info.Attributes & (uint)FileAttributes.ReparsePoint) != 0) throw new IOException("Hard links and reparse files are unsupported.");
        return new(info.Volume, info.IndexHigh, info.IndexLow);
    }
    internal static string CreatePrivateDirectory(string parent)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        CheckPath(parent);
        if(!Directory.Exists(parent)) throw new DirectoryNotFoundException();
        var path = Path.Combine(Normal(parent), "backup-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).Create(acl); CheckPath(path);
        return path;
    }
    internal static void CreateCopy(string path, byte[] bytes, Action<CopyPoint,string,FileStream?>? probe=null)
    {
        using var lease = PinParents(path);
        CheckPath(path);
        bool created = false;
        try
        {
            probe?.Invoke(CopyPoint.BeforeCreate,path,null);
            using(var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                created = true; Identity(stream);
                probe?.Invoke(CopyPoint.AfterCreate,path,stream);
                int first=bytes.Length/2;stream.Write(bytes.AsSpan(0,first));
                probe?.Invoke(CopyPoint.DuringWrite,path,stream);
                stream.Write(bytes.AsSpan(first));
                probe?.Invoke(CopyPoint.BeforeFlush,path,stream);
                stream.Flush(true);
                probe?.Invoke(CopyPoint.BeforeVerify,path,stream);
                stream.Position = 0;
                if(Digest(ReadBounded(stream)) != Digest(bytes)) throw new IOException("Copy verification failed.");
            }
        }
        catch(Exception failure)
        {
            if(created) try
            {
                probe?.Invoke(CopyPoint.BeforeCleanup,path,null);
                File.Delete(path);
            }
            catch(Exception cleanup) { throw new IncompleteCopyException(path,failure,cleanup); }
            throw;
        }
    }
    // Pin only the immediate parent without delete sharing; check all ancestors.
    // Higher ancestors are not locked; hostile concurrent substitution is not covered.
    internal static IDisposable PinParents(string file)
    {
        var full = Normal(file);
        var parent = Path.GetDirectoryName(full)!;
        var chain = new Stack<string>();
        CheckPath(parent);
        chain.Push(parent);
        var lease = new DirectoryLease();
        try
        {
            while(chain.TryPop(out var path))
            {
                var handle = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
                if(handle.IsInvalid) {handle.Dispose();throw new IOException("Cannot lock destination parent.");}
                lease.Handles.Add(handle);
                if(!GetFileInformationByHandle(handle,out var info) || (info.Attributes & (uint)FileAttributes.ReparsePoint)!=0 || (info.Attributes & (uint)FileAttributes.Directory)==0)
                    throw new IOException("Unsafe path ancestry.");
            }
            CheckPath(parent);
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }
    private sealed class DirectoryLease : IDisposable
    {
        internal List<SafeFileHandle> Handles {get;}=[];
        public void Dispose() {for(int i=Handles.Count-1;i>=0;i--)Handles[i].Dispose();}
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [StructLayout(LayoutKind.Sequential)] private struct HandleInfo { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out HandleInfo info);
}









