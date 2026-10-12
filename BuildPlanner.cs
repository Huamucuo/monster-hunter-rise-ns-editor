using System.Text.Json;
namespace FreshFoundation;

public enum BuildPosition
{
    Weapon, Head, Chest, Arms, Waist, Legs, Talisman
}
public sealed record VirtualTalisman(SkillPair[] Pairs, int[] SlotLevels);
public sealed record PlannedPiece(BuildPosition Position, uint? Id, uint[] Decorations, VirtualTalisman? Talisman = null, OwnedTemplateSelection? Template = null);
public sealed record PortableBuild(string Schema, int SchemaVersion, string Title, PlannedPiece[] Pieces);
public sealed record PlannedSkill(uint Id, long Total, uint? DeclaredMaximum, long Effective, long Excess);
public sealed record BuildSummary(int? Defense, CatalogResistances? Resistances, int? Attack, int? Affinity, IReadOnlyList<PlannedSkill> Skills, bool Partial, IReadOnlyList<string> Issues);
public sealed class BuildPlanner
{
    private readonly GameCatalog catalog;
    private Dictionary<BuildPosition, PlannedPiece> pieces = [];
    public bool ShowCandidates
    {
        get; set;
    }
    public string Title { get; set; } = "Untitled plan";
    public BuildPlanner(GameCatalog catalog) => this.catalog = catalog;
    public static string? ArmorCategory(BuildPosition position) => position switch { BuildPosition.Head => "Head", BuildPosition.Chest => "Chest", BuildPosition.Arms => "Arm", BuildPosition.Waist => "Waist", BuildPosition.Legs => "Leg", _ => null };
    private static PlannedPiece Clone(PlannedPiece p) => p with { Decorations = p.Decorations.ToArray(), Talisman = p.Talisman is null ? null : new(p.Talisman.Pairs.ToArray(), p.Talisman.SlotLevels.ToArray()) };
    public PlannedPiece? Piece(BuildPosition position) => pieces.TryGetValue(position, out var p) ? Clone(p) : null;
    public IReadOnlyList<GameCatalogEntry> Choices(BuildPosition position, string query = "", string? category = null) => !ShowCandidates || position == BuildPosition.Talisman ? [] : catalog.Search(query, position == BuildPosition.Weapon ? GameCatalogKind.Weapon : GameCatalogKind.Armor, position == BuildPosition.Weapon ? category : ArmorCategory(position));
    public IReadOnlyList<GameCatalogEntry> Skills(string query = "") => !ShowCandidates ? [] : catalog.Search(query, GameCatalogKind.Skill).Where(e => e.DeclaredMaximumSkillLevel is > 0 and <= 255).ToArray();
    public GameCatalogEntry? Reference(PlannedPiece p) => !ShowCandidates || p.Id is null || p.Position == BuildPosition.Talisman ? null : catalog.Find(p.Position == BuildPosition.Weapon ? GameCatalogKind.Weapon : GameCatalogKind.Armor, p.Id.Value);
    public GameCatalogEntry? Decoration(uint id) => ShowCandidates ? catalog.Find(GameCatalogKind.Decoration, id) : null;
    public GameCatalogEntry? Skill(uint id) => ShowCandidates ? catalog.Find(GameCatalogKind.Skill, id) : null;
    public IReadOnlyList<GameCatalogEntry> Decorations(string query = "", int? slot = null)
    {
        if (!ShowCandidates || slot is < 1 or > 4)
            return [];
        return catalog.Entries.Where(e => e.Kind == GameCatalogKind.Decoration && (slot is null || e.RequiredSlotLevel is >= 1 and <= 4 && e.RequiredSlotLevel <= slot) && ($"{e.Id} 0x{e.Id:X8}".Contains(query, StringComparison.OrdinalIgnoreCase) || e.Names.Values.Any(n => n.Contains(query, StringComparison.OrdinalIgnoreCase)) || (e.SkillRelations ?? []).Any(r => catalog.Find(GameCatalogKind.Skill, r.SkillId)?.Names.Values.Any(n => n.Contains(query, StringComparison.OrdinalIgnoreCase)) == true))).ToArray();
    }
    public int[]? Slots(PlannedPiece p) => p.Template is not null ? null : p.Position == BuildPosition.Talisman ? p.Talisman?.SlotLevels.ToArray() : Reference(p)?.EquipmentStats?.BaseSlotLevels?.ToArray();
    private static void Validate(PlannedPiece p)
    {
        if (!Enum.IsDefined(p.Position) || p.Decorations is null || p.Decorations.Length != 3)
            throw new InvalidDataException("Plan piece needs a recognized position and three decoration IDs.");
        if (p.Talisman is not null && (p.Position != BuildPosition.Talisman || p.Id is not null || p.Talisman.Pairs is null || p.Talisman.Pairs.Length != 2 || p.Talisman.Pairs.Any(x => x is null || x.Level > 255 || x.Id == 0 && x.Level != 0 || x.Id != 0 && x.Level == 0) || p.Talisman.SlotLevels is null || p.Talisman.SlotLevels.Length != 3 || p.Talisman.SlotLevels.Any(x => x < 0 || x > 4)))
            throw new InvalidDataException("Invalid virtual talisman pairs or plan slot levels.");
        if (p.Template is not null)
        {
            var template = p.Template;
            uint expectedType = p.Position == BuildPosition.Weapon ? 1u : p.Position == BuildPosition.Talisman ? 3u : 2u;
            if (p.Talisman is not null || p.Id != template.Id || template.Type != expectedType || template.RecordIndex < 0 ||
               !Digest(template.SourceDigest) || !Digest(template.StateDigest))
                throw new InvalidDataException("Owned template selection has an invalid kind, identity or binding. Virtual slots cannot be combined with an owned template.");
        }
        else if (p.Position == BuildPosition.Talisman && p.Id is not null)
            throw new InvalidDataException("Talisman position needs an original owned template or a virtual planning configuration.");
    }
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    public void Confirm(PlannedPiece draft)
    {
        Validate(draft);
        pieces[draft.Position] = Clone(draft);
    }
    public void Clear()
    {
        pieces.Clear();
        Title = "Untitled plan";
    }
    public string ToJson()
    {
        if (Title is null || Title.Length > 200)
            throw new InvalidDataException("Plan title must be at most 200 characters.");
        return JsonSerializer.Serialize(new PortableBuild("rise-offline-build", pieces.Values.Any(p => p.Template is not null) ? 2 : 1, Title, pieces.Values.OrderBy(p => p.Position).Select(Clone).ToArray()), new JsonSerializerOptions { WriteIndented = true });
    }
    public void LoadJson(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 1024 * 1024)
            throw new InvalidDataException("Plan exceeds 1 MiB.");
        var plan = JsonSerializer.Deserialize<PortableBuild>(json) ?? throw new InvalidDataException("Plan is empty.");
        if (plan.Schema != "rise-offline-build" || plan.SchemaVersion is not (1 or 2) || plan.Title is null || plan.Title.Length > 200 || plan.Pieces is null || plan.Pieces.Length > 7)
            throw new InvalidDataException("Unsupported plan schema, version or size.");
        var pending = new Dictionary<BuildPosition, PlannedPiece>();
        foreach (var p in plan.Pieces)
        {
            if (p is null)
                throw new InvalidDataException("Null plan piece.");
            if (plan.SchemaVersion == 1 && p.Template is not null)
                throw new InvalidDataException("Owned templates require portable plan schema 2.");
            Validate(p);
            if (!pending.TryAdd(p.Position, Clone(p)))
                throw new InvalidDataException("Duplicate plan position.");
        }
        pieces = pending;
        Title = plan.Title;
    }
    public void Load(string explicitlySelectedFile)
    {
        if (new FileInfo(explicitlySelectedFile).Length > 1024 * 1024)
            throw new InvalidDataException("Plan exceeds 1 MiB.");
        LoadJson(File.ReadAllText(explicitlySelectedFile));
    }
    public void SaveNew(string explicitlySelectedFile)
    {
        string json = ToJson();
        using var stream = new FileStream(explicitlySelectedFile, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream);
        writer.Write(json);
    }
    public BuildSummary PreviewPiece(PlannedPiece draft)
    {
        var preview = new BuildPlanner(catalog) { ShowCandidates = ShowCandidates };
        preview.Confirm(draft);
        return preview.Summarize();
    }

    public IReadOnlyList<EquipmentTemplate> TemplateChoices(DomainEditor domain, BuildPosition position, uint? id = null)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (!Enum.IsDefined(position))
            throw new ArgumentOutOfRangeException(nameof(position));
        uint type = position == BuildPosition.Weapon ? 1u : position == BuildPosition.Talisman ? 3u : 2u;
        return domain.EquipmentTemplates(type).Where(t => id is null || t.Id == id).ToArray();
    }

    public void SelectTemplate(BuildPosition position, EquipmentTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        Confirm(new(position, template.Id, [0, 0, 0], null,
            new(template.RecordIndex, template.Type, template.Id, template.SourceDigest, template.StateDigest)));
    }

    private EquipmentTemplate ResolveTemplate(DomainEditor domain, PlannedPiece piece)
    {
        if (piece.Talisman is not null)
            throw new InvalidOperationException($"{piece.Position}: virtual talisman output is unavailable; virtual socket levels have no admitted save mapping.");
        if (piece.Template is null)
        {
            var choices = TemplateChoices(domain, piece.Position, piece.Id);
            if (choices.Count == 0)
                throw new InvalidOperationException($"{piece.Position}: no original same-kind/source-ID template exists for ID {piece.Id}; fresh identities are unsupported.");
            throw new InvalidOperationException($"{piece.Position}: choose an original owned template explicitly for ID {piece.Id}; {choices.Count} matching configuration(s) are available.");
        }
        var binding = piece.Template;
        EquipmentTemplate current;
        try
        {
            current = domain.ReadEquipmentTemplate(binding.RecordIndex);
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or ArgumentException)
        {
            throw new InvalidOperationException($"{piece.Position}: template binding is unresolved; choose a current original template. " + e.Message, e);
        }
        if (current.Type != binding.Type || current.Id != binding.Id || current.SourceDigest != binding.SourceDigest || current.StateDigest != binding.StateDigest)
            throw new InvalidOperationException($"{piece.Position}: template binding is stale or mismatched; choose a current original template explicitly.");
        return current;
    }

    public EquipmentAddPlan PreviewAddToBox(DomainEditor domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        var requests = new List<EquipmentCopyRequest>();
        foreach (var piece in pieces.Values.OrderBy(p => p.Position))
        {
            Validate(piece);
            bool none = piece.Id is null && piece.Talisman is null && piece.Template is null;
            if (none)
            {
                if (piece.Decorations.Any(id => id != 0))
                    throw new InvalidOperationException($"{piece.Position}: planned decorations require a resolved piece; None cannot carry jewels.");
                continue;
            }
            var template = ResolveTemplate(domain, piece);
            for (int at = 0; at < 3; at++)
            {
                if (piece.Decorations[at] != 0 && piece.Decorations[at] != template.OriginalDecorations[at])
                    throw new InvalidOperationException($"{piece.Position}: unsupported planned decoration at position {at + 1}; choose zero or this template's original same-position ID.");
            }
            requests.Add(new(piece.Position, template, piece.Decorations));
        }
        return domain.PreviewEquipmentCopies(requests);
    }

    public EquipmentAddPlan AddToBox(DomainEditor domain)
    {
        var plan = PreviewAddToBox(domain);
        domain.ApplyEquipmentAdd(plan);
        return plan;
    }
    public BuildSummary Summarize()
    {
        var issues = new List<string>();
        var totals = new Dictionary<uint, long>();
        int defense = 0;
        bool defenseKnown = true;
        var resist = new int[5];
        var resistKnown = new[] { true, true, true, true, true };
        int? attack = null, affinity = null;
        void AddRelations(CatalogSkillRelation[]? relations, string label)
        {
            if (relations is null)
            {
                issues.Add(label + ": skill relations unknown");
                return;
            }
            foreach (var relation in relations)
                totals[relation.SkillId] = checked(totals.GetValueOrDefault(relation.SkillId) + relation.Level);
        }
        foreach (var p in pieces.Values)
        {
            bool selected = p.Id is not null || p.Talisman is not null;
            if (!selected)
            {
                if (p.Decorations.Any(id => id != 0))
                    issues.Add(p.Position + ": decorations without a piece excluded");
                continue;
            }
            if (!ShowCandidates)
            {
                issues.Add(p.Position + ": candidate metadata hidden; summary unavailable");
                defenseKnown = false;
                Array.Fill(resistKnown, false);
                continue;
            }
            int[]? slots = null;
            if (p.Position == BuildPosition.Talisman && p.Template is not null)
            {
                issues.Add("Talisman: owned template pairs are retained in the source; actual socket capacities are unknown.");
            }
            else if (p.Position == BuildPosition.Talisman)
            {
                slots = p.Talisman!.SlotLevels;
                foreach (var pair in p.Talisman.Pairs)
                {
                    if (pair.Id == 0 && pair.Level == 0)
                        continue;
                    var maximum = Skill(pair.Id)?.DeclaredMaximumSkillLevel;
                    if (maximum is null || pair.Level > maximum)
                    {
                        issues.Add("Talisman: unresolved/over-limit pair excluded");
                        continue;
                    }
                    totals[pair.Id] = totals.GetValueOrDefault(pair.Id) + pair.Level;
                }
            }
            else
            {
                var entry = Reference(p);
                if (entry is null || p.Position != BuildPosition.Weapon && entry.Category != ArmorCategory(p.Position))
                {
                    issues.Add(p.Position + ": missing or wrong-part catalog ID " + p.Id);
                    defenseKnown = false;
                    if (p.Position != BuildPosition.Weapon)
                        Array.Fill(resistKnown, false);
                    continue;
                }
                var stats = entry.EquipmentStats;
                slots = stats?.BaseSlotLevels;
                if (p.Position == BuildPosition.Weapon)
                {
                    attack = stats?.BaseAttack;
                    affinity = stats?.AffinityPercent;
                    if (attack is null || affinity is null)
                        issues.Add("Weapon: attack/affinity unknown");
                    if (stats?.DefenseBonus is int bonus)
                        defense = checked(defense + bonus);
                    else
                        defenseKnown = false;
                }
                else
                {
                    if (stats?.BaseDefense is int value)
                        defense = checked(defense + value);
                    else
                        defenseKnown = false;
                    int?[] values = [stats?.Resistances?.Fire, stats?.Resistances?.Water, stats?.Resistances?.Thunder, stats?.Resistances?.Ice, stats?.Resistances?.Dragon];
                    for (int i = 0; i < 5; i++)
                        if (values[i] is int n)
                            resist[i] = checked(resist[i] + n);
                        else
                            resistKnown[i] = false;
                    AddRelations(entry.SkillRelations, p.Position.ToString());
                }
            }
            if (slots is null || slots.Length != 3 || slots.Any(s => s < 0 || s > 4))
                issues.Add(p.Position + ": base slots unknown");
            for (int i = 0; i < 3; i++)
            {
                uint id = p.Decorations[i];
                if (id == 0)
                    continue;
                var deco = catalog.Find(GameCatalogKind.Decoration, id);
                if (slots is null || slots.Length != 3 || slots[i] is < 1 or > 4 || deco?.RequiredSlotLevel is not uint need || need is < 1 or > 4 || need > slots[i])
                {
                    issues.Add(p.Position + $": decoration {id} at {i + 1} unresolved or does not fit; excluded");
                    continue;
                }
                AddRelations(deco.SkillRelations, p.Position + " decoration " + id);
            }
        }
        if (!defenseKnown)
            issues.Add("Defense total partial/unknown");
        if (resistKnown.Any(x => !x))
            issues.Add("Resistance totals partial/unknown");
        var skills = totals.OrderBy(p => p.Key).Select(p => { uint? max = Skill(p.Key)?.DeclaredMaximumSkillLevel; if (max is null) issues.Add("Skill " + p.Key + ": maximum unknown"); return new PlannedSkill(p.Key, p.Value, max, max is uint m ? Math.Min(p.Value, m) : p.Value, max is uint n ? Math.Max(0, p.Value - n) : 0); }).ToArray();
        int? R(int i) => resistKnown[i] ? resist[i] : null;
        return new(defenseKnown ? defense : null, new(R(0), R(1), R(2), R(3), R(4)), attack, affinity, skills, issues.Count > 0, issues);
    }
}
