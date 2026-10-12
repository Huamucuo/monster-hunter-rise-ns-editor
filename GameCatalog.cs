using System.Text.Json;
using System.Text.Json.Serialization;
namespace FreshFoundation;
public enum GameCatalogKind { Item, Weapon, Armor, Skill, Decoration, Rampage }
public enum CatalogItemStatus { KNOWN_ITEM, VERIFIED_ITEM, UNKNOWN_ITEM, UNSUPPORTED }
public sealed record CatalogOrigin(string? Source=null,string? License=null,string? Edition=null,string? Notes=null);
public sealed record CatalogSkillRelation(uint SkillId,uint Level);
public sealed record CatalogResistances(int? Fire=null,int? Water=null,int? Thunder=null,int? Ice=null,int? Dragon=null);
public sealed record CatalogElement(string Type,int Value);
public sealed record CatalogEquipmentStats(int? BaseAttack=null,int? AffinityPercent=null,int? DefenseBonus=null,int? BaseDefense=null,CatalogResistances? Resistances=null,int[]? BaseSlotLevels=null,CatalogElement[]? Elements=null);
public sealed record GameCatalogEntry(GameCatalogKind Kind,uint Id,Dictionary<string,string> Names,string? Category=null,uint? EquipmentKind=null,CatalogOrigin? Origin=null,CatalogItemStatus DeclaredStatus=CatalogItemStatus.UNKNOWN_ITEM,long? DeclaredMaximumQuantity=null,uint? DeclaredMaximumSkillLevel=null,uint? RequiredSlotLevel=null,CatalogSkillRelation[]? SkillRelations=null,int? Rarity=null,CatalogEquipmentStats? EquipmentStats=null)
{
    public CatalogItemStatus EffectiveStatus=>DeclaredStatus==CatalogItemStatus.UNSUPPORTED?CatalogItemStatus.UNSUPPORTED:Origin is null||string.IsNullOrWhiteSpace(Origin.Source)||string.IsNullOrWhiteSpace(Origin.License)?CatalogItemStatus.UNKNOWN_ITEM:DeclaredStatus is CatalogItemStatus.KNOWN_ITEM or CatalogItemStatus.VERIFIED_ITEM?CatalogItemStatus.KNOWN_ITEM:CatalogItemStatus.UNKNOWN_ITEM;
    public string Verification=>"LOCAL_CATALOG_CLAIM_ONLY; no independent range verification";
    public string Name(string locale)=>Names.GetValueOrDefault(locale)??Names.GetValueOrDefault("en")??Names.Values.FirstOrDefault()??$"{Kind} {Id}";
}
public sealed record GameCatalogFile(int SchemaVersion,GameCatalogEntry[] Entries);
public sealed class GameCatalog : IPresentationNames
{
    private readonly GameCatalogEntry[] entries;
    private readonly Dictionary<(GameCatalogKind,uint,uint?),GameCatalogEntry> byKey=[];
    private readonly Func<string> locale;
    private readonly PresentationNames fallback=new();
    public IReadOnlyList<GameCatalogEntry> Entries=>entries.Select(Clone).ToArray();
    public string? LoadedFile {get;}
    public GameCatalog(IEnumerable<GameCatalogEntry> records,Func<string>? locale=null,string? loadedFile=null)
    {
        this.locale=locale??(()=>"en");LoadedFile=loadedFile;entries=records.Select(e=>Clone(e) with{EquipmentKind=e.EquipmentKind??(e.Kind==GameCatalogKind.Weapon?1u:e.Kind==GameCatalogKind.Armor?2u:null)}).ToArray();if(entries.Length>100000)throw new InvalidDataException("Catalog exceeds record limit.");var keys=new HashSet<(GameCatalogKind,uint,uint?)>();
        foreach(var e in entries){if(!Enum.IsDefined(e.Kind)||!Enum.IsDefined(e.DeclaredStatus))throw new InvalidDataException("Unknown catalog enum value.");if(e.Kind==GameCatalogKind.Weapon?e.EquipmentKind!=1:e.Kind==GameCatalogKind.Armor?e.EquipmentKind!=2:e.EquipmentKind is not null)throw new InvalidDataException("Equipment kind is permitted only for weapon=1 or armor=2.");if(e.Names is null||e.Names.Any(p=>string.IsNullOrWhiteSpace(p.Key)||string.IsNullOrWhiteSpace(p.Value)||p.Value.Length>1024)||e.DeclaredMaximumQuantity<0)throw new InvalidDataException("Invalid catalog display record.");if(!keys.Add((e.Kind,e.Id,e.EquipmentKind)))throw new InvalidDataException("Duplicate catalog namespace/kind/ID.");byKey.Add((e.Kind,e.Id,e.EquipmentKind),e);}
    }
    private static GameCatalogEntry Clone(GameCatalogEntry e)=>e with{Names=new Dictionary<string,string>(e.Names??[]),SkillRelations=e.SkillRelations?.ToArray(),EquipmentStats=e.EquipmentStats is null?null:e.EquipmentStats with{BaseSlotLevels=e.EquipmentStats.BaseSlotLevels?.ToArray(),Elements=e.EquipmentStats.Elements?.ToArray()}};
    public static GameCatalog LoadDirectory(string explicitlySelectedDirectory,Func<string>? locale=null)
    {
        string file=Path.Combine(Path.GetFullPath(explicitlySelectedDirectory),"catalog.json");if(!File.Exists(file))throw new FileNotFoundException("Selected directory must contain catalog.json.",file);if(new FileInfo(file).Length>8*1024*1024)throw new InvalidDataException("Catalog exceeds 8 MiB.");
        var options=new JsonSerializerOptions(){PropertyNameCaseInsensitive=true};options.Converters.Add(new JsonStringEnumConverter());var data=JsonSerializer.Deserialize<GameCatalogFile>(File.ReadAllText(file),options)??throw new InvalidDataException("Catalog document is empty.");if(data.SchemaVersion!=1||data.Entries is null)throw new InvalidDataException("Unsupported catalog schema.");return new(data.Entries,locale,file);
    }
    public GameCatalogEntry? Find(GameCatalogKind kind,uint id,uint? equipmentKind=null)=>byKey.TryGetValue((kind,id,equipmentKind??(kind==GameCatalogKind.Weapon?1u:kind==GameCatalogKind.Armor?2u:null)),out var entry)?Clone(entry):null;
    public string Label(CatalogNamespace space,uint id,uint? equipmentKind=null)
    {
        GameCatalogKind? kind=space switch{CatalogNamespace.Item=>GameCatalogKind.Item,CatalogNamespace.Skill=>GameCatalogKind.Skill,CatalogNamespace.Equipment when equipmentKind==1=>GameCatalogKind.Weapon,CatalogNamespace.Equipment when equipmentKind==2=>GameCatalogKind.Armor,_=>null};return kind is GameCatalogKind k&&Find(k,id,equipmentKind) is GameCatalogEntry e?e.Name(locale()):fallback.Label(space,id,equipmentKind);
    }
    public IReadOnlyList<GameCatalogEntry> Search(string query="",GameCatalogKind? kind=null,string? category=null)
    {
        string q=query.Trim();return entries.Where(e=>(kind is null||e.Kind==kind)&&(category is null||e.Category==category)&&(q.Length==0||e.Id.ToString().Contains(q,StringComparison.OrdinalIgnoreCase)||$"0x{e.Id:X8}".Contains(q,StringComparison.OrdinalIgnoreCase)||e.Names.Values.Any(n=>n.Contains(q,StringComparison.OrdinalIgnoreCase)))).Select(Clone).ToArray();
    }
    public IReadOnlyList<GameCatalogEntry> DecorationsForKnownSlot(uint? knownSlotLevel,IReadOnlySet<uint>? ownedIds=null)=>knownSlotLevel is not uint level||level is <1 or >4?[]:entries.Where(e=>e.Kind==GameCatalogKind.Decoration&&e.EffectiveStatus==CatalogItemStatus.KNOWN_ITEM&&e.RequiredSlotLevel is uint need&&need is >=1 and <=4&&need<=level&&(ownedIds is null||ownedIds.Contains(e.Id))).Select(Clone).ToArray();
}
