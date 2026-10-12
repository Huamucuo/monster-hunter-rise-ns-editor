using System.Buffers.Binary;
using System.Globalization;
using System.Text;
namespace FreshFoundation;

public enum SlotStatus { Recognized, Empty, Unsupported, Error }
public sealed record SlotCandidate(int? SlotNumber,string Label,string Path,SlotStatus Status,string Detail);
public static class SlotDiscovery
{
    public static IReadOnlyList<SlotCandidate> Discover(string directory)
    {
        if(!Directory.Exists(directory))throw new DirectoryNotFoundException(directory);
        return new (int? number,string name,string label)[]{(1,"data001Slot.bin","Slot 1"),(2,"data002Slot.bin","Slot 2"),(3,"data003Slot.bin","Slot 3"),(null,"data00-1.bin","System")}
            .Select(p=>Inspect(Path.Combine(directory,p.name),p.number,p.label)).ToArray();
    }
    public static SlotCandidate Inspect(string path,int? slotNumber=null,string label="Selected file")
    {
        var full=Path.GetFullPath(path);
        if(!File.Exists(full))return new(slotNumber,label,full,SlotStatus.Empty,"File not present; character occupancy unknown.");
        try
        {
            if(new FileInfo(full).Length==0)return new(slotNumber,label,full,SlotStatus.Empty,"Zero-byte file; character occupancy unknown.");
            var s=new ExperimentalExportSession();s.Select(full);
            return new(slotNumber,label,full,SlotStatus.Recognized,$"Observed DSSS profile; {s.Selected!.Structure.Summary.Records} roots. Game identity unverified.");
        }
        catch(InvalidDataException e){return new(slotNumber,label,full,SlotStatus.Unsupported,e.Message);}
        catch(NotSupportedException e){return new(slotNumber,label,full,SlotStatus.Unsupported,e.Message);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){return new(slotNumber,label,full,SlotStatus.Error,e.Message);}
    }
}
public sealed record BrowserRow(int EventIndex,int ParentIndex,string OpaqueIdentifier,int Tag,uint? Width,int Coordinate,string RawHex,string RawInterpretation,string Text,bool CanEdit,int ChildCount);
public sealed record BrowserPage(IReadOnlyList<BrowserRow> Rows,int Total,int PageNumber,int PageSize);
public sealed record StagedChange(string FileLabel,string Capability,int EventIndex,string Identifier,byte[] OriginalBytes,byte[] CurrentBytes);
public sealed class EditorWorkspace
{
    private ExperimentalExportSession session=new();
    private byte[] payload=[];
    private int[] parents=[],firstChildren=[],nextSiblings=[],childCounts=[];
    private int firstRoot=-1,rootCount;
    private readonly Dictionary<int,byte[]> staged=[];
    private readonly Stack<Dictionary<int,byte[]?>> undo=[],redo=[];
    private readonly object browserGate=new();
    private string? searchKey;
    private int[] searchMatches=[];
    public Document? Document=>session.Selected;
    public string? SourcePath=>session.SourcePath;
    public int StagedCount=>staged.Count;
    public bool CanUndo=>undo.Count>0;
    public bool CanRedo=>redo.Count>0;
    internal int UndoDepth=>undo.Count;
    public ExperimentalExportReceipt? LastExport {get;private set;}
    public void Open(string path)
    {
        session=new();payload=[];parents=[];firstChildren=[];nextSiblings=[];childCounts=[];staged.Clear();undo.Clear();redo.Clear();searchKey=null;searchMatches=[];LastExport=null;firstRoot=-1;rootCount=0;
        session.Select(path);payload=Document!.Structure.CopyPayload();
        var events=Document.Structure.Events;int count=events.Count;
        parents=Enumerable.Repeat(-1,count).ToArray();firstChildren=Enumerable.Repeat(-1,count).ToArray();nextSiblings=Enumerable.Repeat(-1,count).ToArray();childCounts=new int[count];
        var lastChildren=Enumerable.Repeat(-1,count).ToArray();var levels=new int[129];int lastRoot=-1;
        for(int i=0;i<count;i++)
        {
            int depth=events[i].Depth;int parent=depth==0?-1:levels[depth-1];parents[i]=parent;levels[depth]=i;
            if(parent<0){if(lastRoot<0)firstRoot=i;else nextSiblings[lastRoot]=i;lastRoot=i;rootCount++;}
            else{if(firstChildren[parent]<0)firstChildren[parent]=i;else nextSiblings[lastChildren[parent]]=i;lastChildren[parent]=i;childCounts[parent]++;}
        }
    }
    public IReadOnlyList<int> ChildIndices(int parent=-1) {if(Document is null)throw new InvalidOperationException("Open a file first.");if(parent>=0)CheckIndex(parent);var result=new List<int>();for(int i=parent<0?firstRoot:firstChildren[parent];i>=0;i=nextSiblings[i])result.Add(i);return result;}
    public int ParentOf(int index){CheckIndex(index);return parents[index];}
    public IReadOnlyList<int> AncestorsOf(int index){CheckIndex(index);var path=new List<int>();for(int i=parents[index];i>=0;i=parents[i])path.Add(i);path.Reverse();return path;}
    public BrowserRow Row(int index)
    {
        CheckIndex(index);var e=Document!.Structure.Events[index];var raw=PrimitiveBytes(index);string hex=raw is null?"":Convert.ToHexString(raw.AsSpan(0,Math.Min(raw.Length,32)))+(raw.Length>32?" …":"");
        return new(index,parents[index],e.Identifier is uint id?$"0x{id:X8}":"(element)",e.Tag,e.Width,e.DataCoordinate??e.Coordinate,hex,raw is null?"":Interpret(raw),TextAt(index).Replace("\0","␀"),e.DataCoordinate!=null,childCounts[index]);
    }
    public BrowserPage Browse(int parent=-1,string query="",int page=0,int pageSize=200,CancellationToken cancellationToken=default){lock(browserGate){cancellationToken.ThrowIfCancellationRequested();return BrowseCore(parent,query,page,pageSize,cancellationToken);}}
    private BrowserPage BrowseCore(int parent,string query,int page,int pageSize,CancellationToken cancellationToken)
    {
        if(Document is null)throw new InvalidOperationException("Open a file first.");if(parent>=0)CheckIndex(parent);if(parent< -1||page<0||pageSize is <1 or >500)throw new ArgumentOutOfRangeException();
        string filter=query.Trim();var indices=new List<int>(pageSize);int offset=checked(page*pageSize),total;
        if(filter.Length>0)
        {
            if(searchKey!=filter)
            {
                var found=new List<int>();string idFilter=filter.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?filter[2..]:filter;
                for(int i=0;i<Document.Structure.Events.Count;i++)
                {
                    if((i&4095)==0)cancellationToken.ThrowIfCancellationRequested();var e=Document.Structure.Events[i];
                    if((e.Identifier is uint id && id.ToString("X8",CultureInfo.InvariantCulture).Contains(idFilter,StringComparison.OrdinalIgnoreCase)) || (e.ClassIdentifier is uint classId&&classId.ToString("X8",CultureInfo.InvariantCulture).Contains(idFilter,StringComparison.OrdinalIgnoreCase)) || TextAt(i).Replace("\0","␀").Contains(filter,StringComparison.OrdinalIgnoreCase))found.Add(i);
                }
                cancellationToken.ThrowIfCancellationRequested();searchMatches=found.ToArray();searchKey=filter;
            }
            total=searchMatches.Length;indices.AddRange(searchMatches.Skip(offset).Take(pageSize));
        }
        else
        {
            total=parent<0?rootCount:childCounts[parent];int skip=0;
            for(int i=parent<0?firstRoot:firstChildren[parent];i>=0&&indices.Count<pageSize;i=nextSiblings[i]){if(skip++>=offset)indices.Add(i);}
        }
        return new(indices.Select(Row).ToArray(),total,page,pageSize);
    }
    public byte[]? PrimitiveBytes(int index,bool original=false)
    {
        CheckIndex(index);var e=Document!.Structure.Events[index];if(e.DataCoordinate is not int coordinate||e.Width is not uint width)return null;
        if(!original&&staged.TryGetValue(index,out var pending))return (byte[])pending.Clone();
        return payload.AsSpan(coordinate-12,(int)width).ToArray();
    }
    internal int ReadSigned32(int index)=>BinaryPrimitives.ReadInt32LittleEndian(PrimitiveBytes(index)!);
    public string TextAt(int index)
    {
        CheckIndex(index);var e=Document!.Structure.Events[index];if(e.Tag!=15||e.Count is not uint units)return "";
        int start=((e.Coordinate+3)&~3)-12+4;return Encoding.Unicode.GetString(payload,start,checked((int)units*2));
    }
    public void Apply(int eventIndex,byte[] replacement)=>Apply(new Dictionary<int,byte[]>{{eventIndex,replacement}});
    public void Apply(IReadOnlyDictionary<int,byte[]> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);var prepared=new Dictionary<int,byte[]>();
        foreach(var p in replacements){var original=PrimitiveBytes(p.Key,true)??throw new ArgumentException("Only primitive data can be staged.");if(p.Value is null||p.Value.Length!=original.Length)throw new ArgumentException("Exact primitive width required.");if(!p.Value.SequenceEqual(PrimitiveBytes(p.Key)!))prepared.Add(p.Key,(byte[])p.Value.Clone());}
        var previous=new Dictionary<int,byte[]?>();
        foreach(var p in prepared){previous[p.Key]=staged.TryGetValue(p.Key,out var old)?(byte[])old.Clone():null;if(p.Value.SequenceEqual(PrimitiveBytes(p.Key,true)!))staged.Remove(p.Key);else staged[p.Key]=p.Value;}
        if(previous.Count>0){undo.Push(previous);redo.Clear();}
    }
    private bool Replay(Stack<Dictionary<int,byte[]?>> from,Stack<Dictionary<int,byte[]?>> to){if(!from.TryPop(out var target))return false;var inverse=new Dictionary<int,byte[]?>();foreach(var p in target){inverse[p.Key]=staged.TryGetValue(p.Key,out var current)?(byte[])current.Clone():null;if(p.Value is null)staged.Remove(p.Key);else staged[p.Key]=(byte[])p.Value.Clone();}to.Push(inverse);return true;}
    internal sealed record HistoryCheckpoint(Dictionary<int,byte[]> Staged,Dictionary<int,byte[]?>[] Undo,Dictionary<int,byte[]?>[] Redo);
    private static Dictionary<int,byte[]?> CopyHistory(Dictionary<int,byte[]?> value)=>value.ToDictionary(p=>p.Key,p=>p.Value is null?null:(byte[])p.Value.Clone());
    internal HistoryCheckpoint CaptureHistory()=>new(staged.ToDictionary(p=>p.Key,p=>(byte[])p.Value.Clone()),undo.Select(CopyHistory).ToArray(),redo.Select(CopyHistory).ToArray());
    internal void RestoreHistory(HistoryCheckpoint state){staged.Clear();foreach(var p in state.Staged)staged[p.Key]=(byte[])p.Value.Clone();undo.Clear();redo.Clear();foreach(var row in state.Undo.Reverse())undo.Push(CopyHistory(row));foreach(var row in state.Redo.Reverse())redo.Push(CopyHistory(row));}
    public bool Undo()=>Replay(undo,redo);
    public bool Redo()=>Replay(redo,undo);
    public void Clear(){if(staged.Count==0)return;undo.Push(staged.ToDictionary(p=>p.Key,p=>(byte[]?)(byte[])p.Value.Clone()));staged.Clear();redo.Clear();}
    public IReadOnlyList<StagedChange> ReviewChanges(string fileLabel="Selected file")=>staged.OrderBy(p=>p.Key).Select(p=>new StagedChange(fileLabel,"Raw / opaque (read-only)",p.Key,$"0x{Document!.Structure.Events[p.Key].Identifier:X8}",PrimitiveBytes(p.Key,true)!,PrimitiveBytes(p.Key)!)).ToArray();
    public ExperimentalExportReceipt Export(string newOutputPath,string backupParent)
    {
        var requests=staged.OrderBy(p=>p.Key).Select(p=>new PrimitiveReplacement(p.Key,PrimitiveBytes(p.Key,true)!,p.Value)).ToArray();
        return LastExport=session.Export(requests,newOutputPath,backupParent);
    }
    public string RestoreOriginal(CompletedOriginalBackup backup,string newDestination)=>session.RestoreOriginal(backup,newDestination);
    private void CheckIndex(int index){if(Document is null||index<0||index>=Document.Structure.Events.Count)throw new ArgumentOutOfRangeException(nameof(index));}
    private static string Interpret(byte[] raw)
    {
        var c=CultureInfo.InvariantCulture;
        return raw.Length switch
        {
            1=>$"raw u8={raw[0]} / i8={unchecked((sbyte)raw[0])}",
            2=>$"raw u16={BinaryPrimitives.ReadUInt16LittleEndian(raw)} / i16={BinaryPrimitives.ReadInt16LittleEndian(raw)}",
            4=>$"raw u32={BinaryPrimitives.ReadUInt32LittleEndian(raw)} / i32={BinaryPrimitives.ReadInt32LittleEndian(raw)} / f32={BitConverter.ToSingle(raw).ToString("G6",c)}",
            8=>$"raw u64={BinaryPrimitives.ReadUInt64LittleEndian(raw)} / i64={BinaryPrimitives.ReadInt64LittleEndian(raw)} / f64={BitConverter.ToDouble(raw).ToString("G6",c)}",
            _=>"opaque primitive bytes; no numeric meaning assigned"
        };
    }
}
