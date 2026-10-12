using FreshFoundation;
using System.Text;
using System.Text.Json;
using System.Reflection;
internal static class R11Checks
{
    internal static int Run()
    {
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "synthetic-r11-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        int failed = 0;
        var cases = new List<object>();
        void Check(bool ok, string detail)
        {
            if (!ok)
                throw new Exception(detail);
        }
        void Test(string name, Action action)
        {
            try
            {
                action();
                cases.Add(new
                {
                    name,
                    status = "PASS"
                });
            }
            catch (Exception e) { failed++; cases.Add(new { name, status = "FAIL", error = e.Message }); }
        }
        T Call<T>(object instance, string method, params object?[] arguments)
        {
            var target = instance.GetType().GetMethods().SingleOrDefault(m => m.Name == method && m.GetParameters().Length == arguments.Length);
            if (target is null)
                throw new Exception("Required operation is unavailable: " + method);
            try
            {
                return (T)target.Invoke(instance, arguments)!;
            }
            catch (TargetInvocationException e) { throw e.InnerException!; }
        }
        void ActionCall(object instance, string method, params object?[] arguments) => Call<object?>(instance, method, arguments);
        void Reject(Action action, string fragment)
        {
            try
            {
                action();
                throw new Exception("Expected rejection: " + fragment);
            }
            catch (Exception e) when (!e.Message.StartsWith("Expected rejection:")) { Check(e.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase), "Wrong rejection: " + e.Message); }
        }
        (DirectoryEditor group, string source) Fresh(string label, int empties = 8, bool linked = true, string owner = "Seed", string? systemOwner = null, bool malformed = false, bool missingMirror = false, bool mismatchMirror = false, int incompatible = -1)
        {
            string dir = Path.Combine(folder, label);
            Directory.CreateDirectory(dir);
            string source = Path.Combine(dir, "data001Slot.bin");
            File.WriteAllBytes(source, R11SyntheticSamples.Slot(emptyRecords: empties, owner: owner, missingMirror: missingMirror, mismatchMirror: mismatchMirror, incompatibleEmpty: incompatible));
            File.WriteAllBytes(Path.Combine(dir, "data00-1.bin"), R11SyntheticSamples.SystemFile(systemOwner ?? owner, malformed: malformed));
            var group = new DirectoryEditor();
            if (linked)
                group.OpenDirectory(dir);
            group.OpenSlot(source);
            return (group, source);
        }
        foreach (bool compressed in new[] { false, true })
            Test("staged text resize and mixed primitive export flags " + (compressed ? 8 : 0), () =>
            {
                string source = Path.Combine(folder, "text-" + compressed + ".bin");
                byte[] original = R11SyntheticSamples.Slot(compressed);
                File.WriteAllBytes(source, original);
                var w = new EditorWorkspace();
                w.Open(source);
                var texts = w.Document!.Structure.Events.Select((e, i) => (e, i)).Where(x => x.e.Tag == 15 && w.TextAt(x.i) == "Seed" && x.e.Identifier == 45809936).ToArray();
                Check(texts.Length == 1, "Unique synthetic owner");
                int owner = texts[0].i;
                w.Apply(owner, Encoding.Unicode.GetBytes("Longer 新😀"));
                Check(w.TextAt(owner) == "Longer 新😀", "Ordinary text access must reflect staging");
                var d = new DomainEditor(w);
                d.SetMoney(1500);
                string output = Path.Combine(folder, "mixed-" + compressed + ".bin");
                w.Export(output, folder);
                var reload = new EditorWorkspace();
                reload.Open(output);
                Check(reload.TextAt(owner) == "Longer 新😀" && new DomainEditor(reload).Character().Money == 1500, "Resized text and primitive persist");
                Check(File.ReadAllBytes(source).SequenceEqual(original), "Original source preserved");
                w.Undo();
                w.Undo();
                Check(w.TextAt(owner) == "Seed" && w.StagedCount == 0, "Text Undo restores source");
                w.Redo();
                Check(w.TextAt(owner) == "Longer 新😀", "Text Redo restores new text");
                w.Clear();
                Check(w.TextAt(owner) == "Seed", "Clear restores text");
            });
        Test("linked rename exact mirrors grouped history export and shorter reload", () =>
        {
            var (g, source) = Fresh("linked-name");
            byte[] original = File.ReadAllBytes(source);
            ActionCall(g, "SetName", "新😀名字");
            Check(g.Domain!.Character().Name == "新😀名字", "Applied name visible");
            Check(Call<string?>(g, "get_CachedName") == "新😀名字", "System preview renamed");
            Check(g.StagedCount == 3, "Exactly two local mirrors plus one system mirror");
            Check(g.Slot.Document!.Structure.Events.Select((e, i) => (e, i)).Where(x => x.e.Identifier == 4154407670).All(x => g.Slot.TextAt(x.i) == "Seed"), "Unrelated matching card is unchanged");
            g.Undo();
            Check(g.StagedCount == 0 && g.Domain.Character().Name == "Seed", "One Undo reverses every mirror");
            g.Redo();
            Check(g.StagedCount == 3, "One Redo restores every mirror");
            string output = Path.Combine(folder, "linked-output");
            var results = g.ExportCopies(output);
            Check(results.Where(r => r.Label == "Slot 1" || r.Label == "System").All(r => r.Error is null), "Linked files export");
            var loaded = new DirectoryEditor();
            loaded.OpenDirectory(output);
            loaded.OpenSlot(Path.Combine(output, "data001Slot.bin"));
            Check(loaded.Domain!.Character().Name == "新😀名字" && Call<string?>(loaded, "get_CachedName") == "新😀名字", "Export/reload name persists");
            ActionCall(loaded, "SetName", "A");
            string shorter = Path.Combine(folder, "shorter-output");
            loaded.ExportCopies(shorter);
            var final = new EditorWorkspace();
            final.Open(Path.Combine(shorter, "data001Slot.bin"));
            Check(new DomainEditor(final).Character().Name == "A", "Short name persists");
            Check(File.ReadAllBytes(source).SequenceEqual(original), "Original remains unchanged");
        });
        Test("rename policies no-op failures and missing/mismatched mirrors preserve redo", () =>
        {
            var (g, _) = Fresh("name-policy", linked: false);
            ActionCall(g, "SetName", "x");
            g.Undo();
            Check(g.CanRedo, "Redo exists");
            foreach (string bad in new[] { "", " ", "bad\0name", "bad\nname", new string('x', 17), "\uD800" })
            {
                Reject(() => ActionCall(g, "SetName", bad), "name");
                Check(g.CanRedo && g.StagedCount == 0, "Invalid name retained redo and staging");
            }
            ActionCall(g, "SetName", "Seed");
            Check(g.CanRedo, "No-op retained redo");
            ActionCall(g, "SetName", new string('界', 16));
            Check(g.Domain!.Character().Name == new string('界', 16) && g.StagedCount == 2, "Conservative 16 UTF-16 units accepted exactly");
            var mismatch = Fresh("name-mismatch", systemOwner: "Else").group;
            Reject(() => ActionCall(mismatch, "SetName", "New"), "system");
            Check(mismatch.StagedCount == 0, "Mismatched system refuses entire rename");
            var malformed = Fresh("name-malformed", malformed: true).group;
            Reject(() => ActionCall(malformed, "SetName", "New"), "system");
            Check(malformed.StagedCount == 0, "Malformed system refuses entire rename");
            foreach (var bad in new[] { Fresh("name-missing-owner", missingMirror: true).group, Fresh("name-owner-mismatch", mismatchMirror: true).group })
            {
                Reject(() => ActionCall(bad, "SetName", "New"), "owner");
                Check(bad.StagedCount == 0, "Required local owner routes reject");
            }
        });
        Test("original weapon armor talisman template copies staged values decorations and atomic history", () =>
        {
            var (g, _) = Fresh("add-types", linked: false);
            var d = g.Domain!;
            d.ApplyEquipmentDraft(d.ReadEquipmentDraft(6), [0, 18, 0], [new SkillPair(5, 7), new SkillPair(2, 3)]);
            var templates = Call<IReadOnlyList<EquipmentTemplate>>(d, "EquipmentTemplates", (object?)null);
            Check(templates.Count == 7 && templates.Select(t => t.Type).Distinct().Count() == 3, "All original occupied kinds are selectable");
            foreach (int index in new[] { 0, 1, 6 })
            {
                var template = Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", index);
                var plan = Call<EquipmentAddPlan>(d, "PreviewEquipmentAdd", template, 2, false);
                Check(plan.Pieces.Count == 2 && plan.Pieces.Select(p => p.DestinationIndex).Distinct().Count() == 2, "Quantity reserves distinct empties");
                int staged = g.StagedCount;
                g.ApplyDomain(x => ActionCall(x, "ApplyEquipmentAdd", plan));
                Check(plan.Pieces.All(p => d.Equipment().Single(e => e.Index == p.DestinationIndex).Type == template.Type && d.DecorationIds(p.DestinationIndex).All(id => id == 0)), "Copies appear and default decorations clear");
                g.Undo();
                Check(g.StagedCount == staged, "One Undo restores destinations");
                g.Redo();
                Check(plan.Pieces.All(p => d.Equipment().Single(e => e.Index == p.DestinationIndex).Id == template.Id), "One Redo restores all copies");
            }
            string exported = Path.Combine(folder, "equipment-copies.bin");
            g.Slot.Export(exported, folder);
            var w = new EditorWorkspace();
            w.Open(exported);
            Check(new DomainEditor(w).Equipment().Count(e => e.Type is 1 or 2 or 3) == 13, "Every copy survives export/reload");
            Reject(() => d.ReadEquipmentDraft(7), "unchanged existing");
            Check(d.Talisman(6).Pairs[0] == new SkillPair(5, 7), "Source staged pairs retained");
        });
        Test("template preview stale occupied capacity and recursive metadata fail atomically", () =>
        {
            var (g, _) = Fresh("add-atomic", empties: 2, linked: false);
            var d = g.Domain!;
            var template = Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", 0);
            var plan = Call<EquipmentAddPlan>(d, "PreviewEquipmentAdd", template, 1, true);
            d.ApplyEquipmentDraft(d.ReadEquipmentDraft(0), [0, 12, 0], null);
            int staged = g.StagedCount;
            Reject(() => ActionCall(d, "ApplyEquipmentAdd", plan), "changed");
            Check(g.StagedCount == staged, "Stale source fails without mutation");
            template = Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", 0);
            plan = Call<EquipmentAddPlan>(d, "PreviewEquipmentAdd", template, 2, true);
            ActionCall(d, "ApplyEquipmentAdd", plan);
            Reject(() => Call<EquipmentAddPlan>(d, "PreviewEquipmentAdd", template, 1, false), "capacity");
            Check(d.Equipment().Count(e => e.Type == 0) == 0, "No empty capacity");
            var altered = Fresh("add-shape", empties: 1, linked: false, incompatible: 7).group;
            var td = altered.Domain!;
            var t = Call<EquipmentTemplate>(td, "ReadEquipmentTemplate", 0);
            Reject(() => Call<EquipmentAddPlan>(td, "PreviewEquipmentAdd", t, 1, false), "metadata");
            Check(altered.StagedCount == 0, "Nested array count mismatch blocks whole copy");
        });
        Test("all seven Build templates without catalog one action portable schema and reload", () =>
        {
            var (g, _) = Fresh("build-seven", linked: false);
            var d = g.Domain!;
            var planner = new BuildPlanner(new GameCatalog([]));
            foreach (var pos in Enum.GetValues<BuildPosition>())
                ActionCall(planner, "SelectTemplate", pos, Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", (int)pos));
            string json = planner.ToJson();
            Check(JsonDocument.Parse(json).RootElement.GetProperty("SchemaVersion").GetInt32() == 2, "Template portable plan has version 2");
            Check(!json.Contains("Seed") && !json.Contains(folder), "Portable plan has no name/path");
            var reloadPlan = new BuildPlanner(new GameCatalog([]));
            reloadPlan.LoadJson(json);
            var plan = Call<EquipmentAddPlan>(reloadPlan, "PreviewAddToBox", d);
            Check(plan.Pieces.Count == 7, "Every non-None position included");
            g.ApplyDomain(x => ActionCall(reloadPlan, "AddToBox", x));
            Check(d.Equipment().Count(e => e.Type is 1 or 2 or 3) == 14, "Complete set staged");
            g.Undo();
            Check(g.StagedCount == 0, "One Undo removes whole Build");
            g.Redo();
            Check(d.Equipment().Count(e => e.Type is 1 or 2 or 3) == 14, "One Redo restores whole Build");
            string output = Path.Combine(folder, "build-seven.bin");
            g.Slot.Export(output, folder);
            var w = new EditorWorkspace();
            w.Open(output);
            Check(new DomainEditor(w).Equipment().Count(e => e.Type is 1 or 2 or 3) == 14, "Whole Build persists");
        });
        Test("six piece None legacy plan and every unresolved request blocks whole Build", () =>
        {
            var (g, _) = Fresh("build-rejections", empties: 6, linked: false);
            var d = g.Domain!;
            var planner = new BuildPlanner(new GameCatalog([]));
            foreach (var pos in Enum.GetValues<BuildPosition>().Where(p => p != BuildPosition.Talisman))
                ActionCall(planner, "SelectTemplate", pos, Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", (int)pos));
            var plan = Call<EquipmentAddPlan>(planner, "PreviewAddToBox", d);
            Check(plan.Pieces.Count == 6, "Six positions with None talisman");
            ActionCall(planner, "SelectTemplate", BuildPosition.Talisman, Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", 6));
            Reject(() => ActionCall(planner, "AddToBox", d), "capacity");
            Check(g.StagedCount == 0, "Seven pieces cannot partially fit six empties");
            planner.Confirm(new(BuildPosition.Talisman, null, [0, 0, 0], new([new(2, 3), new(0, 0)], [4, 0, 0])));
            Reject(() => ActionCall(planner, "AddToBox", d), "virtual");
            Check(g.StagedCount == 0, "Virtual piece is never omitted");
            planner.Confirm(new(BuildPosition.Talisman, null, [0, 0, 0]));
            planner.Confirm(new(BuildPosition.Head, 9999, [0, 0, 0]));
            Reject(() => ActionCall(planner, "AddToBox", d), "original");
            Check(g.StagedCount == 0, "Fresh ID is never fabricated or omitted");
            string legacy = "{\"Schema\":\"rise-offline-build\",\"SchemaVersion\":1,\"Title\":\"Legacy\",\"Pieces\":[{\"Position\":0,\"Id\":101,\"Decorations\":[0,0,0]}]}";
            planner.LoadJson(legacy);
            Check(planner.Piece(BuildPosition.Weapon)?.Id == 101, "Schema 1 remains readable");
            Reject(() => ActionCall(planner, "AddToBox", d), "choose");
        });
        Test("added talisman displays copied pairs without admitting existing-template writers", () =>
        {
            var (g, _) = Fresh("copied-talisman-read", linked: false);
            var d = g.Domain!;
            var template = Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", 6);
            var plan = Call<EquipmentAddPlan>(d, "PreviewEquipmentAdd", template, 1, false);
            ActionCall(d, "ApplyEquipmentAdd", plan);
            int added = plan.Pieces[0].DestinationIndex;
            Check(d.Talisman(added).Pairs.SequenceEqual(new[] { new SkillPair(2, 3), new SkillPair(5, 7) }), "Copied pairs are visible before export/reload");
            Reject(() => d.ReadEquipmentDraft(added), "unchanged existing");
            Reject(() => Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", added), "original occupied");
        });
        foreach (bool compressed in new[] { false, true })
            Test("strict text alignment padding no-op and opaque preservation flags " + (compressed ? 8 : 0), () =>
            {
                var b = new SyntheticR2();
                b.Root(0xAB00, 4, () =>
                {
                    b.Field(0xAB01, 15, () => b.Text("abc"));
                    b.Field(0xAB02, 15, () => b.Text("KEEP😀"));
                    b.Field(0xAB03, 16, () => b.Primitive(12));
                    b.Field(0xAB04, -1, () => b.Array(15, 32, 2, 0, () => { b.Text("x"); b.Text("yz"); }));
                });
                byte[] original = b.File(compressed);
                var document = Document.Inspect(original);
                int target = document.Structure.Events.Select((e, i) => (e, i)).Single(x => x.e.Identifier == 0xAB01).i;
                int opaque = document.Structure.Events.Select((e, i) => (e, i)).Single(x => x.e.Identifier == 0xAB03).i;
                byte[] opaqueBytes = document.Structure.CopyPayload().AsSpan(document.Structure.Events[opaque].DataCoordinate!.Value - 12, 12).ToArray();
                var noOp = ExperimentalStructuralSerializer.Replace(document, [], [new(target, Encoding.Unicode.GetBytes("abc"), Encoding.Unicode.GetBytes("abc"))]);
                Check(noOp.CopyFileBytes().SequenceEqual(original), "Unchanged text is byte-identical source");
                foreach (string replacement in new[] { "a", "ab", "abcd", "abcde", "😀😀" })
                {
                    var result = ExperimentalStructuralSerializer.Replace(document, [], [new(target, Encoding.Unicode.GetBytes("abc"), Encoding.Unicode.GetBytes(replacement))]).InspectedDocument;
                    var payload = result.Structure.CopyPayload();
                    var e = result.Structure.Events[target];
                    Check(Encoding.Unicode.GetString(payload, ((e.Coordinate + 3) & ~3) - 12 + 4, replacement.Length * 2) == replacement, "Strict accepted text persists");
                    Check(result.Structure.Events.Count == document.Structure.Events.Count && result.Structure.Events.Single(e => e.Identifier == 0xAB04).Count == 2, "Root/array shape preserved across resizing");
                    Check(payload.AsSpan(result.Structure.Events[opaque].DataCoordinate!.Value - 12, 12).SequenceEqual(opaqueBytes), "Untouched opaque primitive preserved");
                    if (replacement.Length % 2 == 1)
                        Check(payload.AsSpan(e.EndCoordinate - 12, 2).SequenceEqual(new byte[] { 0xA7, 0xA7 }), "Common nonzero padding is retained");
                    if (compressed)
                        Check(result.SerializeUnchanged().AsSpan(12, 4).SequenceEqual(original.AsSpan(12, 4)), "Opaque compressed header word preserved");
                }
                Reject(() => ExperimentalStructuralSerializer.Replace(document, [], [new(target, Encoding.Unicode.GetBytes("abd"), Encoding.Unicode.GetBytes("a"))]), "expected original text");
                Reject(() => ExperimentalStructuralSerializer.Replace(document, [], [new(target, Encoding.Unicode.GetBytes("abc"), new byte[] { 0 })]), "UTF-16");
                Reject(() => ExperimentalStructuralSerializer.Replace(document, [], [new(target, Encoding.Unicode.GetBytes("abc"), new byte[] { 0, 0xD8 })]), "UTF-16");
            });
        Test("template original identity occupied destination and duplicate targets are rejected before mutation", () =>
        {
            var (g, _) = Fresh("strict-template-targets", linked: false);
            var d = g.Domain!;
            var template = Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", 0);
            var plan = Call<EquipmentAddPlan>(d, "PreviewEquipmentAdd", template, 1, false);
            var occupied = plan with
            {
                Pieces = new[] { plan.Pieces[0] with { DestinationIndex = 0 } }
            };
            Reject(() => ActionCall(d, "ApplyEquipmentAdd", occupied), "occupied");
            Check(g.StagedCount == 0, "Occupied target did not mutate staging");
            var duplicate = plan with
            {
                Pieces = new[] { plan.Pieces[0], plan.Pieces[0] }
            };
            Reject(() => ActionCall(d, "ApplyEquipmentAdd", duplicate), "duplicated");
            Check(g.StagedCount == 0, "Duplicate target did not partially stage");
            int id = g.Slot.Document!.Structure.Events.Select((e, i) => (e, i)).First(x => x.e.Identifier == 1854292354 && g.Slot.PrimitiveBytes(x.i)!.SequenceEqual(BitConverter.GetBytes(101u))).i;
            g.Slot.Apply(id, BitConverter.GetBytes(9999u));
            int staged = g.StagedCount;
            Reject(() => Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", 0), "type/ID changed");
            Reject(() => ActionCall(d, "ApplyEquipmentAdd", plan), "type/ID changed");
            Check(g.StagedCount == staged, "Changed original identity did not mutate destination");
        });
        Test("six-piece success stale loaded binding unsupported jewel and None jewel never silently omit", () =>
        {
            var (g, _) = Fresh("build-complete-edges", empties: 7, linked: false);
            var d = g.Domain!;
            var planner = new BuildPlanner(new GameCatalog([]));
            foreach (var pos in Enum.GetValues<BuildPosition>().Where(p => p != BuildPosition.Talisman))
                ActionCall(planner, "SelectTemplate", pos, Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", (int)pos));
            g.ApplyDomain(x => ActionCall(planner, "AddToBox", x));
            Check(d.Equipment().Count(e => e.Type is 1 or 2 or 3) == 13, "All six selected pieces staged");
            g.Undo();
            Check(g.CanRedo && g.StagedCount == 0, "Six-piece group undone");
            var head = planner.Piece(BuildPosition.Head)!;
            planner.Confirm(head with
            {
                Decorations = [9999, 0, 0]
            });
            Reject(() => ActionCall(planner, "AddToBox", d), "unsupported planned decoration");
            Check(g.CanRedo && g.StagedCount == 0, "Invalid jewels retain redo");
            planner.Confirm(head);
            planner.Confirm(new(BuildPosition.Talisman, null, [11, 0, 0]));
            Reject(() => ActionCall(planner, "AddToBox", d), "None");
            Check(g.CanRedo && g.StagedCount == 0, "None jewels cannot be omitted");
            planner.Confirm(new(BuildPosition.Talisman, null, [0, 0, 0]));
            string json = planner.ToJson().Replace(g.Slot.Document!.Digest, new string('A', 64));
            var stale = new BuildPlanner(new GameCatalog([]));
            stale.LoadJson(json);
            Reject(() => ActionCall(stale, "AddToBox", d), "stale");
            Check(g.CanRedo && g.StagedCount == 0, "Stale binding cannot partially stage");
            ActionCall(stale, "SelectTemplate", BuildPosition.Weapon, Call<EquipmentTemplate>(d, "ReadEquipmentTemplate", 0));
            Check(stale.Piece(BuildPosition.Weapon)!.Template!.SourceDigest == g.Slot.Document.Digest, "Explicit current selection repairs a binding hint");
        });
        Test("malformed original UTF-16 is rejected by the reader before name association", () =>
        {
            byte[] bytes = R11SyntheticSamples.Slot(compressed: false);
            var inspected = Document.Inspect(bytes);
            var primary = inspected.Structure.Events.Single(e => e.Identifier == 45809936);
            var mirror = inspected.Structure.Events.Single(e => e.Identifier == 264986686);
            int primaryOffset = ((primary.Coordinate + 3) & ~3) + 4;
            int mirrorOffset = ((mirror.Coordinate + 3) & ~3) + 4;
            bytes[primaryOffset] = 0;
            bytes[primaryOffset + 1] = 0xD8;
            bytes[mirrorOffset] = 1;
            bytes[mirrorOffset + 1] = 0xD8;
            SyntheticR2.Recheck(bytes);
            Reject(() => Document.Inspect(bytes), "Unrecognized UTF-16");
        });
        File.WriteAllText("test-results-r11.json", JsonSerializer.Serialize(new
        {
            atUtc = DateTimeOffset.UtcNow,
            scope = "New synthetic R11 operations only; native game acceptance NOT_TESTED",
            failed,
            cases
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"R11 cases={cases.Count}; failures={failed}");
        foreach (var c in cases)
            Console.WriteLine(JsonSerializer.Serialize(c));
        return failed == 0 ? 0 : 1;
    }
}
