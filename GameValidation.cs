using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FreshFoundation;

public sealed record ValidationOperation(string Id,string Kind,long? Value=null,int? RecordIndex=null,uint? ItemId=null,int? Position=null,byte? SkillId=null,uint? SkillLevel=null);
public sealed record ValidationPlan(string ExpectedSourceSha256,string ToolRevision,ValidationOperation[] Cases,Dictionary<string,string>? ExpectedBundleHashes=null);
public sealed record HumanGameReport(string CaseId,string ExportSha256,string? GameVersion=null,string? Platform=null,bool? GameLoadSuccess=null,bool? EffectSuccess=null,string? ActualValues=null,bool? ManualResavePerformed=null,string? Notes=null,string[]? PhotoReferences=null,bool? GameResaveSuccess=null);
public sealed record DomainSnapshot(int? Money,uint? Points,int? HunterRank,int? MasterRank,string NameFingerprint,SortedDictionary<uint,long> InventoryTotals,string[] EquipmentMultiset,string EquipmentFingerprint,int? CachedMoney);
public sealed record RawChange(string File,int EventIndex,uint? FieldIdentifier,int Coordinate,string BeforeHex,string ExpectedHex);
public sealed record ValidationManifest(string CaseId,string Status,int SelectedSlot,string ToolRevision,string FormatVersion,string GameVersion,ValidationOperation Operation,Dictionary<string,string> SourceHashes,Dictionary<string,string> ExportHashes,Envelope SourceProfile,DomainSnapshot Before,DomainSnapshot Expected,Dictionary<int,DomainSnapshot> OtherSlots,RawChange[] RawChanges,string NonTargetExpectation,DateTimeOffset CreatedUtc);
public sealed record ValidationResult(string CaseId,string Status,string SourceManifestSha256,string HumanReportSha256,HumanGameReport HumanEvidence,string EvidenceLabel,Dictionary<string,string>? ReturnedHashes,string[] Comparisons,bool ScopedPromotionRecommended,DateTimeOffset CreatedUtc);
public static class GameValidation
{
    public static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
    public static readonly string[] FileNames=["data001Slot.bin","data002Slot.bin","data003Slot.bin","data00-1.bin"];
    public static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    public static T Read<T>(string path)=>JsonSerializer.Deserialize<T>(File.ReadAllText(path),Json)??throw new InvalidDataException("Empty JSON document.");
    private static void WriteNew(string path,object value){using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);JsonSerializer.Serialize(stream,value,value.GetType(),Json);}
    public static Dictionary<string,string> BundleHashes(string directory)
    {
        var candidates=SlotDiscovery.Discover(directory);if(candidates.Count!=4||candidates.Any(c=>c.Status!=SlotStatus.Recognized))throw new InvalidDataException("All four recognized files are required.");
        return FileNames.ToDictionary(n=>n,n=>Hash(File.ReadAllBytes(Path.Combine(directory,n))));
    }
    private static DirectoryEditor Open(string directory,int slot){if(slot is <1 or >3)throw new ArgumentOutOfRangeException(nameof(slot));var e=new DirectoryEditor();e.OpenDirectory(directory);e.OpenSlot(Path.Combine(directory,FileNames[slot-1]));return e;}
    public static DomainSnapshot Capture(DirectoryEditor editor)
    {
        var d=editor.Domain??throw new InvalidOperationException("Select a slot.");var c=d.Character();if(!d.CanEditMoney||!d.CanEditPoints)throw new InvalidDataException("Complete money/points witnesses unavailable.");
        var totals=new SortedDictionary<uint,long>();foreach(var item in d.Inventory()){if(item.Id is null||item.Quantity is null||!item.Editable)throw new InvalidDataException("Incomplete inventory witness.");if(item.Quantity==0)continue;totals[item.Id.Value]=checked(totals.GetValueOrDefault(item.Id.Value)+item.Quantity.Value);}
        var equipment=new List<string>();foreach(var item in d.Equipment())
        {
            if(item.Type is null||item.Id is null||item.Issue is not null)throw new InvalidDataException("Incomplete equipment witness.");
            string entry=$"{item.Type}:{item.Id}:decorations={string.Join(',',d.DecorationIds(item.Index))}";
            if(item.Type==3){var t=d.Talisman(item.Index);entry+=$":pairs={string.Join(',',t.Pairs.Select(p=>$"{p.Id}/{p.Level}"))}:aux={string.Join(',',d.EquipmentSchema(item.Index).Single(f=>f.FieldIdentifier==557620028).Values)}";}
            equipment.Add(entry);
        }
        var sorted=equipment.Order(StringComparer.Ordinal).ToArray();return new(c.Money,c.Points,c.HunterRank,c.MasterRank,Hash(Encoding.UTF8.GetBytes(c.Name??"UNKNOWN")),totals,sorted,Hash(Encoding.UTF8.GetBytes(string.Join('\n',sorted))),editor.CachedMoney);
    }
    public static string[] Compare(DomainSnapshot expected,DomainSnapshot actual)
    {
        var differences=new List<string>();if(expected.Money!=actual.Money)differences.Add("Money differs");if(expected.Points!=actual.Points)differences.Add("Points differ");if(expected.CachedMoney!=actual.CachedMoney)differences.Add("System cached money differs");if(expected.HunterRank!=actual.HunterRank||expected.MasterRank!=actual.MasterRank)differences.Add("Unrelated ranks differ");if(expected.NameFingerprint!=actual.NameFingerprint)differences.Add("Character identity witness differs");
        if(!expected.InventoryTotals.SequenceEqual(actual.InventoryTotals))differences.Add("Inventory quantity totals differ (including unrelated IDs)");if(!expected.EquipmentMultiset.SequenceEqual(actual.EquipmentMultiset))differences.Add("Equipment semantic multiset differs (including unrelated records)");return differences.ToArray();
    }
    private static void ValidateOperation(ValidationOperation op)
    {
        if(!Regex.IsMatch(op.Id??"","^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$")||op.Id=="00-baseline"||Regex.IsMatch(op.Id??"","^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",RegexOptions.IgnoreCase))throw new ArgumentException("Case ID must be a safe unique label; 00-baseline is reserved.");
        bool value=op.Value is not null,record=op.RecordIndex is >=0,item=op.ItemId is not null,pair=op.Position is 0 or 1&&op.SkillId is not null&&op.SkillLevel is not null;
        bool valid=op.Kind switch
        {
            "money"=>value&&op.Value>=0&&op.Value<=int.MaxValue&&!record&&!item&&!pair,
            "points"=>value&&op.Value>=0&&op.Value<=uint.MaxValue&&!record&&!item&&!pair,
            "item-quantity"=>value&&op.Value>=0&&op.Value<=int.MaxValue&&record&&!item&&!pair,
            "add-known-item"=>value&&op.Value>0&&op.Value<=int.MaxValue&&!record&&item&&!pair,
            "delete-item"=>!value&&record&&!item&&!pair,
            "talisman-pair"=>!value&&record&&!item&&pair,
            _=>false
        };
        if(op.Kind!="talisman-pair"&&(op.Position is not null||op.SkillId is not null||op.SkillLevel is not null))valid=false;
        if(op.Kind is "money" or "points" or "add-known-item"&&op.RecordIndex is not null)valid=false;
        if(!valid)throw new ArgumentException("Incorrect operation parameters: "+op.Id);
    }
    private static void Apply(DirectoryEditor e,ValidationOperation op)
    {
        switch(op.Kind)
        {
            case "baseline":return;
            case "money":if(e.System is null)throw new InvalidDataException("Money validation requires the linked system witness.");e.SetMoney((int)op.Value!.Value);break;
            case "points":e.ApplyDomain(d=>d.SetPoints((uint)op.Value!.Value));break;
            case "item-quantity":e.ApplyDomain(d=>d.SetItemQuantity(op.RecordIndex!.Value,(int)op.Value!.Value));break;
            case "add-known-item":e.ApplyDomain(d=>d.AddKnownItem(op.ItemId!.Value,(int)op.Value!.Value));break;
            case "delete-item":e.ApplyDomain(d=>d.DeleteItem(op.RecordIndex!.Value));break;
            case "talisman-pair":e.ApplyDomain(d=>d.SetTalismanPair(op.RecordIndex!.Value,op.Position!.Value,new(op.SkillId!.Value,op.SkillLevel!.Value)));break;
        }
    }
    private static RawChange[] Changes(DirectoryEditor e)
    {
        var changes=new List<RawChange>();foreach(var w in new[]{e.Slot,e.System}.OfType<EditorWorkspace>())foreach(var (v,i) in w.Document!.Structure.Events.Select((v,i)=>(v,i)))if(v.DataCoordinate is int coordinate){var before=w.PrimitiveBytes(i,true)!;var after=w.PrimitiveBytes(i)!;if(!before.SequenceEqual(after))changes.Add(new(Path.GetFileName(w.SourcePath)!,i,v.Identifier,coordinate,Convert.ToHexString(before),Convert.ToHexString(after)));}return changes.ToArray();
    }
    private static bool SameHashes(Dictionary<string,string> a,Dictionary<string,string> b)=>a.Count==b.Count&&a.All(p=>b.TryGetValue(p.Key,out string? h)&&string.Equals(p.Value,h,StringComparison.OrdinalIgnoreCase));
    public static IReadOnlyList<string> Prepare(string sourceDirectory,int slot,ValidationPlan plan,string outputRoot)
    {
        if(slot is <1 or >3)throw new ArgumentOutOfRangeException(nameof(slot));if(string.IsNullOrWhiteSpace(plan.ToolRevision)||plan.Cases is null)throw new ArgumentException("ToolRevision and Cases required.");
        foreach(var op in plan.Cases)ValidateOperation(op);if(plan.Cases.Select(c=>c.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=plan.Cases.Length)throw new ArgumentException("Duplicate case IDs.");
        var sourceHashes=BundleHashes(sourceDirectory);if(!string.Equals(sourceHashes[FileNames[slot-1]],plan.ExpectedSourceSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Stale selected-source hash.");if(plan.ExpectedBundleHashes is not null&&!SameHashes(sourceHashes,plan.ExpectedBundleHashes))throw new InvalidDataException("Stale bundle hashes.");
        if(Directory.Exists(outputRoot)||File.Exists(outputRoot))throw new IOException("A fresh output root is required.");
        // Validate all operations and witnesses before creating the run, with a fresh selection for each.
        foreach(var op in plan.Cases){var e=Open(sourceDirectory,slot);var before=Capture(e);Apply(e,op);if(Changes(e).Length==0||Compare(before,Capture(e)).Length==0)throw new ArgumentException("No-op case: "+op.Id);}
        Directory.CreateDirectory(outputRoot);var manifests=new List<string>();
        foreach(var op in new[]{new ValidationOperation("00-baseline","baseline")}.Concat(plan.Cases))
        {
            if(!SameHashes(sourceHashes,BundleHashes(sourceDirectory)))throw new IOException("Source bundle changed during preparation.");
            var editor=Open(sourceDirectory,slot);var before=Capture(editor);var others=Enumerable.Range(1,3).Where(i=>i!=slot).ToDictionary(i=>i,i=>Capture(Open(sourceDirectory,i)));Apply(editor,op);var expected=Capture(editor);var raw=Changes(editor);
            string caseRoot=Path.Combine(outputRoot,op.Id),import=Path.Combine(caseRoot,"import"),backups=Path.Combine(caseRoot,"backups");Directory.CreateDirectory(caseRoot);Directory.CreateDirectory(backups);
            var results=editor.ExportCopies(import,backups);if(results.Count!=4||results.Any(r=>r.Error is not null||r.Receipt is null))throw new IOException("Incomplete export: "+string.Join("; ",results.Where(r=>r.Error!=null).Select(r=>r.Label+": "+r.Error)));
            if(!SameHashes(sourceHashes,BundleHashes(sourceDirectory)))throw new IOException("Source bundle changed during export.");
            var exportedHashes=BundleHashes(import);var differences=Compare(expected,Capture(Open(import,slot)));foreach(var other in others)differences=differences.Concat(Compare(other.Value,Capture(Open(import,other.Key))).Select(s=>$"Slot {other.Key}: {s}")).ToArray();if(differences.Length>0)throw new InvalidDataException("Export reload differs: "+string.Join("; ",differences));
            var manifest=new ValidationManifest(op.Id,"EXPORTED",slot,plan.ToolRevision,"DSSS observed v2","UNKNOWN",op,sourceHashes,exportedHashes,editor.Slot.Document!.Metadata,before,expected,others,raw,"Only listed raw spans change pre-game. After native resave compare domain witnesses; unknown bytes may change and inventory records may merge/reorder. Unrelated domain changes require investigation.",DateTimeOffset.UtcNow);
            string manifestPath=Path.Combine(caseRoot,"manifest.json");WriteNew(manifestPath,manifest);manifests.Add(manifestPath);WriteNew(Path.Combine(caseRoot,"human-report.json"),new HumanGameReport(op.Id,exportedHashes[FileNames[slot-1]]));
            File.WriteAllText(Path.Combine(caseRoot,"INSTRUCTIONS.md"),$"# {op.Id}\n\nExperimental {op.Kind}, slot {slot}. Import only the four files in import/ using your own save management process. Do not replace your original backups. Record game version/platform, load success, exact observed effect and values, and whether you performed a native game save in human-report.json. For baseline confirm unchanged domain values. Export that returned four-file bundle separately and run validation-verify-return. The editor never operates the game. Unknown answers remain pending.\n\nBefore/expected values, record selectors and export hashes are in manifest.json. Do not edit this manifest. Human observations are reported evidence, not independently observed gameplay.\n");
        }
        File.WriteAllText(Path.Combine(outputRoot,"GAME_VALIDATION_MATRIX.md"),"# Run validation matrix\n\n| Case | Status |\n|---|---|\n"+string.Join('\n',manifests.Select(p=>$"| {Path.GetFileName(Path.GetDirectoryName(p))} | EXPORTED |"))+"\n\nNo gameplay status is inferred from exports.\n");return manifests;
    }
    private static bool Known(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Trim().ToUpperInvariant() is not ("UNKNOWN" or "PENDING" or "NOT_TESTED" or "N/A");
    public static ValidationResult VerifyReturn(string manifestPath,string reportPath,string returnedDirectory,string newResultPath)
    {
        byte[] manifestBytes=File.ReadAllBytes(manifestPath),reportBytes=File.ReadAllBytes(reportPath);var m=JsonSerializer.Deserialize<ValidationManifest>(manifestBytes,Json)??throw new InvalidDataException("Manifest required.");var report=JsonSerializer.Deserialize<HumanGameReport>(reportBytes,Json)??throw new InvalidDataException("Report required.");
        if(m.Status!="EXPORTED"||m.SelectedSlot is <1 or >3)throw new InvalidDataException("Original EXPORTED manifest required.");
        string status="EXPORTED";var details=new List<string>();Dictionary<string,string>? hashes=null;try{hashes=BundleHashes(returnedDirectory);}catch(Exception e){details.Add("Returned bundle unavailable: "+e.Message);}
        bool identity=report.CaseId==m.CaseId&&string.Equals(report.ExportSha256,m.ExportHashes[FileNames[m.SelectedSlot-1]],StringComparison.OrdinalIgnoreCase);
        if(!identity){status="FAILED";details.Add("Case identity/export hash mismatch.");}
        else if(report.GameLoadSuccess==false||report.EffectSuccess==false||report.GameResaveSuccess==false){status="FAILED";details.Add("Human evidence reports load, effect or native save failure.");}
        else if(!Known(report.GameVersion)||!Known(report.Platform)){details.Add("Game version/platform scope is missing.");}
        else if(report.GameLoadSuccess!=true){details.Add("Affirmative manual game load evidence is pending.");}
        else
        {
            status="GAME_LOAD_OK";
            if(report.EffectSuccess!=true||!Known(report.ActualValues))details.Add("Affirmative effect evidence and actual observed values are pending.");
            else
            {
                status="EFFECT_CONFIRMED";
                if(report.ManualResavePerformed!=true||report.GameResaveSuccess!=true)details.Add("Affirmative native resave performed and success evidence is pending.");
                else
                {
                    status="GAME_RESAVE_OK";
                    try
                    {
                        hashes=BundleHashes(returnedDirectory);
                        if(string.Equals(hashes[FileNames[m.SelectedSlot-1]],m.ExportHashes[FileNames[m.SelectedSlot-1]],StringComparison.OrdinalIgnoreCase))details.Add("Selected returned file is identical to exported bytes; fresh native resave is not demonstrated. Pending.");
                        else
                        {
                            details.AddRange(Compare(m.Expected,Capture(Open(returnedDirectory,m.SelectedSlot))));foreach(var other in m.OtherSlots)details.AddRange(Compare(other.Value,Capture(Open(returnedDirectory,other.Key))).Select(s=>$"Slot {other.Key}: {s}"));
                            status=details.Count==0?"EDITOR_RELOAD_OK":"FAILED";if(details.Count==0)details.Add("Returned bundle reloaded; selected and unrelated domain witnesses match. Unknown bytes and record order are not compared.");
                        }
                    }
                    catch(Exception e){status="FAILED";details.Add("Returned bundle verification failed: "+e.Message);}
                }
            }
        }
        var result=new ValidationResult(m.CaseId,status,Hash(manifestBytes),Hash(reportBytes),report,"HUMAN_REPORTED gameplay; editor independently checked returned bytes and domain witnesses only",hashes,details.ToArray(),status=="EDITOR_RELOAD_OK",DateTimeOffset.UtcNow);WriteNew(newResultPath,result);return result;
    }
    public static int Cli(string[] args)
    {
        try{if(args.Length==5&&args[0]=="validation-prepare"){var paths=Prepare(args[1],int.Parse(args[2]),Read<ValidationPlan>(args[3]),args[4]);Console.WriteLine(JsonSerializer.Serialize(paths,Json));return 0;}if(args.Length==5&&args[0]=="validation-verify-return"){var result=VerifyReturn(args[1],args[2],args[3],args[4]);Console.WriteLine(JsonSerializer.Serialize(result,Json));return result.Status=="FAILED"?1:0;}throw new ArgumentException("validation-prepare <sourceDir> <slot1..3> <plan.json> <freshOutputRoot> | validation-verify-return <manifest.json> <human-report.json> <returnedBundleDir> <newResult.json>");}catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
    }
}


