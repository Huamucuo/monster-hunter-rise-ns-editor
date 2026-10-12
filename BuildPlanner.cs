using System.Text.Json;
namespace FreshFoundation;
public enum BuildPosition { Weapon, Head, Chest, Arms, Waist, Legs, Talisman }
public sealed record VirtualTalisman(SkillPair[] Pairs,int[] SlotLevels);
public sealed record PlannedPiece(BuildPosition Position,uint? Id,uint[] Decorations,VirtualTalisman? Talisman=null);
public sealed record PortableBuild(string Schema,int SchemaVersion,string Title,PlannedPiece[] Pieces);
public sealed record PlannedSkill(uint Id,long Total,uint? DeclaredMaximum,long Effective,long Excess);
public sealed record BuildSummary(int? Defense,CatalogResistances? Resistances,int? Attack,int? Affinity,IReadOnlyList<PlannedSkill> Skills,bool Partial,IReadOnlyList<string> Issues);
public sealed class BuildPlanner
{
    private readonly GameCatalog catalog;
    private Dictionary<BuildPosition,PlannedPiece> pieces=[];
    public bool ShowCandidates {get;set;}
    public string Title {get;set;}="Untitled plan";
    public BuildPlanner(GameCatalog catalog)=>this.catalog=catalog;
    public static string? ArmorCategory(BuildPosition position)=>position switch{BuildPosition.Head=>"Head",BuildPosition.Chest=>"Chest",BuildPosition.Arms=>"Arm",BuildPosition.Waist=>"Waist",BuildPosition.Legs=>"Leg",_=>null};
    private static PlannedPiece Clone(PlannedPiece p)=>p with{Decorations=p.Decorations.ToArray(),Talisman=p.Talisman is null?null:new(p.Talisman.Pairs.ToArray(),p.Talisman.SlotLevels.ToArray())};
    public PlannedPiece? Piece(BuildPosition position)=>pieces.TryGetValue(position,out var p)?Clone(p):null;
    public IReadOnlyList<GameCatalogEntry> Choices(BuildPosition position,string query="",string? category=null)=>!ShowCandidates||position==BuildPosition.Talisman?[]:catalog.Search(query,position==BuildPosition.Weapon?GameCatalogKind.Weapon:GameCatalogKind.Armor,position==BuildPosition.Weapon?category:ArmorCategory(position));
    public IReadOnlyList<GameCatalogEntry> Skills(string query="")=>!ShowCandidates?[]:catalog.Search(query,GameCatalogKind.Skill).Where(e=>e.DeclaredMaximumSkillLevel is >0 and <=255).ToArray();
    public GameCatalogEntry? Reference(PlannedPiece p)=>!ShowCandidates||p.Id is null||p.Position==BuildPosition.Talisman?null:catalog.Find(p.Position==BuildPosition.Weapon?GameCatalogKind.Weapon:GameCatalogKind.Armor,p.Id.Value);
    public GameCatalogEntry? Decoration(uint id)=>ShowCandidates?catalog.Find(GameCatalogKind.Decoration,id):null;
    public GameCatalogEntry? Skill(uint id)=>ShowCandidates?catalog.Find(GameCatalogKind.Skill,id):null;
    public IReadOnlyList<GameCatalogEntry> Decorations(string query="",int? slot=null)
    {
        if(!ShowCandidates||slot is <1 or >4)return [];
        return catalog.Entries.Where(e=>e.Kind==GameCatalogKind.Decoration&&(slot is null||e.RequiredSlotLevel is >=1 and <=4&&e.RequiredSlotLevel<=slot)&&($"{e.Id} 0x{e.Id:X8}".Contains(query,StringComparison.OrdinalIgnoreCase)||e.Names.Values.Any(n=>n.Contains(query,StringComparison.OrdinalIgnoreCase))||(e.SkillRelations??[]).Any(r=>catalog.Find(GameCatalogKind.Skill,r.SkillId)?.Names.Values.Any(n=>n.Contains(query,StringComparison.OrdinalIgnoreCase))==true))).ToArray();
    }
    public int[]? Slots(PlannedPiece p)=>p.Position==BuildPosition.Talisman?p.Talisman?.SlotLevels.ToArray():Reference(p)?.EquipmentStats?.BaseSlotLevels?.ToArray();
    private static void Validate(PlannedPiece p)
    {
        if(!Enum.IsDefined(p.Position)||p.Decorations is null||p.Decorations.Length!=3)throw new InvalidDataException("Plan piece needs a recognized position and three decoration IDs.");
        if(p.Talisman is not null&&(p.Position!=BuildPosition.Talisman||p.Id is not null||p.Talisman.Pairs is null||p.Talisman.Pairs.Length!=2||p.Talisman.Pairs.Any(x=>x is null||x.Level>255||x.Id==0&&x.Level!=0||x.Id!=0&&x.Level==0)||p.Talisman.SlotLevels is null||p.Talisman.SlotLevels.Length!=3||p.Talisman.SlotLevels.Any(x=>x<0||x>4)))throw new InvalidDataException("Invalid virtual talisman pairs or plan slot levels.");
        if(p.Position==BuildPosition.Talisman&&p.Id is not null)throw new InvalidDataException("Talisman position accepts a virtual configuration only.");
    }
    public void Confirm(PlannedPiece draft){Validate(draft);pieces[draft.Position]=Clone(draft);}
    public void Clear(){pieces.Clear();Title="Untitled plan";}
    public string ToJson(){if(Title is null||Title.Length>200)throw new InvalidDataException("Plan title must be at most 200 characters.");return JsonSerializer.Serialize(new PortableBuild("rise-offline-build",1,Title,pieces.Values.OrderBy(p=>p.Position).Select(Clone).ToArray()),new JsonSerializerOptions{WriteIndented=true});}
    public void LoadJson(string json)
    {
        if(System.Text.Encoding.UTF8.GetByteCount(json)>1024*1024)throw new InvalidDataException("Plan exceeds 1 MiB.");
        var plan=JsonSerializer.Deserialize<PortableBuild>(json)??throw new InvalidDataException("Plan is empty.");if(plan.Schema!="rise-offline-build"||plan.SchemaVersion!=1||plan.Title is null||plan.Title.Length>200||plan.Pieces is null||plan.Pieces.Length>7)throw new InvalidDataException("Unsupported plan schema, version or size.");var pending=new Dictionary<BuildPosition,PlannedPiece>();foreach(var p in plan.Pieces){if(p is null)throw new InvalidDataException("Null plan piece.");Validate(p);if(!pending.TryAdd(p.Position,Clone(p)))throw new InvalidDataException("Duplicate plan position.");}pieces=pending;Title=plan.Title;
    }
    public void Load(string explicitlySelectedFile){if(new FileInfo(explicitlySelectedFile).Length>1024*1024)throw new InvalidDataException("Plan exceeds 1 MiB.");LoadJson(File.ReadAllText(explicitlySelectedFile));}
    public void SaveNew(string explicitlySelectedFile){string json=ToJson();using var stream=new FileStream(explicitlySelectedFile,FileMode.CreateNew,FileAccess.Write);using var writer=new StreamWriter(stream);writer.Write(json);}
    public BuildSummary PreviewPiece(PlannedPiece draft){var preview=new BuildPlanner(catalog){ShowCandidates=ShowCandidates};preview.Confirm(draft);return preview.Summarize();}
    public BuildSummary Summarize()
    {
        var issues=new List<string>();var totals=new Dictionary<uint,long>();int defense=0;bool defenseKnown=true;var resist=new int[5];var resistKnown=new[]{true,true,true,true,true};int? attack=null,affinity=null;
        void AddRelations(CatalogSkillRelation[]? relations,string label){if(relations is null){issues.Add(label+": skill relations unknown");return;}foreach(var relation in relations)totals[relation.SkillId]=checked(totals.GetValueOrDefault(relation.SkillId)+relation.Level);}
        foreach(var p in pieces.Values){bool selected=p.Id is not null||p.Talisman is not null;if(!selected){if(p.Decorations.Any(id=>id!=0))issues.Add(p.Position+": decorations without a piece excluded");continue;}if(!ShowCandidates){issues.Add(p.Position+": candidate metadata hidden; summary unavailable");defenseKnown=false;Array.Fill(resistKnown,false);continue;}
            int[]? slots=null;
            if(p.Position==BuildPosition.Talisman){slots=p.Talisman!.SlotLevels;foreach(var pair in p.Talisman.Pairs){if(pair.Id==0&&pair.Level==0)continue;var maximum=Skill(pair.Id)?.DeclaredMaximumSkillLevel;if(maximum is null||pair.Level>maximum){issues.Add("Talisman: unresolved/over-limit pair excluded");continue;}totals[pair.Id]=totals.GetValueOrDefault(pair.Id)+pair.Level;}}
            else{var entry=Reference(p);if(entry is null||p.Position!=BuildPosition.Weapon&&entry.Category!=ArmorCategory(p.Position)){issues.Add(p.Position+": missing or wrong-part catalog ID "+p.Id);defenseKnown=false;if(p.Position!=BuildPosition.Weapon)Array.Fill(resistKnown,false);continue;}var stats=entry.EquipmentStats;slots=stats?.BaseSlotLevels;if(p.Position==BuildPosition.Weapon){attack=stats?.BaseAttack;affinity=stats?.AffinityPercent;if(attack is null||affinity is null)issues.Add("Weapon: attack/affinity unknown");if(stats?.DefenseBonus is int bonus)defense=checked(defense+bonus);else defenseKnown=false;}else{if(stats?.BaseDefense is int value)defense=checked(defense+value);else defenseKnown=false;int?[] values=[stats?.Resistances?.Fire,stats?.Resistances?.Water,stats?.Resistances?.Thunder,stats?.Resistances?.Ice,stats?.Resistances?.Dragon];for(int i=0;i<5;i++)if(values[i] is int n)resist[i]=checked(resist[i]+n);else resistKnown[i]=false;AddRelations(entry.SkillRelations,p.Position.ToString());}}
            if(slots is null||slots.Length!=3||slots.Any(s=>s<0||s>4))issues.Add(p.Position+": base slots unknown");
            for(int i=0;i<3;i++){uint id=p.Decorations[i];if(id==0)continue;var deco=catalog.Find(GameCatalogKind.Decoration,id);if(slots is null||slots.Length!=3||slots[i] is <1 or >4||deco?.RequiredSlotLevel is not uint need||need is <1 or >4||need>slots[i]){issues.Add(p.Position+$": decoration {id} at {i+1} unresolved or does not fit; excluded");continue;}AddRelations(deco.SkillRelations,p.Position+" decoration "+id);}
        }
        if(!defenseKnown)issues.Add("Defense total partial/unknown");if(resistKnown.Any(x=>!x))issues.Add("Resistance totals partial/unknown");var skills=totals.OrderBy(p=>p.Key).Select(p=>{uint? max=Skill(p.Key)?.DeclaredMaximumSkillLevel;if(max is null)issues.Add("Skill "+p.Key+": maximum unknown");return new PlannedSkill(p.Key,p.Value,max,max is uint m?Math.Min(p.Value,m):p.Value,max is uint n?Math.Max(0,p.Value-n):0);}).ToArray();int? R(int i)=>resistKnown[i]?resist[i]:null;
        return new(defenseKnown?defense:null,new(R(0),R(1),R(2),R(3),R(4)),attack,affinity,skills,issues.Count>0,issues);
    }
}
