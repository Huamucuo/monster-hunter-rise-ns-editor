namespace FreshFoundation;
public enum CatalogNamespace { Item, EquipmentType, Equipment, Skill }
public sealed record CatalogRecord(CatalogNamespace Namespace,uint Id,string Label,string Source,string License,uint? EquipmentKind=null);
public interface IPresentationNames { string Label(CatalogNamespace space,uint id,uint? equipmentKind=null); }
public sealed class PresentationNames : IPresentationNames
{
    private readonly Dictionary<(CatalogNamespace,uint,uint?),CatalogRecord> entries=[];
    public PresentationNames(IEnumerable<CatalogRecord>? records=null)
    {
        foreach(var r in records??[])
        {
            if(string.IsNullOrWhiteSpace(r.Label)||string.IsNullOrWhiteSpace(r.Source)||string.IsNullOrWhiteSpace(r.License))throw new ArgumentException("Catalog records require label, source and license metadata.");
            if((r.Namespace==CatalogNamespace.Equipment)!=(r.EquipmentKind is not null))throw new ArgumentException("Equipment labels require a kind; other namespaces do not accept one.");
            if(!entries.TryAdd((r.Namespace,r.Id,r.EquipmentKind),r))throw new ArgumentException("Duplicate namespace/kind/ID.");
        }
    }
    public string Label(CatalogNamespace space,uint id,uint? equipmentKind=null)=>entries.TryGetValue((space,id,equipmentKind),out var r)?r.Label:space==CatalogNamespace.Equipment?$"{PresentationRows.TypeName(equipmentKind)} {id}":space==CatalogNamespace.EquipmentType?PresentationRows.TypeName(id):$"{space} {id}";
    public IReadOnlyList<CatalogRecord> Records=>entries.Values.ToArray();
}
public static class PresentationRows
{
    public static string TypeName(uint? type)=>type switch{0=>"Empty",1=>"Weapon",2=>"Armor",3=>"Talisman",_=>"Unknown"};
    public static string ItemName(IPresentationNames names,InventoryItem item)=>item.Id is uint id?names.Label(CatalogNamespace.Item,id):"Unknown";
    public static string EquipmentName(IPresentationNames names,EquipmentItem item)=>item.Id is uint id?names.Label(CatalogNamespace.Equipment,id,item.Type):"Unknown";
}
public sealed record EquipmentFieldSchema(string Label,uint FieldIdentifier,int ObservedTag,uint Width,int Count,IReadOnlyList<uint> Values,string Meaning,string Status="READ_ONLY");

