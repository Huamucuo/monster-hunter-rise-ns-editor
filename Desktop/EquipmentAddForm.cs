using FreshFoundation;
namespace RiseWorkshopDesktop;

internal static class EquipmentCopyPresentation
{
    internal static string L(string en, string zh) => EquipmentDraftForm.L(en, zh);
    internal static string Warning => L(
        "Experimental copies retain opaque template data whose game instance meaning is unverified. Copies are added to the box; they are not equipped. Export creates a modified copy and preserves the original.",
        "实验性副本保留模板中的未知数据，其游戏实例含义尚未验证。副本加入装备箱，不会自动穿戴。导出生成修改副本并保留原件。");

    internal static string Describe(DirectoryEditor editor, EquipmentAddPlan plan)
    {
        string text = L("Selected character: ", "所选角色：") + (editor.Domain!.Character().Name ?? L("Unknown", "未知")) + "\r\n";
        text += L("Requested copies: ", "请求副本：") + plan.Pieces.Count + "\r\n";
        text += L("Empty equipment capacity: ", "空装备记录容量：") + editor.Domain.EmptyEquipmentCapacity + "\r\n\r\n";
        foreach (var piece in plan.Pieces)
        {
            var template = piece.Template;
            string position = piece.Position is BuildPosition p ? BuildForm.PositionLabel(p) : L("Copy", "副本");
            text += $"{position} · {L("Source record", "来源记录")} {template.RecordIndex} → {L("Destination record", "目标记录")} {piece.DestinationIndex}\r\n";
            text += $"{L("Type", "类型")} {template.Type} · ID {template.Id}\r\n";
            text += L("Planned decoration IDs: ", "计划装饰珠 ID：") + string.Join(" / ", piece.Decorations) + "\r\n";
            if (template.Pairs is not null)
                text += L("Actual template skill pairs: ", "模板实际技能配对：") + string.Join("; ", template.Pairs.Select(p => $"ID {p.Id} / {L("level", "等级")} {p.Level}")) + "\r\n";
            text += L("Actual socket levels: unknown. Other template values are copied without invented defaults.", "实际槽等级：未知。其他模板值完整复制，不生成未确认的默认值。") + "\r\n\r\n";
        }
        return text;
    }

    internal static bool FullyInside(Control control)
    {
        if (!control.Visible)
            return false;
        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
            if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds))
                return false;
        return true;
    }
}

