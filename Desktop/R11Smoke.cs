using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using FreshFoundation;
namespace RiseWorkshopDesktop;

internal static class R11Smoke
{
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
        {
            string observed = SynchronizationContext.Current?.GetType().Name ?? "null";
            throw new InvalidOperationException("Actual control smoke must run inside the application UI message loop; observed context: " + observed);
        }
        var results = new List<object>();
        int failed = 0;
        void Test(string name, Action action)
        {
            try
            {
                action();
                results.Add(new
                {
                    name,
                    status = "PASS"
                });
            }
            catch (Exception e) { failed++; results.Add(new { name, status = "FAIL", error = e.ToString() }); }
            File.WriteAllText(Path.Combine(folder, "r11-smoke-progress.json"), JsonSerializer.Serialize(new
            {
                failed,
                results
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        T Find<T>(Control root, string name) where T : Control
        {
            var found = root.Controls.Find(name, true).OfType<T>().SingleOrDefault();
            return found ?? throw new Exception("Required actual control is unavailable: " + name);
        }
        DirectoryEditor Model(WorkshopForm form) => (DirectoryEditor)typeof(WorkshopForm).GetField("directoryModel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        void Render(Form form, string name)
        {
            using var image = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(image, new(Point.Empty, form.Size));
            image.Save(Path.Combine(folder, name), ImageFormat.Png);
        }
        void DriveModal(Button launch, string windowName, Action<Form> interact)
        {
            File.AppendAllText(Path.Combine(folder, "ui-context.log"), $"Before {windowName}: thread {Environment.CurrentManagedThreadId}, context {SynchronizationContext.Current?.GetType().Name ?? "null"}\n");
            Exception? failure = null;
            bool found = false;
            int ticks = 0;
            using var timer = new System.Windows.Forms.Timer() { Interval = 40 };
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name == windowName);
                if (dialog is null)
                {
                    if (++ticks > 100)
                    {
                        timer.Stop();
                        failure = new Exception("Modal control path did not open " + windowName);
                    }
                    return;
                }
                timer.Stop();
                found = true;
                try
                {
                    interact(dialog);
                }
                catch (Exception e) { failure = e; }
                finally { if (!dialog.IsDisposed) { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); } }
            };
            timer.Start();
            launch.PerformClick();
            timer.Stop();
            File.AppendAllText(Path.Combine(folder, "ui-context.log"), $"After {windowName}: thread {Environment.CurrentManagedThreadId}, context {SynchronizationContext.Current?.GetType().Name ?? "null"}\n");
            if (failure is not null)
                throw failure;
            if (!found)
                throw new Exception("Actual button did not open " + windowName);
        }
        Button HistoryButton(WorkshopForm form, string field) => (Button)typeof(WorkshopForm).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        void WaitUntil(Func<bool> condition, string reason)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!condition())
            {
                if (watch.ElapsedMilliseconds > 3000)
                    throw new Exception("UI did not settle: " + reason);
                Application.DoEvents();
                Thread.Sleep(5);
            }
            Application.DoEvents();
        }
        string Bounds(Control control)
        {
            var result = new List<string>();
            for (Control? at = control; at is not null; at = at.Parent)
                result.Add(at.GetType().Name + " " + at.Name + " " + at.Bounds + " client=" + at.ClientSize);
            return string.Join(" | ", result);
        }
        foreach (string language in new[] { "en", "zh-CN" })
            foreach (Size size in new[] { new Size(1120, 760), new Size(1500, 940) })
            {
                UiLanguage.Set(language, false);
                string input = Path.Combine(folder, $"input-{language}-{size.Width}.bin");
                File.WriteAllBytes(input, R11SyntheticSamples.Slot(emptyRecords: 12));
                using var form = new WorkshopForm() { Size = size, ShowInTaskbar = false, Opacity = 0 };
                form.Show();
                form.LoadSyntheticForSmoke(input);
                Application.DoEvents();
                Test($"{language} {size.Width} actual name Apply/reset and draft retention", () =>
                {
                    form.SelectDomainSmokeTab(0);
                    Application.DoEvents();
                    var editor = Model(form);
                    var document = editor.Slot.Document;
                    var draft = Find<TextBox>(form, "NameDraft");
                    if (!EquipmentCopyPresentation.FullyInside(draft))
                        throw new Exception("Name draft input is outside the visible character region");
                    draft.Text = "新😀Name";
                    if (editor.StagedCount != 0 || editor.Slot.Document != document)
                        throw new Exception("Keystrokes changed staging or reparsed the file");
                    Find<Button>(form, "NameApply").PerformClick();
                    if (editor.Domain!.Character().Name != "新😀Name" || editor.StagedCount != 2)
                        throw new Exception("Real name Apply did not stage both owner fields");
                    draft.Text = "not applied";
                    form.SetSmokeLanguage(language == "en" ? "zh-CN" : "en");
                    if (draft.Text != "not applied")
                        throw new Exception("Language change discarded a draft");
                    form.SetSmokeLanguage(language);
                    Find<Button>(form, "NameReset").PerformClick();
                    if (draft.Text != "新😀Name" || editor.StagedCount != 2)
                        throw new Exception("Reset changed applied staging");
                    var warning = Find<Label>(form, "NameMirrorWarning");
                    if (!EquipmentCopyPresentation.FullyInside(warning) || !warning.Text.Contains(language == "en" ? "Single-file" : "单文件"))
                        throw new Exception("Single-file mirror limitation is not visible");
                    if (!EquipmentCopyPresentation.FullyInside(Find<Button>(form, "NameApply")))
                        throw new Exception("Name Apply is outside the fixed footer");
                    Render(form, $"{language}-{size.Width}-name.png");
                });
                Test($"{language} {size.Width} actual equipment template entry", () =>
                {
                    form.SelectDomainSmokeTab(2);
                    Application.DoEvents();
                    var entry = Find<Button>(form, "EquipmentTemplateAdd");
                    if (!entry.Visible || !entry.Enabled)
                        throw new Exception("Equipment add entry unavailable");
                    int occupied = Model(form).Domain!.Equipment().Count(e => e.Type is 1 or 2 or 3);
                    DriveModal(entry, "EquipmentAddWindow", dialog =>
                    {
                        dialog.Size = size.Width == 1120 ? new(800, 600) : new(1040, 760);
                        Find<ComboBox>(dialog, "TemplateChoice").SelectedIndex = 0;
                        Find<NumericUpDown>(dialog, "TemplateQuantity").Value = 2;
                        Find<CheckBox>(dialog, "CopyTemplateDecorations").Checked = false;
                        Application.DoEvents();
                        var apply = Find<Button>(dialog, "TemplateApply");
                        var warning = Find<Label>(dialog, "TemplateWarning");
                        Render(dialog, $"{language}-{size.Width}-template-add.png");
                        if (!apply.Enabled || !EquipmentCopyPresentation.FullyInside(apply) || !EquipmentCopyPresentation.FullyInside(warning))
                            throw new Exception("Template confirmation or warning is clipped: " + Bounds(apply) + " / " + Bounds(warning));
                        if (!Find<TextBox>(dialog, "TemplatePreview").Text.Contains(language == "en" ? "Destination record" : "目标记录"))
                            throw new Exception("Template destinations are missing");
                        Render(dialog, $"{language}-{size.Width}-template-add.png");
                        apply.PerformClick();
                    });
                    if (Model(form).Domain!.Equipment().Count(e => e.Type is 1 or 2 or 3) != occupied + 2)
                        throw new Exception("Actual template Apply omitted copies");
                    HistoryButton(form, "undo").PerformClick();
                    if (Model(form).Domain!.Equipment().Count(e => e.Type is 1 or 2 or 3) != occupied)
                        throw new Exception("Template copies did not Undo together");
                    WaitUntil(() => HistoryButton(form, "redo").Enabled, "template Redo ready");
                    HistoryButton(form, "redo").PerformClick();
                    if (Model(form).Domain!.Equipment().Count(e => e.Type is 1 or 2 or 3) != occupied + 2)
                        throw new Exception("Template copies did not Redo together");
                    WaitUntil(() => HistoryButton(form, "undo").Enabled && !HistoryButton(form, "redo").Enabled, "template history toolbar updated");
                });
                Test($"{language} {size.Width} actual whole Build action", () =>
                {
                    using var build = form.CreateBuildWindow();
                    build.Size = size.Width == 1120 ? new(900, 600) : new(1200, 850);
                    build.Show();
                    Application.DoEvents();
                    var action = Find<Button>(build, "BuildAddSet");
                    if (!action.Visible || !action.Enabled)
                        throw new Exception("Whole Build action is unavailable");
                    var positions = Find<ListBox>(build, "BuildPositions");
                    foreach (var position in Enum.GetValues<BuildPosition>())
                    {
                        positions.SelectedIndex = (int)position;
                        DriveModal(Find<Button>(build, "BuildEditPiece"), "BuildPieceWindow", piece =>
                        {
                            piece.Size = size.Width == 1120 ? new(840, 680) : new(1050, 850);
                            Find<ComboBox>(piece, "BuildPieceMode").SelectedIndex = 1;
                            var templates = Find<ComboBox>(piece, "BuildOwnedTemplate");
                            templates.SelectedItem = templates.Items.OfType<EquipmentTemplate>().Single(t => t.RecordIndex == (int)position);
                            Application.DoEvents();
                            var confirm = Find<Button>(piece, "BuildPieceConfirm");
                            if (!EquipmentCopyPresentation.FullyInside(confirm))
                                throw new Exception("Piece confirmation is clipped");
                            if (position == BuildPosition.Talisman)
                                Render(piece, $"{language}-{size.Width}-owned-talisman.png");
                            confirm.PerformClick();
                        });
                    }
                    Application.DoEvents();
                    if (!EquipmentCopyPresentation.FullyInside(action))
                        throw new Exception("Whole Build action is outside its fixed footer");
                    Render(build, $"{language}-{size.Width}-build.png");
                    int occupied = Model(form).Domain!.Equipment().Count(e => e.Type is 1 or 2 or 3);
                    DriveModal(action, "EquipmentSetConfirmation", confirmation =>
                    {
                        confirmation.Size = size.Width == 1120 ? new(780, 580) : new(1000, 740);
                        Application.DoEvents();
                        var apply = Find<Button>(confirmation, "SetApply");
                        var warning = Find<Label>(confirmation, "SetWarning");
                        Render(confirmation, $"{language}-{size.Width}-set-confirmation.png");
                        if (!EquipmentCopyPresentation.FullyInside(apply) || !EquipmentCopyPresentation.FullyInside(warning))
                            throw new Exception("Set confirmation or warning is clipped: " + Bounds(apply) + " / " + Bounds(warning));
                        string preview = Find<TextBox>(confirmation, "SetPreview").Text;
                        if (!preview.Contains(language == "en" ? "Requested copies: 7" : "请求副本：7") || !preview.Contains("ID 107") || !preview.Contains(language == "en" ? "Actual template skill pairs" : "模板实际技能配对"))
                            throw new Exception("Complete set preview omitted a piece or actual talisman pairs");
                        Render(confirmation, $"{language}-{size.Width}-set-confirmation.png");
                        apply.PerformClick();
                    });
                    if (Model(form).Domain!.Equipment().Count(e => e.Type is 1 or 2 or 3) != occupied + 7)
                        throw new Exception("Actual whole Build Apply omitted a piece");
                    HistoryButton(form, "undo").PerformClick();
                    if (Model(form).Domain!.Equipment().Count(e => e.Type is 1 or 2 or 3) != occupied)
                        throw new Exception("Whole Build did not Undo together");
                    WaitUntil(() => HistoryButton(form, "redo").Enabled, "whole Build Redo ready");
                    HistoryButton(form, "redo").PerformClick();
                    if (Model(form).Domain!.Equipment().Count(e => e.Type is 1 or 2 or 3) != occupied + 7)
                        throw new Exception("Whole Build did not Redo together");
                    WaitUntil(() => HistoryButton(form, "undo").Enabled && !HistoryButton(form, "redo").Enabled, "whole Build history toolbar updated");
                    // Suppress the normal unsaved-plan question during test disposal;
                    // all authored plan changes have already exercised real controls.
                    typeof(BuildForm).GetField("dirty", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(build, false);
                    build.Close();
                });
                using (var image = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(image, new(Point.Empty, form.Size));
                    image.Save(Path.Combine(folder, $"{language}-{size.Width}-workshop.png"), ImageFormat.Png);
                }
                Model(form).Clear();
                WaitUntil(() => !typeof(WorkshopForm).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!.Equals(true), "window idle");
                Test($"{language} {size.Width} window owns detached domain controller", () =>
                {
                    var pages = (DomainPages)typeof(WorkshopForm).GetField("domainPages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                    form.Close();
                    if (!pages.IsDisposed)
                        throw new Exception("Closed window leaves its detached domain controller subscribed to language changes");
                });
            }
        File.WriteAllText(Path.Combine(folder, "r11-smoke-results.json"), JsonSerializer.Serialize(new
        {
            atUtc = DateTimeOffset.UtcNow,
            scope = "Fresh R11 actual WinForms controls with generated synthetic files only; native game acceptance NOT_TESTED",
            failed,
            results
        }, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
}
