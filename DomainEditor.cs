using System.Buffers.Binary;
namespace FreshFoundation;
public sealed record CharacterInfo(string? Name,int? HunterRank,int? MasterRank,int? Money,uint? Points,IReadOnlyDictionary<string,string> Issues);
public sealed record InventoryItem(int Index,uint? Id,int? Quantity,bool Editable,string? Issue);
public sealed record EquipmentItem(int Index,uint? Type,uint? Id,string? Issue);
public sealed record SkillPair(byte Id,uint Level);
public sealed record TalismanInfo(int EquipmentIndex,IReadOnlyList<SkillPair> Pairs,IReadOnlyList<uint> DecorationIds,int AuxiliaryArrayCount);
public sealed partial class DomainEditor
{
    private readonly EditorWorkspace workspace;

    private int money=-1,points=-1,name=-1,hr=-1,mr=-1;
    private readonly List<(int id,int count)> inventory=[];
    private readonly List<(int type,int id,int node)> equipment=[];
    private readonly Dictionary<string,string> issues=[];
    private readonly HashSet<uint> knownIds=[];
    private readonly Dictionary<uint,long> originalTotals=[];
    private bool inventoryBoundsAvailable;
    public int? OriginalMoneyCeiling=>money>=0&&ReadInt(money,true)>=0?ReadInt(money,true):null;
    public uint? OriginalPointsCeiling=>points>=0?ReadUInt(points,true):null;
    public long? OriginalItemTotalCeiling(uint id)=>inventoryBoundsAvailable&&originalTotals.TryGetValue(id,out long total)?total:null;
    private readonly HashSet<SkillPair> knownPairs=[];
    public IReadOnlyList<SkillPair> KnownSkillPairs=>knownPairs.OrderBy(p=>p.Id).ThenBy(p=>p.Level).ToArray();
    public const uint EmptyItemId=0x04000000;
    public IReadOnlyDictionary<string,string> Issues=>new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(issues);
    public IReadOnlyList<uint> KnownItemIds=>knownIds.Order().ToArray();
    public bool CanEditMoney=>OriginalMoneyCeiling is not null;
    public bool CanEditPoints=>points>=0;
    public DomainEditor(EditorWorkspace workspace)
    {
        this.workspace=workspace;if(workspace.Document is null)throw new InvalidOperationException("Open a file first.");

        Try("Name",()=>name=Child(Child(RootWith(3575327903),3575327903,17),45809936,15));
        Try("Hunter rank",()=>hr=Child(RootWith(1448329361),1448329361,7,4));Try("Master rank",()=>mr=Child(RootWith(1448329361),1312982661,7,4));
        Try("Money",()=>money=ResolvePath((1043188015,17,null),(884539280,17,null),(2249897735,7,4)));
        Try("Points",()=>points=ResolvePath((1043188015,17,null),(2442806060,17,null),(2994776752,8,4)));
        Try("Inventory",()=>
        {
            int array=ResolvePath((1043188015,17,null),(4130173922,17,null),(2039932910,-1,null));
            foreach(int record in ChildrenObjects(array))
            {
                try{int item=Child(record,3266549020,17);int id=Child(item,2940755215,1,4),count=Child(item,2349967638,7,4);inventory.Add((id,count));if(ReadInt(count,true)>0)knownIds.Add(ReadUInt(id,true));}
                catch{inventory.Add((-1,-1));}
            }
        });
        Try("Equipment",()=>
        {
            int array=ResolvePath((1043188015,17,null),(2675617066,17,null),(2694263966,-1,null));
            foreach(int record in ChildrenObjects(array))
            {try{equipment.Add((Child(record,2037761500,1,4),Child(record,1854292354,8,4),record));}catch{equipment.Add((-1,-1,record));}}
        });
        inventoryBoundsAvailable=inventory.Count>0&&inventory.All(p=>p.id>=0&&ReadInt(p.count,true)>=0&&!(ReadUInt(p.id,true)==EmptyItemId&&ReadInt(p.count,true)>0));
        if(inventoryBoundsAvailable)foreach(var p in inventory){uint id=ReadUInt(p.id,true);int count=ReadInt(p.count,true);if(count>0)originalTotals[id]=checked(originalTotals.GetValueOrDefault(id)+count);}
        for(int i=0;i<equipment.Count;i++){try{if(equipment[i].type>=0&&ReadUInt(equipment[i].type,true)==3)foreach(var pair in ReadPairs(i,true))knownPairs.Add(pair);}catch{}}
    }
    private (int[] ids,int[] levels) PairFields(int index)
    {
        if(index<0||index>=equipment.Count)throw new ArgumentOutOfRangeException(nameof(index));var p=equipment[index];
        if(p.type<0||ReadUInt(p.type)!=3||ReadUInt(p.type,true)!=3)throw new InvalidOperationException("Only existing type-3 talismans support this operation.");
        int[] ids=PrimitiveArray(p.node,347745453,2,1,1),levels=PrimitiveArray(p.node,3058239487,2,8,4);return(ids,levels);
    }
    private int[] PrimitiveArray(int parent,uint id,int count,int tag,uint width)
    {
        int array=Child(parent,id,-1);var children=workspace.ChildIndices(array).ToArray();
        if(children.Length!=count||children.Any(i=>workspace.Document!.Structure.Events[i].Tag!=tag||workspace.Document.Structure.Events[i].Width!=width))throw new InvalidDataException("Array shape not admitted for this capability.");return children;
    }
    private IReadOnlyList<SkillPair> ReadPairs(int index,bool original=false)
    {
        var fields=PairFields(index);return Enumerable.Range(0,2).Select(i=>new SkillPair(workspace.PrimitiveBytes(fields.ids[i],original)![0],ReadUInt(fields.levels[i],original))).ToArray();
    }
    public IReadOnlyList<uint> DecorationIds(int equipmentIndex)
    {
        if(equipmentIndex<0||equipmentIndex>=equipment.Count)throw new ArgumentOutOfRangeException(nameof(equipmentIndex));return PrimitiveArray(equipment[equipmentIndex].node,4129230907,3,1,4).Select(i=>ReadUInt(i)).ToArray();
    }
    public TalismanInfo Talisman(int equipmentIndex)
    {
        var pairs=ReadPairs(equipmentIndex);int array=Child(equipment[equipmentIndex].node,557620028,-1);var children=workspace.ChildIndices(array);
        if(children.Count is not (4 or 5)||children.Any(i=>workspace.Document!.Structure.Events[i].Tag!=8||workspace.Document.Structure.Events[i].Width!=4))throw new InvalidDataException("Auxiliary array shape not recognized.");
        return new(equipmentIndex,pairs,DecorationIds(equipmentIndex),children.Count);
    }
    public IReadOnlyList<EquipmentFieldSchema> EquipmentSchema(int index)
    {
        if(index<0||index>=equipment.Count)throw new ArgumentOutOfRangeException(nameof(index));var p=equipment[index];if(p.type<0)throw new InvalidDataException("Equipment fields unavailable.");
        var result=new List<EquipmentFieldSchema>{new("Equipment type",2037761500,1,4,1,[ReadUInt(p.type)],"Observed numeric discriminator; game semantics experimental"),new("Equipment ID",1854292354,8,4,1,[ReadUInt(p.id)],"Numeric ID; catalog meaning unknown"),new("Decorations",4129230907,1,4,3,DecorationIds(index),"Observed decoration IDs")};
        if(ReadUInt(p.type)==3)
        {
            var t=Talisman(index);result.Add(new("Skill IDs",347745453,1,1,2,t.Pairs.Select(v=>(uint)v.Id).ToArray(),"Observed coupled skill IDs"));result.Add(new("Skill levels",3058239487,8,4,2,t.Pairs.Select(v=>v.Level).ToArray(),"Observed coupled levels"));
            int[] a=PrimitiveArray(p.node,557620028,t.AuxiliaryArrayCount,8,4);result.Add(new("Auxiliary values",557620028,8,4,a.Length,a.Select(i=>ReadUInt(i)).ToArray(),"UNKNOWN; not established as socket levels"));
        }
        return result;
    }
    public void SetTalismanPair(int equipmentIndex,int position,SkillPair pair)
    {
        if(position is <0 or >1)throw new ArgumentOutOfRangeException(nameof(position));if(!knownPairs.Contains(pair))throw new ArgumentException("Choose an original observed skill-ID/level pair from this selected inventory.");
        var fields=PairFields(equipmentIndex);workspace.Apply(new Dictionary<int,byte[]>{{fields.ids[position],[pair.Id]},{fields.levels[position],UIntBytes(pair.Level)}});
    }
    private void Try(string capability,Action read){try{read();}catch(Exception e){issues[capability]=e.Message;}}
    private int ResolvePath(params (uint id,int tag,uint? width)[] path)
    {
        var matches=new List<int>();foreach(int root in workspace.ChildIndices()){try{int node=root;foreach(var step in path)node=Child(node,step.id,step.tag,step.width);matches.Add(node);}catch(InvalidDataException){}}
        if(matches.Count!=1)throw new InvalidDataException("Complete domain path absent or ambiguous.");return matches[0];
    }
    private int RootWith(uint id)
    {
        var roots=workspace.ChildIndices().Where(r=>workspace.ChildIndices(r).Any(i=>workspace.Document!.Structure.Events[i].Identifier==id)).ToArray();
        if(roots.Length!=1)throw new InvalidDataException("Expected a unique root/direct field relationship.");return roots[0];
    }
    internal int Child(int parent,uint id,int tag,uint? width=null)
    {
        if(parent<0)throw new InvalidDataException("Required parent path is unavailable.");
        var matches=workspace.ChildIndices(parent).Where(i=>workspace.Document!.Structure.Events[i].Identifier==id).ToArray();
        if(matches.Length!=1)throw new InvalidDataException("Field path absent or ambiguous: 0x"+id.ToString("X8"));
        var e=workspace.Document!.Structure.Events[matches[0]];if(e.Tag!=tag||(width is not null&&e.Width!=width))throw new InvalidDataException("Unexpected tag/width at 0x"+id.ToString("X8"));return matches[0];
    }
    private IReadOnlyList<int> ChildrenObjects(int array)
    {
        var children=workspace.ChildIndices(array);if(children.Any(i=>workspace.Document!.Structure.Events[i].Tag!=17))throw new InvalidDataException("Object array shape not recognized.");return children;
    }
    internal int ReadInt(int index,bool original=false)=>BinaryPrimitives.ReadInt32LittleEndian(workspace.PrimitiveBytes(index,original)!);
    internal uint ReadUInt(int index,bool original=false)=>BinaryPrimitives.ReadUInt32LittleEndian(workspace.PrimitiveBytes(index,original)!);
    internal static byte[] IntBytes(int value){var b=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(b,value);return b;}
    internal static byte[] UIntBytes(uint value){var b=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,value);return b;}
    public CharacterInfo Character()=>new(name<0?null:workspace.TextAt(name).TrimEnd('\0'),hr<0?null:ReadInt(hr),mr<0?null:ReadInt(mr),money<0?null:ReadInt(money),points<0?null:ReadUInt(points),Issues);
    public void SetMoney(int value){if(OriginalMoneyCeiling is not int ceiling)throw new InvalidOperationException("Original money is unavailable or anomalous; read-only.");if(value<0||value>ceiling)throw new ArgumentOutOfRangeException(nameof(value),"Conservative range is 0 through original money; this is not a verified game maximum.");workspace.Apply(money,IntBytes(value));}
    public void SetPoints(uint value){if(OriginalPointsCeiling is not uint ceiling)throw new InvalidOperationException("Points path unavailable.");if(value>ceiling)throw new ArgumentOutOfRangeException(nameof(value),"Conservative range is 0 through original points; this is not a verified game maximum.");workspace.Apply(points,UIntBytes(value));}
    public IReadOnlyList<InventoryItem> Inventory(string filter="",bool includeEmpty=true)
    {
        var rows=new List<InventoryItem>();for(int i=0;i<inventory.Count;i++){var p=inventory[i];if(p.id<0){rows.Add(new(i,null,null,false,"Missing or ambiguous item fields."));continue;}uint id=ReadUInt(p.id);int quantity=ReadInt(p.count);if(!includeEmpty&&quantity==0)continue;if(filter.Length>0&&!id.ToString().Contains(filter,StringComparison.OrdinalIgnoreCase)&&!$"0x{id:X8}".Contains(filter,StringComparison.OrdinalIgnoreCase))continue;rows.Add(new(i,id,quantity,quantity>=0,quantity<0?"Negative quantity is outside admitted profile.":null));}return rows;
    }
    private (int id,int count) Item(int index){if(index<0||index>=inventory.Count||inventory[index].id<0)throw new ArgumentOutOfRangeException(nameof(index));return inventory[index];}
    public void SetItemQuantity(int index,int value){if(value==0){DeleteItem(index);return;}if(value<0)throw new ArgumentOutOfRangeException(nameof(value));var p=Item(index);if(ReadInt(p.count)<=0)throw new InvalidOperationException("Use Add to fill an empty record.");CheckItemTotal(ReadUInt(p.id),value-(long)ReadInt(p.count));workspace.Apply(p.count,IntBytes(value));}
    private void CheckItemTotal(uint id,long delta){if(!inventoryBoundsAvailable||!originalTotals.TryGetValue(id,out long ceiling)||!knownIds.Contains(id))throw new InvalidOperationException("Original item totals are unknown or anomalous; increases and additions are unavailable.");long total=0;foreach(var p in inventory){int count=ReadInt(p.count);if(count<0)throw new InvalidOperationException("Anomalous staged inventory is read-only.");if(ReadUInt(p.id)==id)total=checked(total+count);}if(total+delta<0||total+delta>ceiling)throw new InvalidOperationException("Requested combined quantity exceeds this ID's original total. This conservative bound is not a game maximum.");}
    public int AddKnownItem(uint id,int quantity)
    {
        if(!knownIds.Contains(id))throw new ArgumentException("Choose a nonempty item ID observed in the original selected inventory.");if(quantity<=0)throw new ArgumentOutOfRangeException(nameof(quantity));CheckItemTotal(id,quantity);
        for(int i=0;i<inventory.Count;i++){var p=inventory[i];if(p.id>=0&&ReadUInt(p.id)==EmptyItemId&&ReadInt(p.count)==0){workspace.Apply(new Dictionary<int,byte[]>{{p.id,UIntBytes(id)},{p.count,IntBytes(quantity)}});return i;}}
        throw new InvalidOperationException("No recognized empty inventory record.");
    }
    public void DeleteItem(int index){var p=Item(index);if(ReadInt(p.count,true)<0||ReadInt(p.count)<0)throw new InvalidOperationException("Anomalous item record is read-only.");workspace.Apply(new Dictionary<int,byte[]>{{p.id,UIntBytes(EmptyItemId)},{p.count,IntBytes(0)}});}
    public IReadOnlyList<EquipmentItem> Equipment(uint? type=null,string filter="")
    {
        var rows=new List<EquipmentItem>();for(int i=0;i<equipment.Count;i++){var p=equipment[i];if(p.type<0){rows.Add(new(i,null,null,"Missing or ambiguous equipment fields."));continue;}uint t=ReadUInt(p.type),id=ReadUInt(p.id);if(type is not null&&type!=t)continue;if(filter.Length>0&&!id.ToString().Contains(filter,StringComparison.OrdinalIgnoreCase)&&!$"0x{id:X8}".Contains(filter,StringComparison.OrdinalIgnoreCase))continue;rows.Add(new(i,t,id,null));}return rows;
    }
}
public sealed record DirectoryFileResult(string Label,string? OutputPath,ExperimentalExportReceipt? Receipt,string? Error);
public sealed class DirectoryEditor
{
    private string? directory;
    public EditorWorkspace Slot {get;private set;}=new();
    public EditorWorkspace? System {get;private set;}
    public DomainEditor? Domain {get;private set;}
    public int? SlotNumber {get;private set;}
    public int? CachedMoney=>System is not null&&cachedMoney>=0?System.ReadSigned32(cachedMoney):null;
    public string MoneyMirrorStatus {get;private set;}="Primary-only: system cached preview remains unchanged.";
    private int cachedMoney=-1;
    private readonly Stack<(int slot,int system)> actions=[],redoActions=[];
    public bool CanUndo=>actions.Count>0;
    public bool CanRedo=>redoActions.Count>0;
    public int StagedCount=>Slot.StagedCount+(System?.StagedCount??0);
    public IReadOnlyList<SlotCandidate> OpenDirectory(string path){Slot=new();System=null;Domain=null;SlotNumber=null;cachedMoney=-1;actions.Clear();redoActions.Clear();directory=Path.GetFullPath(path);return SlotDiscovery.Discover(directory);}
    public void OpenSlot(string path)
    {
        actions.Clear();redoActions.Clear();Slot=new();System=null;Domain=null;cachedMoney=-1;MoneyMirrorStatus="Primary-only: system cached preview remains unchanged.";
        Slot.Open(path);Domain=new(Slot);SlotNumber=Path.GetFileName(path).ToLowerInvariant() switch{"data001slot.bin"=>1,"data002slot.bin"=>2,"data003slot.bin"=>3,_=>null};
        if(directory is null||SlotNumber is null||!string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),directory,StringComparison.OrdinalIgnoreCase))return;
        string systemPath=Path.Combine(directory,"data00-1.bin");if(!File.Exists(systemPath))return;
        try
        {
            var system=new EditorWorkspace();system.Open(systemPath);var arrays=system.Document!.Structure.Events.Select((e,i)=>(e,i)).Where(p=>p.e.Identifier==28334441).ToArray();
            if(arrays.Length!=1||arrays[0].e.Tag!=-1)throw new InvalidDataException("System character array is absent or ambiguous.");var hunters=system.ChildIndices(arrays[0].i);if(hunters.Count!=3||hunters.Any(i=>system.Document.Structure.Events[i].Tag!=17))throw new InvalidDataException("System character array does not have three objects.");
            int hunter=hunters[SlotNumber.Value-1];var helper=new DomainEditor(system);int name=helper.Child(hunter,4154407670,15);int money=helper.Child(hunter,884539280,7,4);
            if(Domain.Character().Name is not string slotName||system.TextAt(name).TrimEnd('\0')!=slotName)throw new InvalidDataException("System/slot name correlation is not established.");
            System=system;cachedMoney=money;MoneyMirrorStatus="Directory context: primary money and corresponding system cached money will be staged together.";
        }
        catch(Exception e){MoneyMirrorStatus="Primary-only: system cached preview not linked. "+e.Message;}
    }
    public void SetMoney(int value)
    {
        if(Domain is null)throw new InvalidOperationException("Select a slot.");if(value<0)throw new ArgumentOutOfRangeException(nameof(value));
        if(!Domain.CanEditMoney)throw new InvalidOperationException("Primary money mapping unavailable.");
        if(System is not null&&cachedMoney>=0&&System.PrimitiveBytes(cachedMoney)?.Length!=4)throw new InvalidOperationException("System money mapping changed.");
        Track(()=>{Domain.SetMoney(value);if(System is not null&&cachedMoney>=0)System.Apply(cachedMoney,DomainEditor.IntBytes(value));});
    }
    private void Track(Action action){var before=(Slot.UndoDepth,System?.UndoDepth??0);var slotState=Slot.CaptureHistory();var systemState=System?.CaptureHistory();try{action();}catch{Slot.RestoreHistory(slotState);if(System is not null&&systemState is not null)System.RestoreHistory(systemState);throw;}if(before!=(Slot.UndoDepth,System?.UndoDepth??0)){actions.Push(before);redoActions.Clear();}}
    public void ApplyDomain(Action<DomainEditor> action){if(Domain is null)throw new InvalidOperationException("Select a slot.");Track(()=>action(Domain));}
    public void ApplyRaw(int eventIndex,byte[] bytes)=>Track(()=>Slot.Apply(eventIndex,bytes));
    public bool Undo(){if(!actions.TryPop(out var before))return false;redoActions.Push((Slot.UndoDepth,System?.UndoDepth??0));while(Slot.UndoDepth>before.slot)Slot.Undo();if(System is not null)while(System.UndoDepth>before.system)System.Undo();return true;}
    public bool Redo(){if(!redoActions.TryPop(out var after))return false;actions.Push((Slot.UndoDepth,System?.UndoDepth??0));while(Slot.UndoDepth<after.slot)if(!Slot.Redo())throw new InvalidOperationException("Slot history mismatch.");if(System is not null)while(System.UndoDepth<after.system)if(!System.Redo())throw new InvalidOperationException("System history mismatch.");return true;}
    public IReadOnlyList<StagedChange> ReviewChanges()=>Slot.ReviewChanges("Selected slot").Select(r=>r with{Capability=Domain?.ChangeLabel(r.EventIndex)??r.Capability}).Concat(System?.ReviewChanges("System").Select(r=>r with{Capability=r.EventIndex==cachedMoney?"Money / cached preview":r.Capability})??[]).ToArray();
    public void Clear()=>Track(()=>{Slot.Clear();System?.Clear();});
    public IReadOnlyList<DirectoryFileResult> ExportCopies(string newDirectory,string? backupParent=null)
    {
        if(Domain is null)throw new InvalidOperationException("Select a slot.");if(!Directory.Exists(newDirectory))Directory.CreateDirectory(newDirectory);
        backupParent??=newDirectory; var result=new List<DirectoryFileResult>();
        if(directory is null||!string.Equals(Path.GetDirectoryName(Slot.SourcePath),directory,StringComparison.OrdinalIgnoreCase)){try{var receipt=Slot.Export(Path.Combine(newDirectory,Path.GetFileName(Slot.SourcePath)!),backupParent);result.Add(new("Selected file",receipt.OutputPath,receipt,null));}catch(Exception e){result.Add(new("Selected file",null,null,e.Message));}return result;}
        var candidates=SlotDiscovery.Discover(directory);var ordered=candidates.OrderBy(c=>string.Equals(c.Path,Slot.SourcePath,StringComparison.OrdinalIgnoreCase)?0:c.SlotNumber is null?2:1);
        bool selectedCompleted=false;
        foreach(var candidate in ordered)
        {
            if(candidate.Status!=SlotStatus.Recognized){result.Add(new(candidate.Label,null,null,candidate.Status+": "+candidate.Detail));continue;}
            bool selected=string.Equals(candidate.Path,Slot.SourcePath,StringComparison.OrdinalIgnoreCase);
            if(candidate.SlotNumber is null&&System?.StagedCount>0&&!selectedCompleted){result.Add(new(candidate.Label,null,null,"Changed system preview not exported because selected-slot export did not complete."));continue;}
            try
            {
                EditorWorkspace current;if(selected)current=Slot;else if(System is not null&&string.Equals(System.SourcePath,candidate.Path,StringComparison.OrdinalIgnoreCase))current=System;else{current=new();current.Open(candidate.Path);}
                var receipt=current.Export(Path.Combine(newDirectory,Path.GetFileName(candidate.Path)),backupParent);result.Add(new(candidate.Label,receipt.OutputPath,receipt,null));if(selected)selectedCompleted=true;
            }
            catch(Exception e){result.Add(new(candidate.Label,null,null,e.Message));}
        }
        return result;
    }
}
