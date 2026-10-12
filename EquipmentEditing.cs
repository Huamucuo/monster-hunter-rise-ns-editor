namespace FreshFoundation;
public sealed record EquipmentDraft(int RecordIndex,uint Type,uint Id,string SourceDigest,IReadOnlyList<uint>? Decorations,IReadOnlyList<uint>? OriginalDecorations,IReadOnlyList<SkillPair>? Pairs,string? DecorationIssue,string? PairIssue);
public sealed partial class DomainEditor
{
    internal string ChangeLabel(int index)
    {
        if(index==money)return "Money";if(index==points)return "Points";
        for(int i=0;i<inventory.Count;i++){if(inventory[i].id==index)return $"Inventory {i} / ID";if(inventory[i].count==index)return $"Inventory {i} / Quantity";}
        for(int i=0;i<equipment.Count;i++){try{if(PrimitiveArray(equipment[i].node,4129230907,3,1,4).Contains(index))return $"Equipment {i} / Decoration ID";}catch(InvalidDataException){}try{var pair=PairFields(i);if(pair.ids.Contains(index))return $"Talisman {i} / Skill ID";if(pair.levels.Contains(index))return $"Talisman {i} / Skill level";}catch(Exception e)when(e is InvalidDataException or InvalidOperationException){}}
        return "Raw / opaque (read-only)";
    }
    public int? OriginalStackCeiling(uint id)=>inventoryBoundsAvailable?inventory.Where(p=>ReadUInt(p.id,true)==id&&ReadInt(p.count,true)>0).Select(p=>(int?)ReadInt(p.count,true)).Max():null;
    public int NextEmptyInventoryRecord()=>Enumerable.Range(0,inventory.Count).FirstOrDefault(i=>inventory[i].id>=0&&ReadUInt(inventory[i].id)==EmptyItemId&&ReadInt(inventory[i].count)==0,-1);
    public void TransferItem(int sourceIndex,int destinationIndex,int quantity)
    {
        if(sourceIndex==destinationIndex||quantity<=0)throw new ArgumentException("Choose different records and a positive quantity.");
        var source=Item(sourceIndex);var target=Item(destinationIndex);uint id=ReadUInt(source.id),targetId=ReadUInt(target.id);int count=ReadInt(source.count),targetCount=ReadInt(target.count);
        if(id==EmptyItemId||count<=0||quantity>count||targetCount<0||!(targetId==id&&targetCount>0||targetId==EmptyItemId&&targetCount==0))throw new InvalidOperationException("Transfer source or target is not admitted.");
        CheckItemTotal(id,0);int ceiling=OriginalStackCeiling(id)??throw new InvalidOperationException("Original stack bound unavailable.");int remaining=checked(count-quantity),combined=checked(targetCount+quantity);
        if(remaining>ceiling||combined>ceiling)throw new InvalidOperationException("Final stack exceeds original observed stack bound; this is not a game maximum.");
        workspace.Apply(new Dictionary<int,byte[]>{{source.id,UIntBytes(remaining==0?EmptyItemId:id)},{source.count,IntBytes(remaining)},{target.id,UIntBytes(id)},{target.count,IntBytes(combined)}});
    }
    public EquipmentDraft ReadEquipmentDraft(int index)
    {
        if(index<0||index>=equipment.Count)throw new ArgumentOutOfRangeException(nameof(index));var p=equipment[index];
        if(p.type<0||ReadUInt(p.type) is <1 or >3||ReadUInt(p.type)!=ReadUInt(p.type,true)||ReadUInt(p.id)!=ReadUInt(p.id,true))throw new InvalidOperationException("Only unchanged existing weapon, armor or talisman identities are supported.");
        uint[]? decorations=null,original=null;SkillPair[]? pairs=null;string? decorationIssue=null,pairIssue=null;
        try{var fields=PrimitiveArray(p.node,4129230907,3,1,4);decorations=fields.Select(i=>ReadUInt(i)).ToArray();original=fields.Select(i=>ReadUInt(i,true)).ToArray();}catch(Exception e){decorationIssue=e.Message;}
        if(ReadUInt(p.type)==3)try{pairs=ReadPairs(index).ToArray();}catch(Exception e){pairIssue=e.Message;}
        return new(index,ReadUInt(p.type),ReadUInt(p.id),workspace.Document!.Digest,decorations,original,pairs,decorationIssue,pairIssue);
    }
    public void ApplyEquipmentDraft(EquipmentDraft opened,IReadOnlyList<uint>? decorations,IReadOnlyList<SkillPair>? pairs)
    {
        ArgumentNullException.ThrowIfNull(opened);var current=ReadEquipmentDraft(opened.RecordIndex);
        if(opened.SourceDigest!=current.SourceDigest||opened.Id!=current.Id||opened.Type!=current.Type)throw new InvalidOperationException("Equipment identity changed; reopen draft.");
        if(!(opened.Decorations??[]).SequenceEqual(current.Decorations??[])||!(opened.Pairs??[]).SequenceEqual(current.Pairs??[]))throw new InvalidOperationException("Equipment changed while draft was open.");
        var edits=new Dictionary<int,byte[]>();var node=equipment[opened.RecordIndex].node;
        if(decorations is not null){if(current.OriginalDecorations is null||decorations.Count!=3)throw new ArgumentException("Exactly three admitted decoration positions are required.");var fields=PrimitiveArray(node,4129230907,3,1,4);for(int i=0;i<3;i++){uint value=decorations[i];if(value!=0&&value!=current.OriginalDecorations[i])throw new ArgumentException("Only zero or original same-position decoration is permitted.");edits.Add(fields[i],UIntBytes(value));}}
        if(pairs is not null){if(current.Type!=3||current.Pairs is null||pairs.Count!=2)throw new ArgumentException("Exactly two admitted talisman pairs are required.");var fields=PairFields(opened.RecordIndex);for(int i=0;i<2;i++){var pair=pairs[i];if(!knownPairs.Contains(pair))throw new ArgumentException("Choose an original observed skill/level pair.");edits.Add(fields.ids[i],[pair.Id]);edits.Add(fields.levels[i],UIntBytes(pair.Level));}}
        var changed=edits.Where(p=>!workspace.PrimitiveBytes(p.Key)!.SequenceEqual(p.Value)).ToDictionary(p=>p.Key,p=>p.Value);if(changed.Count>0)workspace.Apply(changed);
    }
}
