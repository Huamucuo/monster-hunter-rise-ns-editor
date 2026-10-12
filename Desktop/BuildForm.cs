using FreshFoundation;
namespace RiseWorkshopDesktop;

internal static class CatalogReferenceView
{
    internal static string Describe(GameCatalogEntry? entry, bool enabled, Func<uint, string>? skillName = null)
    {
        string L(string en, string zh) => EquipmentDraftForm.L(en, zh);
        string V(object? value) => value?.ToString() ?? L("Unknown", "未知");
        if (entry is null)
            return L("No matching catalog reference. Numeric identity is retained.", "未找到目录参考，保留数字身份。");
        string identity = $"{entry.Kind} · ID {entry.Id} / 0x{entry.Id:X8}\r\nUNVERIFIED_MAPPING\r\n";
        string source = $"\r\n{L("Source", "来源")}: {entry.Origin?.Source}\r\n{L("License", "许可")}: {entry.Origin?.License}\r\n{L("Edition", "版本声明")}: {entry.Origin?.Edition}";
        if (!enabled)
            return identity + L("Name and reference parameters hidden. Enable unverified candidates to inspect.", "名称和参考参数已隐藏。启用未验证候选后查看。") + source;
        var s = entry.EquipmentStats;
        string text = identity + entry.Name(UiLanguage.Current) + "\r\n" + L("External reference only; no upgrades, augmentation or other game effects included.", "仅外部参考；不含升级、强化及其他游戏效果。") + "\r\n" + $"{L("Category", "类别")}: {V(entry.Category)} · {L("Rarity", "稀有度")}: {V(entry.Rarity)}\r\n";
        if (entry.Kind == GameCatalogKind.Weapon)
            text += $"{L("Base attack / affinity / defense bonus", "基础攻击 / 会心 / 防御加成")}: {V(s?.BaseAttack)} / {V(s?.AffinityPercent)} / {V(s?.DefenseBonus)}\r\n{L("Elements / status", "属性 / 异常")}: {(s?.Elements is null ? V(null) : string.Join(", ", s.Elements.Select(e => $"{e.Type} {e.Value}")))}\r\n";
        if (entry.Kind == GameCatalogKind.Armor)
            text += $"{L("Base defense", "基础防御")}: {V(s?.BaseDefense)}\r\n{L("Fire / Water / Thunder / Ice / Dragon", "火 / 水 / 雷 / 冰 / 龙")}: {V(s?.Resistances?.Fire)} / {V(s?.Resistances?.Water)} / {V(s?.Resistances?.Thunder)} / {V(s?.Resistances?.Ice)} / {V(s?.Resistances?.Dragon)}\r\n";
        if (entry.Kind is GameCatalogKind.Weapon or GameCatalogKind.Armor)
            text += $"{L("Catalog base slots (not actual augmented capacities)", "目录基础槽（非实际强化后容量）")}: {(s?.BaseSlotLevels is null ? V(null) : string.Join(" / ", s.BaseSlotLevels))}\r\n";
        text += $"{L("Required slot / declared skill maximum", "需求槽 / 声明技能上限")}: {V(entry.RequiredSlotLevel)} / {V(entry.DeclaredMaximumSkillLevel)}\r\n{L("Skill contributions (ID / level)", "技能贡献（ID / 等级）")}: {(entry.SkillRelations is null ? V(null) : string.Join(", ", entry.SkillRelations.Select(r => $"{skillName?.Invoke(r.SkillId) ?? "ID"} · {r.SkillId} / {r.Level}")))}";
        return text + source;
    }
    internal static void Show(IWin32Window owner, string observed, GameCatalogEntry? entry, bool enabled)
    {
        using var form = new Form() { Text = EquipmentDraftForm.L("Equipment information", "装备信息"), Size = new(900, 620), MinimumSize = new(700, 450), StartPosition = FormStartPosition.CenterParent, Font = new("Microsoft YaHei UI", 9.5f) };
        form.Controls.Add(new TextBox() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = observed + "\r\n\r\n" + Describe(entry, enabled) });
        form.ShowDialog(owner);
    }
}
internal sealed class BuildForm : Form
{
    private readonly BuildPlanner model;
    private readonly DirectoryEditor? editor;
    private readonly Action? staged;
    private readonly Label addStatus = new() { Name = "BuildStageStatus", AutoSize = true };
    private readonly ListBox positions = new() { Dock = DockStyle.Fill };
    private readonly TextBox title = new() { Width = 250 }, summary = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical }, details = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox candidates = new() { AutoSize = true }; private bool dirty;
    private static string L(string en, string zh) => EquipmentDraftForm.L(en, zh);
    internal BuildForm(GameCatalog catalog, DirectoryEditor? editor = null, Action? staged = null)
    {
        this.editor = editor;
        this.staged = staged;
        model = new(catalog);
        Text = L("Build — offline plan", "配装 — 离线规划");
        Size = new(1080, 700);
        MinimumSize = new(900, 600);
        Font = new("Microsoft YaHei UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new(12) };
        root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize));
        Controls.Add(root);
        var tools = new FlowLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true };
        candidates.Text = L("Show unverified name candidates", "显示未验证名称候选");
        tools.Controls.AddRange([B(L("New / clear", "新建 / 清空"), () => { if (Discard()) { model.Clear(); dirty = false; RefreshPlan(); } }), B(L("Open plan…", "打开配装…"), Open), B(L("Save plan as new…", "另存新配装…"), Save), title, candidates]);
        root.Controls.Add(tools, 0, 0);
        var introduction = new Label() { AutoSize = true, Text = L("Seven planning positions accept original owned templates, including a real template talisman or None, without a catalog. Add set stages copies in the equipment box and does not equip them. Virtual talismans remain offline only; candidate parameters are unverified.", "七个规划位置无需目录即可选择原始已有模板，包括实际模板护石或无。添加整套将副本暂存到装备箱，不会自动穿戴。虚拟护石仍仅用于离线规划；候选参数未经验证。") };
        root.Controls.Add(introduction, 0, 1);
        root.SizeChanged += (_, _) => introduction.MaximumSize = new Size(Math.Max(200, root.ClientSize.Width - root.Padding.Horizontal - introduction.Margin.Horizontal), 0);
        var body = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        body.ColumnStyles.Add(new(SizeType.Percent, 40));
        body.ColumnStyles.Add(new(SizeType.Percent, 60));
        body.RowStyles.Add(new(SizeType.Percent, 46));
        body.RowStyles.Add(new(SizeType.Percent, 54));
        body.Controls.Add(positions, 0, 0);
        body.Controls.Add(details, 1, 0);
        body.Controls.Add(summary, 0, 1);
        body.SetColumnSpan(summary, 2);
        root.Controls.Add(body, 0, 2);
        positions.Name = "BuildPositions";
        var footer = new TableLayoutPanel() { Name = "BuildFooter", AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        var bottom = new FlowLayoutPanel() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        var edit = B(L("Edit selected piece…", "编辑所选单件…"), Edit);
        edit.Name = "BuildEditPiece";
        var add = B(L("Add set to equipment box…", "添加整套到装备箱…"), AddSet);
        add.Name = "BuildAddSet";
        add.Enabled = editor?.Domain is not null;
        bottom.Controls.AddRange([edit, add, B(L("Close", "关闭"), Close)]);
        footer.Controls.Add(addStatus, 0, 0);
        footer.Controls.Add(bottom, 0, 1);
        root.Controls.Add(footer, 0, 3);
        footer.SizeChanged += (_, _) => addStatus.MaximumSize = new(Math.Max(200, footer.ClientSize.Width - 12), 0);
        title.TextChanged += (_, _) => { if (title.Text != model.Title) { model.Title = title.Text; dirty = true; } };
        candidates.CheckedChanged += (_, _) => { model.ShowCandidates = candidates.Checked; RefreshPlan(); };
        positions.SelectedIndexChanged += (_, _) => ShowPiece();
        FormClosing += (_, e) => { if (!Discard()) e.Cancel = true; };
        RefreshPlan();
        WorkshopStyle.Apply(this);
    }
    private static Button B(string text, Action action)
    {
        var b = new Button() { AutoSize = true, Text = text };
        b.Click += (_, _) => { try { action(); } catch (Exception e) { MessageBox.Show(e.Message, EquipmentDraftForm.L("Operation not completed", "操作未完成")); } };
        return b;
    }
    private bool Discard() => !dirty || MessageBox.Show(this, L("Discard unsaved local plan changes?", "放弃尚未保存的本地配装修改？"), Text, MessageBoxButtons.YesNo) == DialogResult.Yes;
    private void Open()
    {
        if (!Discard())
            return;
        using var dialog = new OpenFileDialog() { Filter = "Build JSON|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        model.Load(dialog.FileName);
        dirty = false;
        RefreshPlan();
    }
    private void Save()
    {
        using var dialog = new SaveFileDialog() { Filter = "Build JSON|*.json", FileName = "build.json", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        model.SaveNew(dialog.FileName);
        dirty = false;
    }
    private void Edit()
    {
        if (positions.SelectedIndex < 0)
            return;
        using var piece = new BuildPieceForm(model, (BuildPosition)positions.SelectedIndex, editor?.Domain);
        if (piece.ShowDialog(this) == DialogResult.OK)
        {
            dirty = true;
            RefreshPlan();
        }
    }
    private void AddSet()
    {
        try
        {
            if (editor?.Domain is null)
                throw new InvalidOperationException(L("Select a character first.", "请先选择角色。"));
            var plan = model.PreviewAddToBox(editor.Domain);
            using var confirmation = new EquipmentSetConfirmationForm(editor, plan);
            if (confirmation.ShowDialog(this) != DialogResult.OK)
                return;
            staged?.Invoke();
            addStatus.Text = L("Complete set staged as one action. Review or Undo in the editor, then export a modified copy.", "整套已作为一次操作暂存。可在编辑器中查看或撤销，再导出修改副本。");
        }
        catch (Exception e)
        {
            addStatus.Text = L("Whole set unavailable; nothing was staged. ", "整套不可用；未暂存任何修改。") + UiLanguage.T(e.Message);
        }
    }
    private void RefreshPlan()
    {
        int selected = positions.SelectedIndex;
        positions.Items.Clear();
        foreach (var position in Enum.GetValues<BuildPosition>())
        {
            var p = model.Piece(position);
            string label = p?.Talisman is not null ? L("Virtual talisman", "虚拟护石") : p?.Id is uint id ? (model.Reference(p)?.Name(UiLanguage.Current) ?? "ID " + id) : L("None", "无");
            positions.Items.Add(PositionLabel(position) + ": " + label);
        }
        positions.SelectedIndex = selected < 0 ? 0 : selected;
        title.Text = model.Title;
        var s = model.Summarize();
        string V(object? v) => v?.ToString() ?? L("Unknown", "未知");
        summary.Text = L("REFERENCE TOTALS — not combat simulation", "参考合计 — 非战斗模拟") + "\r\n" + $"{L("Base defense", "基础防御")}: {V(s.Defense)} · {L("Weapon attack / affinity", "武器攻击 / 会心")}: {V(s.Attack)} / {V(s.Affinity)}\r\n{L("Fire / Water / Thunder / Ice / Dragon", "火 / 水 / 雷 / 冰 / 龙")}: {V(s.Resistances?.Fire)} / {V(s.Resistances?.Water)} / {V(s.Resistances?.Thunder)} / {V(s.Resistances?.Ice)} / {V(s.Resistances?.Dragon)}\r\n" + string.Join("\r\n", s.Skills.Select(x => $"{model.Skill(x.Id)?.Name(UiLanguage.Current) ?? "Skill"} · ID {x.Id}: {x.Total} / {L("maximum", "上限")} {V(x.DeclaredMaximum)} · {L("effective / excess", "有效预览 / 超出")} {x.Effective} / {x.Excess}")) + "\r\n" + (s.Partial ? L("PARTIAL / unresolved:", "部分结果 / 未解决：") : L("All selected reference inputs available.", "所选参考输入完整。")) + "\r\n" + string.Join("\r\n", s.Issues.Select(BuildIssueText.Localize));
        ShowPiece();
    }
    internal static string PositionLabel(BuildPosition p) => UiLanguage.Current != "zh-CN" ? p.ToString() : p switch { BuildPosition.Weapon => "武器", BuildPosition.Head => "头部", BuildPosition.Chest => "胸部", BuildPosition.Arms => "手部", BuildPosition.Waist => "腰部", BuildPosition.Legs => "腿部", _ => "护石" };
    private void ShowPiece()
    {
        if (positions.SelectedIndex < 0)
            return;
        var p = model.Piece((BuildPosition)positions.SelectedIndex);
        details.Text = p is null ? L("Empty position. Edit to choose a local plan piece.", "空位置。编辑以选择本地规划单件。") : BuildPresentation.Piece(model, p, editor?.Domain);
    }
    internal bool SmokeContainsWeapon(uint id)
    {
        bool before = model.ShowCandidates;
        model.ShowCandidates = true;
        bool result = model.Choices(BuildPosition.Weapon).Any(e => e.Id == id);
        model.ShowCandidates = before;
        return result;
    }
    internal void SmokeRoundtrip(string path)
    {
        candidates.Checked = true;
        using (var form = new BuildPieceForm(model, BuildPosition.Weapon))
        {
            form.SmokeSelectFirst();
            form.SmokeConfirm();
        }
        model.SaveNew(path);
        model.Clear();
        model.Load(path);
        RefreshPlan();
        dirty = false;
    }
}
internal sealed class BuildPieceForm : Form
{
    private readonly BuildPlanner planner; private readonly BuildPosition position; private PlannedPiece draft;
    private readonly DomainEditor? ownedDomain;
    private readonly ComboBox pieceMode = new() { Name = "BuildPieceMode", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox ownedTemplates = new() { Name = "BuildOwnedTemplate", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 850 };
    private readonly ComboBox selected = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }; private readonly ComboBox[] decorations = new ComboBox[3], skills = new ComboBox[2]; private readonly NumericUpDown[] levels = new NumericUpDown[2], slots = new NumericUpDown[3]; private readonly TextBox info = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical }; private readonly CheckBox virtualEnabled = new() { AutoSize = true }; private bool updating; private Action<string>? setDecorationSearch; private string decorationQuery = ""; private readonly Label[] slotCaptions = new Label[3];
    private sealed record Choice(uint? Id, string Label)
    {
        public override string ToString() => Label;
    }
    private static string L(string en, string zh) => EquipmentDraftForm.L(en, zh);
    internal BuildPieceForm(BuildPlanner planner, BuildPosition position, DomainEditor? ownedDomain = null)
    {
        this.ownedDomain = ownedDomain;
        this.planner = planner;
        this.position = position;
        draft = planner.Piece(position) ?? new(position, null, [0, 0, 0]);
        Text = BuildForm.PositionLabel(position) + " — " + L("piece draft", "单件草稿");
        Name = "BuildPieceWindow";
        Size = new(940, 760);
        MinimumSize = new(840, 680);
        StartPosition = FormStartPosition.CenterParent;
        Font = new("Microsoft YaHei UI", 9.5f);
        var root = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new(12) };
        root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize));
        Controls.Add(root);
        var top = new TableLayoutPanel() { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1 };
        root.Controls.Add(top, 0, 0);
        var search = new TextBox() { PlaceholderText = L("Name / ID search", "名称 / ID 搜索"), Width = 260 };
        var category = new ComboBox() { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
        category.Items.Add(L("All weapon categories", "全部武器类别"));
        if (position == BuildPosition.Weapon)
            foreach (string c in planner.Choices(position).Select(e => e.Category).OfType<string>().Distinct().Order())
                category.Items.Add(c);
        category.SelectedIndex = 0;
        var find = new FlowLayoutPanel() { AutoSize = true, Dock = DockStyle.Fill };
        find.Controls.AddRange([new Label() { AutoSize = true, Text = L("Piece search", "单件搜索") }, search, category]);
        top.Controls.Add(find);
        top.Controls.Add(selected);
        selected.FormattingEnabled = true;
        void Choices()
        {
            uint? keep = draft.Id;
            updating = true;
            selected.Items.Clear();
            selected.Items.Add(new Choice(null, L("None", "无")));
            foreach (var e in planner.Choices(position, search.Text, category.SelectedIndex > 0 ? category.SelectedItem as string : null))
                selected.Items.Add(new Choice(e.Id, $"ID {e.Id} · {e.Name(UiLanguage.Current)} [UNVERIFIED]"));
            if (keep is not null && !selected.Items.Cast<Choice>().Any(c => c.Id == keep))
                selected.Items.Add(new Choice(keep, "ID " + keep + L(" (retained/unresolved)", "（保留 / 未解决）")));
            selected.SelectedItem = selected.Items.Cast<Choice>().First(c => c.Id == keep);
            updating = false;
        }
        search.TextChanged += (_, _) => Choices();
        category.SelectedIndexChanged += (_, _) => Choices();
        selected.SelectedIndexChanged += (_, _) => { if (updating || pieceMode.SelectedIndex == 1) return; draft = draft with { Id = (selected.SelectedItem as Choice)?.Id, Template = null }; RefreshDecorations(); };
        Choices();
        find.Visible = selected.Visible = position != BuildPosition.Talisman;
        if (position == BuildPosition.Talisman)
        {
            virtualEnabled.Text = L("Virtual talisman (planning only)", "虚拟护石（仅规划）");
            virtualEnabled.Checked = draft.Talisman is not null;
            top.Controls.Add(virtualEnabled);
            for (int i = 0; i < 2; i++)
            {
                int at = i;
                var row = new FlowLayoutPanel() { AutoSize = true, Dock = DockStyle.Fill };
                skills[i] = new()
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 440,
                    DropDownWidth = 700
                };
                skills[i].Items.Add(new Choice(0, L("None", "无")));
                foreach (var e in planner.Skills().Where(e => e.Id <= 255))
                    skills[i].Items.Add(new Choice(e.Id, $"ID {e.Id} · {e.Name(UiLanguage.Current)}"));
                uint id = draft.Talisman?.Pairs[i].Id ?? 0;
                var choice = skills[i].Items.Cast<Choice>().FirstOrDefault(c => c.Id == id);
                if (choice is null)
                {
                    choice = new(id, "ID " + id + " ?");
                    skills[i].Items.Add(choice);
                }
                skills[i].SelectedItem = choice;
                levels[i] = new()
                {
                    Minimum = id == 0 ? 0 : 1,
                    Maximum = Math.Max(draft.Talisman?.Pairs[i].Level ?? 0, id == 0 ? 0 : Math.Min(255, planner.Skill(id)?.DeclaredMaximumSkillLevel ?? 0)),
                    Width = 85,
                    Value = draft.Talisman?.Pairs[i].Level ?? 0
                };
                var skillSearch = new TextBox() { Width = 140, PlaceholderText = L("Name / ID", "名称 / ID") };
                skills[i].Width = 320;
                skillSearch.TextChanged += (_, _) => { var keep = skills[at].SelectedItem as Choice; updating = true; skills[at].Items.Clear(); skills[at].Items.Add(new Choice(0, L("None", "无"))); foreach (var entry in planner.Skills(skillSearch.Text).Where(e => e.Id <= 255)) skills[at].Items.Add(new Choice(entry.Id, $"ID {entry.Id} · {entry.Name(UiLanguage.Current)}")); if (keep is not null && !skills[at].Items.Cast<Choice>().Any(c => c.Id == keep.Id)) skills[at].Items.Add(keep); skills[at].SelectedItem = skills[at].Items.Cast<Choice>().First(c => c.Id == (keep?.Id ?? 0)); updating = false; };
                row.Controls.AddRange([new Label() { AutoSize = true, Text = L("Skill ", "技能 ") + (i + 1) }, skillSearch, skills[i], levels[i]]);
                skills[i].SelectedIndexChanged += (_, _) => { if (updating) return; uint chosen = ((Choice)skills[at].SelectedItem!).Id ?? 0; updating = true; levels[at].Minimum = 0; levels[at].Maximum = chosen == 0 ? 0 : Math.Max(1, Math.Min(255, planner.Skill(chosen)?.DeclaredMaximumSkillLevel ?? (uint)levels[at].Value)); levels[at].Minimum = chosen == 0 ? 0 : 1; updating = false; UpdateVirtual(); };
                levels[i].ValueChanged += (_, _) => UpdateVirtual();
                top.Controls.Add(row);
            }
            var slotRow = new FlowLayoutPanel() { AutoSize = true, Dock = DockStyle.Fill };
            slotRow.Controls.Add(new Label() { AutoSize = true, Text = L("Offline slots (0–4)", "离线槽（0–4）") });
            for (int i = 0; i < 3; i++)
            {
                slots[i] = new()
                {
                    Minimum = 0,
                    Maximum = 4,
                    Width = 70,
                    Value = draft.Talisman?.SlotLevels[i] ?? 0
                };
                slots[i].ValueChanged += (_, _) => { UpdateVirtual(); RefreshDecorations(); };
                slotRow.Controls.Add(slots[i]);
            }
            top.Controls.Add(slotRow);
            virtualEnabled.CheckedChanged += (_, _) => { UpdateVirtual(); RefreshDecorations(); };
        }
        var decoSearch = new TextBox() { Width = 350, PlaceholderText = L("Decoration / related skill / ID", "装饰珠 / 关联技能 / ID") };
        top.Controls.Add(new Label() { AutoSize = true, Text = L("Decoration search by name, skill or ID", "按名称、技能或 ID 搜索装饰珠") });
        top.Controls.Add(decoSearch);
        for (int i = 0; i < 3; i++)
        {
            decorations[i] = new()
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DropDownWidth = 740
            };
            int at = i;
            decorations[i].SelectedIndexChanged += (_, _) => { if (updating) return; draft.Decorations[at] = ((Choice)decorations[at].SelectedItem!).Id ?? 0; ShowInfo(); };
            var slotRow = new TableLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            slotRow.ColumnStyles.Add(new(SizeType.Absolute, 225));
            slotRow.ColumnStyles.Add(new(SizeType.Percent, 100));
            slotCaptions[i] = new()
            {
                AutoSize = true,
                MaximumSize = new(220, 0)
            };
            slotRow.Controls.Add(slotCaptions[i], 0, 0);
            slotRow.Controls.Add(decorations[i], 1, 0);
            top.Controls.Add(slotRow);
        }
        setDecorationSearch = value => decoSearch.Text = value;
        decoSearch.TextChanged += (_, _) => RefreshDecorations(decoSearch.Text);
        root.Controls.Add(info, 0, 1);
        var actions = new FlowLayoutPanel() { AutoSize = true, Dock = DockStyle.Fill };
        var confirm = new Button() { Name = "BuildPieceConfirm", Text = L("Confirm piece", "确认单件"), AutoSize = true };
        var cancel = new Button() { AutoSize = true, Text = L("Cancel", "取消"), DialogResult = DialogResult.Cancel };
        confirm.Click += (_, _) => { try { SmokeConfirm(); DialogResult = DialogResult.OK; Close(); } catch (Exception e) { MessageBox.Show(this, UiLanguage.T(e.Message), Text); } };
        actions.Controls.AddRange([confirm, cancel]);
        root.Controls.Add(actions, 0, 2);
        CancelButton = cancel;
        ConfigureTemplateModes(top, find, selected);
        RefreshDecorations();
        WorkshopStyle.Apply(this);
    }
    private void ConfigureTemplateModes(TableLayoutPanel top, Control searchControls, Control catalogSelection)
    {
        pieceMode.Items.AddRange([L("None","无"),L("Original owned template","原始已有模板"),
            position==BuildPosition.Talisman?L("Virtual talisman — planning only","虚拟护石 — 仅规划"):L("Catalog candidate — offline plan","目录候选 — 离线规划")]);
        ownedTemplates.Items.Add(L("Choose an original record explicitly", "明确选择原始记录"));
        if (ownedDomain is not null)
            foreach (var template in planner.TemplateChoices(ownedDomain, position))
                ownedTemplates.Items.Add(template);
        ownedTemplates.FormattingEnabled = true;
        ownedTemplates.Format += (_, e) =>
        {
            if (e.ListItem is EquipmentTemplate template)
                e.Value = $"{L("Record", "记录")} {template.RecordIndex} · {L("Type", "类型")} {template.Type} · ID {template.Id}" +
                    (template.Pairs is null ? "" : " · " + string.Join("; ", template.Pairs.Select(p => $"ID {p.Id}/{p.Level}")));
        };
        updating = true;
        ownedTemplates.SelectedIndex = 0;
        if (draft.Template is OwnedTemplateSelection binding)
        {
            foreach (var template in ownedTemplates.Items.OfType<EquipmentTemplate>())
                if (template.RecordIndex == binding.RecordIndex && template.Type == binding.Type && template.Id == binding.Id && template.SourceDigest == binding.SourceDigest && template.StateDigest == binding.StateDigest)
                    ownedTemplates.SelectedItem = template;
        }
        pieceMode.SelectedIndex = draft.Template is not null ? 1 : draft.Id is not null || draft.Talisman is not null ? 2 : 0;
        updating = false;
        var modes = new TableLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 3 };
        modes.Controls.Add(new Label() { AutoSize = true, Text = L("Selection mode (positions are planning labels; no automatic equip)", "选择模式（位置仅用于规划；不会自动穿戴）") }, 0, 0);
        modes.Controls.Add(pieceMode, 0, 1);
        modes.Controls.Add(ownedTemplates, 0, 2);
        top.Controls.Add(modes);
        top.Controls.SetChildIndex(modes, 0);

        void SetVisibility()
        {
            bool offline = pieceMode.SelectedIndex == 2;
            ownedTemplates.Visible = pieceMode.SelectedIndex == 1;
            searchControls.Visible = catalogSelection.Visible = offline && position != BuildPosition.Talisman;
            virtualEnabled.Visible = offline && position == BuildPosition.Talisman;
            foreach (var control in skills.Cast<Control?>().Concat(levels).Concat(slots))
                if (control is not null && control.Parent is not null)
                    control.Parent.Visible = offline && position == BuildPosition.Talisman;
        }
        pieceMode.SelectedIndexChanged += (_, _) =>
        {
            SetVisibility();
            if (updating)
                return;
            if (pieceMode.SelectedIndex == 0)
                draft = new(position, null, [0, 0, 0]);
            else if (pieceMode.SelectedIndex == 1)
                UseSelectedTemplate();
            else
            {
                draft = draft with
                {
                    Id = position == BuildPosition.Talisman ? null : (selected.SelectedItem as Choice)?.Id,
                    Template = null,
                    Decorations = [0, 0, 0]
                };
                UpdateVirtual();
            }
            RefreshDecorations();
        };
        ownedTemplates.SelectedIndexChanged += (_, _) =>
        {
            if (updating || pieceMode.SelectedIndex != 1)
                return;
            UseSelectedTemplate();
            RefreshDecorations();
        };
        SetVisibility();
    }

    private void UseSelectedTemplate()
    {
        if (ownedTemplates.SelectedItem is not EquipmentTemplate template)
        {
            draft = new(position, null, [0, 0, 0]);
            return;
        }
        draft = new(position, template.Id, [0, 0, 0], null,
            new(template.RecordIndex, template.Type, template.Id, template.SourceDigest, template.StateDigest));
    }
    private void UpdateVirtual()
    {
        if (updating || pieceMode.SelectedIndex != 2 || position != BuildPosition.Talisman || skills.Any(c => c is null) || levels.Any(c => c is null) || slots.Any(c => c is null))
            return;
        draft = draft with
        {
            Template = null,
            Talisman = virtualEnabled.Checked ?
            new(skills.Select((c, i) => new SkillPair(checked((byte)(((Choice)c.SelectedItem!).Id ?? 0)), (uint)levels[i].Value)).ToArray(), slots.Select(c => (int)c.Value).ToArray()) : null
        };
        ShowInfo();
    }
    private void RefreshDecorations(string? query = null)
    {
        if (query is not null)
            decorationQuery = query;
        updating = true;
        var capacities = planner.Slots(draft);
        var template = pieceMode.SelectedIndex == 1 ? ownedTemplates.SelectedItem as EquipmentTemplate : null;
        for (int i = 0; i < 3; i++)
        {
            if (decorations[i] is null)
                continue;
            var choice = decorations[i];
            slotCaptions[i].Text = (template is not null ? L("Template position ", "模板位置 ") : L("Plan slot ", "规划槽 ")) + (i + 1) + ": " +
                (capacities is null ? L("unknown capacity", "容量未知") : capacities.ElementAtOrDefault(i) == 0 ? L("absent", "无槽") : L("level ", "等级 ") + capacities.ElementAtOrDefault(i));
            uint keep = draft.Decorations[i];
            choice.Items.Clear();
            choice.Items.Add(new Choice(0, L("None", "无")));
            if (template is not null)
            {
                uint original = template.OriginalDecorations[i];
                if (original != 0)
                    choice.Items.Add(new Choice(original, $"ID {original} · " + L("original same-position reference (experimental)", "原始同位置引用（实验性）")));
            }
            else if (pieceMode.SelectedIndex != 0)
            {
                foreach (var entry in planner.Decorations(decorationQuery, capacities?.ElementAtOrDefault(i) ?? 0))
                    choice.Items.Add(new Choice(entry.Id, $"{i + 1}: ID {entry.Id} · {entry.Name(UiLanguage.Current)} · {L("requires", "需求")} {entry.RequiredSlotLevel} · {BuildPresentation.Relations(planner, entry)}"));
            }
            if (!choice.Items.Cast<Choice>().Any(x => x.Id == keep))
                choice.Items.Add(new Choice(keep, $"ID {keep} " + L("retained: unresolved / filtered / conflict", "保留：未解决 / 已筛选 / 冲突")));
            choice.SelectedItem = choice.Items.Cast<Choice>().First(x => x.Id == keep);
        }
        updating = false;
        ShowInfo();
    }
    private void ShowInfo()
    {
        info.Text = BuildPresentation.Piece(planner, draft, ownedDomain);
    }
    internal bool SmokeSkillSearch()
    {
        if (position != BuildPosition.Weapon)
            return true;
        setDecorationSearch?.Invoke("集中");
        bool zh = decorations[0].Items.Cast<Choice>().Any(c => c.Id == 20);
        setDecorationSearch?.Invoke("Focus");
        bool en = decorations[0].Items.Cast<Choice>().Any(c => c.Id == 20);
        return zh && en;
    }
    internal bool SmokeFilterRetention()
    {
        if (position == BuildPosition.Talisman)
        {
            virtualEnabled.Checked = true;
            slots[0].Value = 2;
            setDecorationSearch?.Invoke("NO_MATCH_SYNTHETIC");
            slots[0].Value = 3;
            bool filtered = decorations[0].Items.Count == 1;
            virtualEnabled.Checked = false;
            virtualEnabled.Checked = true;
            return filtered && decorations[0].Items.Count == 1 && slotCaptions.All(l => l.Text.Length > 0);
        }
        SmokeSelectFirst();
        setDecorationSearch?.Invoke("NO_MATCH_SYNTHETIC");
        selected.SelectedIndex = 0;
        if (selected.Items.Count > 1)
            selected.SelectedIndex = 1;
        return decorations.All(c => c.Items.Count == 1) && slotCaptions.All(l => l.Text.Length > 0);
    }
    internal bool SmokeLevelZero()
    {
        pieceMode.SelectedIndex = 2;
        virtualEnabled.Checked = true;
        skills[0].SelectedIndex = 1;
        levels[0].DownButton();
        SmokeConfirm();
        return planner.Piece(position)?.Talisman?.Pairs[0].Level > 0;
    }
    internal bool SmokeVirtual()
    {
        if (position != BuildPosition.Talisman)
            return false;
        pieceMode.SelectedIndex = 2;
        virtualEnabled.Checked = true;
        if (skills[0].Items.Count > 1)
            skills[0].SelectedIndex = 1;
        levels[0].Value = Math.Min(2, levels[0].Maximum);
        slots[0].Value = 2;
        RefreshDecorations();
        if (decorations[0].Items.Count > 1)
            decorations[0].SelectedIndex = 1;
        SmokeConfirm();
        return planner.Piece(position)?.Talisman?.Pairs[0].Level == 2 && planner.Piece(position)?.Decorations[0] == 20;
    }
    internal void SmokeSelectFirst()
    {
        pieceMode.SelectedIndex = 2;
        if (selected.Items.Count > 1)
            selected.SelectedIndex = 1;
    }
    internal bool SmokeCancelKeepsPlan()
    {
        string before = planner.ToJson();
        SmokeSelectFirst();
        return before == planner.ToJson();
    }
    internal void SmokeConfirm()
    {
        if (pieceMode.SelectedIndex == 1 && ownedTemplates.SelectedItem is not EquipmentTemplate)
            throw new InvalidOperationException(L("Choose a current original template explicitly; a stale binding cannot be confirmed.", "请明确选择当前原始模板；无法确认过期绑定。"));
        UpdateVirtual();
        planner.Confirm(draft);
    }
}

internal static class BuildIssueText
{
    internal static string Localize(string value) => UiLanguage.Current != "zh-CN" ? value : value.Replace("candidate metadata hidden; summary unavailable", "候选参数已隐藏，无法合计").Replace("missing or wrong-part catalog ID", "目录 ID 缺失或部位不符").Replace("base slots unknown", "基础槽未知").Replace("skill relations unknown", "技能关系未知").Replace("unresolved or does not fit; excluded", "未解决或不合孔，已排除").Replace("decorations without a piece excluded", "无对应单件的装饰珠已排除").Replace("unresolved/over-limit pair excluded", "未解决或超限配对已排除").Replace("Defense total partial/unknown", "防御合计不完整 / 未知").Replace("Resistance totals partial/unknown", "耐性合计不完整 / 未知").Replace("maximum unknown", "声明上限未知").Replace("attack/affinity unknown", "攻击 / 会心未知").Replace("decoration", "装饰珠").Replace("Weapon", "武器").Replace("Head", "头部").Replace("Chest", "胸部").Replace("Arms", "手部").Replace("Waist", "腰部").Replace("Legs", "腿部").Replace("Talisman", "护石").Replace("Skill ", "技能 ");
}
