namespace FreshFoundation;
public enum CatalogProviderSource { Local, External }
public enum CatalogMappingStatus { UNVERIFIED_MAPPING, PROBABLE_MAPPING, VERIFIED_NS_MAPPING }
public sealed class CatalogMapping
{
    public GameCatalogEntry Entry {get;}
    public string Provider {get;}
    public CatalogProviderSource SourceKind {get;}
    public CatalogMappingStatus MappingStatus {get;}
    public string Evidence {get;}
    internal CatalogMapping(GameCatalogEntry entry,string provider,CatalogProviderSource source,CatalogMappingStatus status,string evidence)
    {
        // No admitted independent NS name evidence exists in this phase. JSON declarations and ID hits cannot issue a verified mapping.
        if(status==CatalogMappingStatus.VERIFIED_NS_MAPPING)throw new NotSupportedException("Independent NS name evidence has not been admitted.");
        Entry=entry;Provider=provider;SourceKind=source;MappingStatus=status;Evidence=evidence;
    }
}
public interface IGameCatalogProvider
{
    string Name {get;}
    CatalogProviderSource SourceKind {get;}
    IReadOnlyList<CatalogMapping> Find(GameCatalogKind kind,uint id,uint? equipmentKind=null);
}
public sealed class LocalGameCatalogProvider(GameCatalog catalog) : IGameCatalogProvider
{
    public string Name=>"Local catalog";
    public CatalogProviderSource SourceKind=>CatalogProviderSource.Local;
    public IReadOnlyList<CatalogMapping> Find(GameCatalogKind kind,uint id,uint? equipmentKind=null)=>catalog.Find(kind,id,equipmentKind) is GameCatalogEntry e&&e.Names.Count>0?[new(e,Name,SourceKind,CatalogMappingStatus.UNVERIFIED_MAPPING,"User-selected local declaration; no independent NS name match")]:[];
}
public sealed class MhriceCatalogProvider(GameCatalog catalog,string datasetIdentity) : IGameCatalogProvider
{
    public string Name=>"MHRice";
    public CatalogProviderSource SourceKind=>CatalogProviderSource.External;
    public IReadOnlyList<CatalogMapping> Find(GameCatalogKind kind,uint id,uint? equipmentKind=null)=>catalog.Find(kind,id,equipmentKind) is GameCatalogEntry e&&e.Names.Count>0?[new(e,Name,SourceKind,CatalogMappingStatus.UNVERIFIED_MAPPING,"External dataset "+datasetIdentity+"; name candidate only; no independently verified NS name mapping")]:[];
}
public sealed class CatalogProviderNames : IPresentationNames
{
    private readonly List<IGameCatalogProvider> providers=[];
    private readonly Func<string> locale;
    private readonly PresentationNames raw=new();
    public bool ShowUnverifiedCandidates {get;set;}
    public CatalogProviderNames(Func<string>? locale=null){this.locale=locale??(()=>"en");}
    public void SetProvider(IGameCatalogProvider provider){providers.RemoveAll(p=>p.Name==provider.Name);providers.Add(provider);}
    public IReadOnlyList<CatalogMapping> ResolveAll(GameCatalogKind kind,uint id,uint? equipmentKind=null)=>providers.SelectMany(p=>p.Find(kind,id,equipmentKind)).OrderBy(Rank).ToArray();
    // Verified priorities are reserved for a future independently reviewed evidence issuer, unavailable in this phase.
    private static int Rank(CatalogMapping mapping)=>mapping.MappingStatus==CatalogMappingStatus.VERIFIED_NS_MAPPING?(mapping.SourceKind==CatalogProviderSource.Local?0:1):mapping.SourceKind==CatalogProviderSource.Local?2:3;
    public CatalogMapping? Resolve(GameCatalogKind kind,uint id,uint? equipmentKind=null)=>ResolveAll(kind,id,equipmentKind).FirstOrDefault(m=>m.MappingStatus==CatalogMappingStatus.VERIFIED_NS_MAPPING||ShowUnverifiedCandidates);
    public string Label(CatalogNamespace space,uint id,uint? equipmentKind=null)
    {
        GameCatalogKind? kind=space switch{CatalogNamespace.Item=>GameCatalogKind.Item,CatalogNamespace.Skill=>GameCatalogKind.Skill,CatalogNamespace.Equipment when equipmentKind==1=>GameCatalogKind.Weapon,CatalogNamespace.Equipment when equipmentKind==2=>GameCatalogKind.Armor,_=>null};
        if(kind is GameCatalogKind k&&Resolve(k,id,equipmentKind) is CatalogMapping m)return m.Entry.Name(locale())+(m.MappingStatus==CatalogMappingStatus.VERIFIED_NS_MAPPING?"":locale()=="zh-CN"?" [未验证候选]":" [unverified candidate]");return raw.Label(space,id,equipmentKind);
    }
    public string GameLabel(GameCatalogKind kind,uint id,uint? equipmentKind=null)=>Resolve(kind,id,equipmentKind) is CatalogMapping m?m.Entry.Name(locale())+(locale()=="zh-CN"?" [未验证候选]":" [unverified candidate]"):$"{(locale()=="zh-CN"?kind switch{GameCatalogKind.Skill=>"技能",GameCatalogKind.Decoration=>"装饰珠",_=>kind.ToString()}:kind.ToString())} {id}";
    public bool Matches(GameCatalogKind kind,uint id,string query,uint? equipmentKind=null)=>string.IsNullOrEmpty(query)||$"{id} 0x{id:X8}".Contains(query,StringComparison.OrdinalIgnoreCase)||(Resolve(kind,id,equipmentKind) is CatalogMapping m&&m.Entry.Names.Values.Any(n=>n.Contains(query,StringComparison.OrdinalIgnoreCase)));
    public string? Category(GameCatalogKind kind,uint id,uint? equipmentKind=null)=>Resolve(kind,id,equipmentKind)?.Entry.Category;
    public string Describe(GameCatalogKind kind,uint id,uint? equipmentKind=null)=>string.Join("\n\n",ResolveAll(kind,id,equipmentKind).Select(m=>$"{kind} / kind={equipmentKind?.ToString()??"—"} / ID={id} / 0x{id:X8}\n{m.Provider}: {m.MappingStatus}\n{(ShowUnverifiedCandidates||m.MappingStatus==CatalogMappingStatus.VERIFIED_NS_MAPPING?m.Entry.Name(locale()):"Name candidate hidden")}\n{m.Evidence}\nSource: {m.Entry.Origin?.Source}\nLicense: {m.Entry.Origin?.License}\nEdition: {m.Entry.Origin?.Edition}\n{m.Entry.Origin?.Notes}"));
}