internal sealed class EquipmentAddForm : Form
{
    private readonly DirectoryEditor editor;
    private readonly ComboBox templates = new() { Name = "TemplateChoice", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 860 };
    private readonly NumericUpDown quantity = new() { Name = "TemplateQuantity", Minimum = 1, Maximum = 1, Value = 1, Width = 110 };
    private readonly CheckBox copyDecorations = new() { Name = "CopyTemplateDecorations", AutoSize = true };
    private readonly TextBox preview = new() { Name = "TemplatePreview", Tag = "RawContent", Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly Button apply = new() { Name = "TemplateApply", AutoSize = true };
    private EquipmentAddPlan? plan;

    internal EquipmentAddForm(DirectoryEditor editor, CatalogProviderNames? names = null)
    {
        this.editor = editor;
        if (editor.Domain is null)
            throw new InvalidOperationException("Select a character before adding equipment.");
        Text = EquipmentCopyPresentation.L("Add equipment from original template", "从原始模板添加装备");
        Name = "EquipmentAddWindow";
        Size = new(1040, 760);
        MinimumSize = new(800, 600);
        StartPosition = FormStartPosition.CenterParent;
        Font = new("Microsoft YaHei UI", 9.5f);
        var root = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new(16) };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize));
        Controls.Add(root);
        var choices = new TableLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 3 };
        choices.ColumnStyles.Add(new(SizeType.Percent, 100));
        var introduction = new Label() { AutoSize = true, Text = EquipmentCopyPresentation.L("Choose an original occupied record; numeric choices work without a catalog.", "选择原始已有装备记录；无需目录也可通过数字选择。") };
        choices.Controls.Add(introduction, 0, 0);
        choices.Controls.Add(templates, 0, 1);
        var options = new FlowLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        copyDecorations.Text = EquipmentCopyPresentation.L("Copy source decorations (experimental; default clears all three)", "复制来源装饰珠（实验性；默认清空三项）");
        options.Controls.AddRange([new Label() { AutoSize = true, Text = EquipmentCopyPresentation.L("Quantity", "数量") }, quantity, copyDecorations]);
        choices.Controls.Add(options, 0, 2);
        root.Controls.Add(choices, 0, 0);
        root.Controls.Add(preview, 0, 1);
        var footer = new TableLayoutPanel() { Name = "TemplateFooter", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = true, Padding = new(0, 10, 0, 0) };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100));
        var warning = new Label() { Name = "TemplateWarning", AutoSize = true, Text = EquipmentCopyPresentation.Warning };
        footer.Controls.Add(warning, 0, 0);
        var buttons = new FlowLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        apply.Text = EquipmentCopyPresentation.L("Stage copies in equipment box", "暂存副本到装备箱");
        var cancel = new Button() { AutoSize = true, Text = EquipmentCopyPresentation.L("Cancel", "取消"), DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([apply, cancel]);
        footer.Controls.Add(buttons, 0, 1);
        root.Controls.Add(footer, 0, 2);
        CancelButton = cancel;
        footer.SizeChanged += (_, _) => warning.MaximumSize = new(Math.Max(200, footer.ClientSize.Width - 12), 0);
        choices.SizeChanged += (_, _) => { int width = Math.Max(200, choices.ClientSize.Width - 12); introduction.MaximumSize = new(width, 0); copyDecorations.MaximumSize = new(width, 0); };
        foreach (var template in editor.Domain.EquipmentTemplates())
            templates.Items.Add(template);
        templates.FormattingEnabled = true;
        templates.Format += (_, e) =>
        {
            if (e.ListItem is not EquipmentTemplate template)
                return;
            string label = template.Type == 3 ? EquipmentCopyPresentation.L("Talisman", "护石") :
                names?.GameLabel(template.Type == 1 ? GameCatalogKind.Weapon : GameCatalogKind.Armor, template.Id, template.Type) ?? UiLanguage.T(PresentationRows.TypeName(template.Type));
            e.Value = $"{EquipmentCopyPresentation.L("Record", "记录")} {template.RecordIndex} · {label} · {EquipmentCopyPresentation.L("Type", "类型")} {template.Type} · ID {template.Id}";
        };
        quantity.Maximum = Math.Max(1, editor.Domain.EmptyEquipmentCapacity);
        templates.SelectedIndexChanged += (_, _) => RefreshPreview();
        quantity.ValueChanged += (_, _) => RefreshPreview();
        copyDecorations.CheckedChanged += (_, _) => RefreshPreview();
        apply.Click += (_, _) =>
        {
            try
            {
                var confirmed = plan ?? throw new InvalidOperationException("Preview is unresolved.");
                editor.ApplyDomain(domain => domain.ApplyEquipmentAdd(confirmed));
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception e)
            {
                plan = null;
                apply.Enabled = false;
                preview.Text = EquipmentCopyPresentation.L("Nothing was staged: ", "未暂存任何修改：") + UiLanguage.T(e.Message);
            }
        };
        if (templates.Items.Count > 0)
            templates.SelectedIndex = 0;
        else
            RefreshPreview();
        WorkshopStyle.Apply(this);
    }

    private void RefreshPreview()
    {
        try
        {
            var template = templates.SelectedItem as EquipmentTemplate ?? throw new InvalidOperationException("No original occupied template is available.");
            plan = editor.Domain!.PreviewEquipmentAdd(template, (int)quantity.Value, copyDecorations.Checked);
            preview.Text = EquipmentCopyPresentation.Describe(editor, plan);
            apply.Enabled = true;
        }
        catch (Exception e)
        {
            plan = null;
            apply.Enabled = false;
            preview.Text = EquipmentCopyPresentation.L("Preview unavailable; nothing will be staged. ", "预览不可用；不会暂存修改。") + UiLanguage.T(e.Message);
        }
    }
}

internal sealed class EquipmentSetConfirmationForm : Form
{
    internal EquipmentSetConfirmationForm(DirectoryEditor editor, EquipmentAddPlan plan)
    {
        Text = EquipmentCopyPresentation.L("Confirm whole Build copies", "确认整套配装副本");
        Name = "EquipmentSetConfirmation";
        Size = new(1000, 740);
        MinimumSize = new(780, 580);
        StartPosition = FormStartPosition.CenterParent;
        Font = new("Microsoft YaHei UI", 9.5f);
        var root = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new(16) };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize));
        Controls.Add(root);
        root.Controls.Add(new TextBox() { Name = "SetPreview", Tag = "RawContent", Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Text = EquipmentCopyPresentation.Describe(editor, plan) }, 0, 0);
        var footer = new TableLayoutPanel() { Name = "SetFooter", AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new(0, 10, 0, 0) };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100));
        var warning = new Label() { Name = "SetWarning", AutoSize = true, Text = EquipmentCopyPresentation.Warning };
        var error = new Label() { Name = "SetError", AutoSize = true };
        footer.Controls.Add(warning, 0, 0);
        footer.Controls.Add(error, 0, 1);
        var actions = new FlowLayoutPanel() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        var apply = new Button() { Name = "SetApply", AutoSize = true, Text = EquipmentCopyPresentation.L("Stage every selected piece", "暂存全部所选单件") };
        var cancel = new Button() { AutoSize = true, Text = EquipmentCopyPresentation.L("Cancel", "取消"), DialogResult = DialogResult.Cancel };
        actions.Controls.AddRange([apply, cancel]);
        footer.Controls.Add(actions, 0, 2);
        root.Controls.Add(footer, 0, 1);
        CancelButton = cancel;
        footer.SizeChanged += (_, _) => { warning.MaximumSize = new(Math.Max(200, footer.ClientSize.Width - 12), 0); error.MaximumSize = warning.MaximumSize; };
        apply.Click += (_, _) =>
        {
            try
            {
                editor.ApplyDomain(domain => domain.ApplyEquipmentAdd(plan));
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception e)
            {
                error.Text = EquipmentCopyPresentation.L("Nothing was staged: ", "未暂存任何修改：") + UiLanguage.T(e.Message);
                apply.Enabled = false;
            }
        };
        WorkshopStyle.Apply(this);
    }
}
