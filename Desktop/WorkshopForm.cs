using FreshFoundation;
namespace RiseWorkshopDesktop;
internal sealed class WorkshopForm : Form
{
    private EditorWorkspace model=new();
    private DirectoryEditor directoryModel=new();
    private DomainPages domainPages=null!;
    private TabControl mainTabs=null!;
    private Panel emptyGuide=null!;
    private TableLayoutPanel navigationLayout=null!;
    private Control slotSidebar=null!;
    private Action? updateToolbarContext;
    private GameCatalog? catalog;
    private GameCatalog? externalCatalog;
    internal CatalogForm CreateCatalogBrowser()=>new(catalog??externalCatalog??new GameCatalog([]),directoryModel,RetainLocalCatalog);
    private void RetainLocalCatalog(GameCatalog loaded){catalog=loaded;domainPages.SetCatalog(loaded);}
    internal BuildForm CreateBuildWindow()=>new(catalog??externalCatalog??new GameCatalog([]),directoryModel,()=>{domainPages.RefreshData();UpdateActions();_ = RefreshPage();});
    private readonly ListBox slots=new(){Dock=DockStyle.Fill,IntegralHeight=false,HorizontalScrollbar=true};
    private readonly DataGridView grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoGenerateColumns=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,RowHeadersVisible=false,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None};
    private readonly TextBox search=new(){Width=280,PlaceholderText="Search opaque ID or text"};
    private readonly TextBox hex=new(){Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical,Font=new Font("Consolas",10),PlaceholderText="Select a primitive occurrence to inspect its bytes"};
    private readonly TextBox details=new(){Tag="RawContent",Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    private readonly Label status=new(){Dock=DockStyle.Fill,AutoEllipsis=true,Text="Open a folder or file. No previous save is loaded."};
    private readonly Label summary=new(){Dock=DockStyle.Fill,AutoEllipsis=true,Text="Experimental structural editor. Game identity, meanings and game acceptance are unverified."};
    private readonly Label context=new(){AutoSize=true,Text="Root records"};
    private readonly Label pageInfo=new(){AutoSize=true,Text="No file selected"};
    private readonly Label pending=new(){AutoSize=true,Text="Staged: 0"};
    private readonly Button apply=new(){Text="Apply to staging",AutoSize=true};
    private readonly Button redo=new(){Text="Redo",AutoSize=true},reviewChanges=new(){Text="Staged changes…",AutoSize=true};
    private readonly Button undo=new(){Text="Undo",AutoSize=true};
    private readonly Button clear=new(){Text="Clear staged",AutoSize=true};
    private readonly Button export=new(){Name="PrimaryExport",Text="Export copy…",AutoSize=true};
    private readonly Button restore=new(){Text="Restore backup…",AutoSize=true};
    private readonly Button openExport=new(){Text="Open export",AutoSize=true};
    private readonly System.Windows.Forms.Timer debounce=new(){Interval=300};
    private CancellationTokenSource? filterCancellation;
    private IReadOnlyList<SlotCandidate> candidates=[];
    private BrowserPage? displayed;
    private int parent=-1,page,chosen=-1,queryGeneration;
    private bool busy;
    private CompletedOriginalBackup? lastBackup;
    private string? lastOutput;
    public WorkshopForm()
    {
        Text="Rise Structural Workshop — Experimental";StartPosition=FormStartPosition.CenterScreen;Size=new Size(1500,940);MinimumSize=new Size(1120,760);Font=new Font("Microsoft YaHei UI",9.5f);AutoScaleMode=AutoScaleMode.Dpi;
        var root=new TableLayoutPanel(){Tag="Canvas",Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(16)};
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));Controls.Add(root);
        var toolbar=new TableLayoutPanel(){Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=2,Padding=new Padding(8),Margin=new Padding(0,0,0,12)};toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));var secondary=new FlowLayoutPanel(){Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true};var languageGroup=new FlowLayoutPanel(){AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Anchor=AnchorStyles.Top|AnchorStyles.Right,Margin=new Padding(18,4,4,4)};var top=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=true,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};var file=Button("Open file…",async()=>await OpenFile());var folder=Button("Open folder…",async()=>await OpenFolder());top.Controls.AddRange([folder,file,export]);secondary.Controls.AddRange([restore,openExport,pending]);toolbar.Controls.Add(top,0,0);toolbar.Controls.Add(languageGroup,1,0);toolbar.Controls.Add(secondary,0,1);toolbar.SetColumnSpan(secondary,2);export.BackColor=WorkshopStyle.Accent;export.ForeColor=Color.White;root.Controls.Add(toolbar,0,0);bool sizingToolbar=false;toolbar.Layout+=(_,_)=>{if(sizingToolbar||top.Controls.Count==0)return;sizingToolbar=true;try{int h1=Math.Max(top.Controls.Cast<Control>().Where(c=>c.Visible).Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(0).Max(),languageGroup.PreferredSize.Height+languageGroup.Margin.Vertical)+6;int h2=secondary.Visible?secondary.Controls.Cast<Control>().Where(c=>c.Visible).Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(0).Max()+6:0;toolbar.RowStyles[0].SizeType=toolbar.RowStyles[1].SizeType=SizeType.Absolute;toolbar.RowStyles[0].Height=h1;toolbar.RowStyles[1].Height=h2;root.RowStyles[0].SizeType=SizeType.Absolute;root.RowStyles[0].Height=h1+h2+toolbar.Padding.Vertical+toolbar.Margin.Vertical+8;}finally{sizingToolbar=false;}};root.Controls.Add(summary,0,1);root.Controls.Add(status,0,3);
        var split=new TableLayoutPanel(){Tag="Canvas",Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};navigationLayout=split;split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.Controls.Add(split,0,2);
        var slotPanel=new TableLayoutPanel(){Dock=DockStyle.Fill,RowCount=2};slotPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,60));slotPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));slotPanel.Controls.Add(new Label(){Dock=DockStyle.Fill,Text="Double-click a slot to open"},0,0);slotPanel.Controls.Add(slots,0,1);slotSidebar=slotPanel;split.Controls.Add(slotPanel,0,0);
        var right=new TableLayoutPanel(){Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));right.RowStyles.Add(new RowStyle(SizeType.Absolute,140));mainTabs=new TabControl(){Dock=DockStyle.Fill};var stage=new Panel(){Tag="Canvas",Dock=DockStyle.Fill,Padding=new Padding(10,0,0,0)};split.Controls.Add(stage,1,0);stage.Controls.Add(mainTabs);emptyGuide=new Panel(){Dock=DockStyle.Fill,Padding=new Padding(40)};var welcome=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};welcome.Controls.Add(new Label(){Text="Start with a save copy",AutoSize=true,Font=new Font("Microsoft YaHei UI",20,FontStyle.Bold),Margin=new Padding(0,20,0,18)});welcome.Controls.Add(new Label(){Text="Open a folder to select a character, or open one file.\nYour source stays unchanged. Review staged edits, then export a new copy.",AutoSize=true,MaximumSize=new Size(610,0),Margin=new Padding(0,0,0,22)});var welcomeActions=new FlowLayoutPanel(){AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false};var welcomeOpen=Button("Open folder…",async()=>await OpenFolder());welcomeOpen.Name="PrimaryOpen";welcomeActions.Controls.AddRange([welcomeOpen,Button("Open file…",async()=>await OpenFile())]);welcome.Controls.Add(welcomeActions);emptyGuide.Controls.Add(welcome);stage.Controls.Add(emptyGuide);var namedTab=new TabPage("Character, inventory, equipment");var rawTab=new TabPage("Structure / raw research");mainTabs.TabPages.AddRange([namedTab,rawTab]);rawTab.Controls.Add(right);domainPages=new DomainPages(()=>{UpdateActions();_ = RefreshPage();});mainTabs.TabPages.Remove(namedTab);mainTabs.TabPages.Clear();mainTabs.TabPages.AddRange(domainPages.DetachPages());mainTabs.TabPages.Add(rawTab);string externalDirectory=Path.Combine(AppContext.BaseDirectory,"data","mhrice");if(File.Exists(Path.Combine(externalDirectory,"catalog.json"))){try{externalCatalog=GameCatalog.LoadDirectory(externalDirectory,()=>UiLanguage.Current);string datasetHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(externalDirectory,"catalog.json"))));domainPages.AddProvider(new MhriceCatalogProvider(externalCatalog,datasetHash));}catch(Exception e)when(e is IOException or ArgumentException or System.Text.Json.JsonException){status.Text="External name candidates unavailable: "+UiLanguage.Data(e.Message);}}
        var nav=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=true,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};nav.Controls.AddRange([Button("Roots",async()=>{parent=-1;page=0;search.Clear();await RefreshPage();}),Button("Up",async()=>{if(parent>=0)parent=model.ParentOf(parent);page=0;search.Clear();await RefreshPage();}),search]);right.Controls.Add(nav,0,0);
        var paging=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=true,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};paging.Controls.AddRange([Button("Previous",async()=>{if(page>0){page--;await RefreshPage();}}),Button("Next",async()=>{if(displayed is not null&&(page+1)*200<displayed.Total){page++;await RefreshPage();}}),pageInfo,context]);right.Controls.Add(paging,0,1);
        AddColumn("Event",nameof(BrowserRow.EventIndex),70);AddColumn("Opaque ID",nameof(BrowserRow.OpaqueIdentifier),140);AddColumn("Tag",nameof(BrowserRow.Tag),55);AddColumn("Width",nameof(BrowserRow.Width),65);AddColumn("Coordinate",nameof(BrowserRow.Coordinate),120);AddColumn("Children",nameof(BrowserRow.ChildCount),100);AddColumn("Raw hex",nameof(BrowserRow.RawHex),270);AddColumn("Raw interpretations (not game meanings)",nameof(BrowserRow.RawInterpretation),360);AddColumn("UTF-16 text",nameof(BrowserRow.Text),300);right.Controls.Add(grid,0,2);
        var editors=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(0,8,0,0)};editors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));editors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));editors.RowStyles.Add(new RowStyle(SizeType.AutoSize));editors.RowStyles.Add(new RowStyle(SizeType.Percent,100));editors.RowStyles.Add(new RowStyle(SizeType.AutoSize));editors.Controls.Add(new Label(){Text="Selected occurrence / local text",AutoSize=true},0,0);editors.Controls.Add(new Label(){Text="Read-only primitive bytes",AutoSize=true},1,0);editors.Controls.Add(details,0,1);editors.Controls.Add(hex,1,1);var editButtons=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=true,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};editButtons.Controls.AddRange([undo,clear]);apply.Visible=false;hex.ReadOnly=true;editors.Controls.Add(editButtons,1,2);editors.Controls.Add(new Label(){AutoSize=true,Text="Author: 花木错"},0,2);right.Controls.Add(editors,0,3);
        slots.DoubleClick+=async(_,_)=>{if(slots.SelectedIndex>=0&&slots.SelectedIndex<candidates.Count){var c=candidates[slots.SelectedIndex];if(c.Status==SlotStatus.Recognized)await OpenPath(c.Path);else status.Text=UiLanguage.Data(c.Detail);}};
        grid.CurrentCellChanged+=(_,_)=>SelectRow();grid.CellDoubleClick+=async(_,e)=>{if(e.RowIndex>=0&&displayed is not null){var row=displayed.Rows[e.RowIndex];if(row.ChildCount>0){parent=row.EventIndex;page=0;search.Clear();await RefreshPage();}}};
        search.TextChanged+=(_,_)=>{debounce.Stop();debounce.Start();};debounce.Tick+=async(_,_)=>{debounce.Stop();page=0;await RefreshPage();};
        apply.Click+=async(_,_)=>{try{if(chosen<0)return;var raw=Convert.FromHexString(string.Concat(hex.Text.Where(c=>!char.IsWhiteSpace(c))));directoryModel.ApplyRaw(chosen,raw);domainPages.RefreshData();status.Text="Primitive change staged. Source file is unchanged.";await RefreshPage();}catch(Exception e){ShowError(e);}};
        redo.Click+=async(_,_)=>{directoryModel.Redo();domainPages.RefreshData();await RefreshPage();};reviewChanges.Click+=(_,_)=>{using var dialog=new ChangesForm(directoryModel);dialog.ShowDialog(this);};undo.Click+=async(_,_)=>{directoryModel.Undo();domainPages.RefreshData();await RefreshPage();};clear.Click+=async(_,_)=>{directoryModel.Clear();domainPages.RefreshData();await RefreshPage();};
        export.Click+=async(_,_)=>await Export();restore.Click+=async(_,_)=>await Restore();openExport.Click+=async(_,_)=>{if(lastOutput is not null&&AllowDiscard()){string path=lastOutput;directoryModel=new();candidates=[SlotDiscovery.Inspect(path)];FillSlots();await OpenPath(path);}};
        secondary.Controls.Add(undo);secondary.Controls.Add(redo);secondary.Controls.Add(clear);secondary.Controls.Add(reviewChanges);var validation=Button("Game validation…",()=>{using var dialog=new ValidationForm();dialog.ShowDialog(this);return Task.CompletedTask;});validation.Name="ValidationEntry";secondary.Controls.Add(validation);var catalogButton=Button("Names and sources…",async()=>{if(externalCatalog is not null){using var sourceBrowser=CreateCatalogBrowser();sourceBrowser.ShowDialog(this);return;}using var pick=new FolderBrowserDialog(){Description="Choose a local directory containing catalog.json"};if(UiLanguage.Show(pick,this)!=DialogResult.OK)return;catalog=await Task.Run(()=>GameCatalog.LoadDirectory(pick.SelectedPath,()=>UiLanguage.Current));RetainLocalCatalog(catalog);using var browser=CreateCatalogBrowser();browser.ShowDialog(this);});secondary.Controls.Add(catalogButton);var build=Button("Build / 配装",()=>{using var planner=CreateBuildWindow();planner.ShowDialog(this);return Task.CompletedTask;});secondary.Controls.Add(build);var candidateNames=new CheckBox(){Text="Show unverified name candidates",AutoSize=true,Checked=false};candidateNames.CheckedChanged+=(_,_)=>domainPages.SetCandidateDisplay(candidateNames.Checked);secondary.Controls.Add(candidateNames);file.Name="ToolbarOpenFile";folder.Name="ToolbarOpenFolder";updateToolbarContext=()=>{bool hasContext=candidates.Count>0||model.Document is not null;file.Visible=folder.Visible=hasContext;var target=hasContext?secondary:top;if(validation.Parent!=target)target.Controls.Add(validation);if(catalogButton.Parent!=target)target.Controls.Add(catalogButton);if(build.Parent!=target)target.Controls.Add(build);secondary.Visible=hasContext;toolbar.PerformLayout();};
        var language=new ComboBox(){Name="LanguageChoice",DropDownStyle=ComboBoxStyle.DropDownList,Width=150};language.Items.AddRange(["English","简体中文"]);language.SelectedIndex=UiLanguage.Current=="zh-CN"?1:0;languageGroup.Controls.Add(new Label(){Text="Language / 语言",AutoSize=true,Margin=new Padding(0,6,8,0)});languageGroup.Controls.Add(language);language.SelectedIndexChanged+=(_,_)=>{try{UiLanguage.Set(language.SelectedIndex==1?"zh-CN":"en");}catch(Exception e){ShowError(e);}};WorkshopStyle.Apply(this);BackColor=WorkshopStyle.Canvas;export.BackColor=WorkshopStyle.Accent;export.ForeColor=Color.White;summary.ForeColor=Color.FromArgb(90,107,125);status.ForeColor=Color.FromArgb(90,107,125);UiLanguage.Attach(this);
        FormClosing+=(_,e)=>{filterCancellation?.Cancel();if(directoryModel.StagedCount>0&&UiLanguage.Message(this,"Discard staged changes and close?","Staged changes",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};UpdateActions();
    }
    private Button Button(string text,Func<Task> action){var button=new Button(){Text=text,AutoSize=true};button.Click+=async(_,_)=>{try{await action();}catch(Exception e){ShowError(e);}};return button;}
    private void AddColumn(string label,string property,int width)=>grid.Columns.Add(new DataGridViewTextBoxColumn(){HeaderText=label,DataPropertyName=property,Width=width,SortMode=DataGridViewColumnSortMode.NotSortable});
    private bool AllowDiscard()=>directoryModel.StagedCount==0||UiLanguage.Message(this,"Discard current staged changes to open another file?","Staged changes",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;
    private async Task OpenFile(){if(busy)return;using var dialog=new OpenFileDialog(){Title="Choose a save file explicitly",Filter="Binary save files (*.bin)|*.bin|All files (*.*)|*.*"};if(UiLanguage.Show(dialog,this)==DialogResult.OK){if(!AllowDiscard())return;directoryModel=new();candidates=[SlotDiscovery.Inspect(dialog.FileName)];FillSlots();await OpenPath(dialog.FileName);}}
    private async Task OpenFolder(){if(busy)return;using var dialog=new FolderBrowserDialog(){Description="Select a directory containing slot files (no recursive scan)"};if(UiLanguage.Show(dialog,this)!=DialogResult.OK||!AllowDiscard())return;filterCancellation?.Cancel();queryGeneration++;directoryModel=new();model=directoryModel.Slot;chosen=-1;displayed=null;grid.DataSource=null;details.Clear();hex.Clear();lastBackup=null;lastOutput=null;domainPages.Bind(directoryModel);summary.Text="Select a recognized candidate explicitly.";busy=true;UpdateActions();status.Text="Inspecting four known filename candidates…";try{candidates=await Task.Run(()=>directoryModel.OpenDirectory(dialog.SelectedPath));FillSlots();status.Text="Double-click a recognized candidate to select it. Other candidates remain independent.";}catch(Exception e){ShowError(e);}finally{busy=false;UpdateActions();}}
    private void FillSlots(){slots.Items.Clear();foreach(var c in candidates)slots.Items.Add($"{c.Label} — {c.Status}");}
    private async Task OpenPath(string path)
    {
        if(busy||!AllowDiscard())return;filterCancellation?.Cancel();queryGeneration++;busy=true;UpdateActions();status.Text="Reading explicitly selected file…";
        try{await Task.Run(()=>directoryModel.OpenSlot(path));model=directoryModel.Slot;domainPages.Bind(directoryModel);parent=-1;page=0;chosen=-1;search.Clear();summary.Text=$"{UiLanguage.Data(Path.GetFileName(path))}  |  CHECKSUM_MATCHED · EXPERIMENTAL structure  |  {model.Document!.Structure.Summary.ValueEvents:N0} events\nExperimental editor — game acceptance is unverified. Changes are staged; export creates new copies.";status.Text="Selected. Double-click rows with children to browse; search checks all IDs and text.";}
        catch(Exception e){model=new();grid.DataSource=null;displayed=null;details.Clear();hex.Clear();ShowError(e);}
        finally{busy=false;UpdateActions();}await RefreshPage();
    }
    private async Task RefreshPage()
    {
        if(model.Document is null||busy)return;filterCancellation?.Cancel();filterCancellation=new();var token=filterCancellation.Token;int generation=++queryGeneration;var current=model;string query=search.Text;int currentParent=parent,currentPage=page;
        try{var data=await Task.Run(()=>current.Browse(currentParent,query,currentPage,200,token));if(generation!=queryGeneration||current!=model||IsDisposed||Disposing)return;displayed=data;grid.DataSource=data.Rows.ToList();pageInfo.Text=$"Page {page+1} · {data.Total:N0} matches";context.Text=query.Trim().Length>0?"Global search":parent<0?"Root records":$"Children of event {parent}";pending.Text=$"Staged: {directoryModel.StagedCount:N0}";domainPages.Enabled=!busy&&model.Document is not null;UpdateActions();}
        catch(OperationCanceledException){}catch(Exception e){ShowError(e);}
    }
    private void SelectRow()
    {
        if(displayed is null||grid.CurrentRow is null||grid.CurrentRow.Index<0||grid.CurrentRow.Index>=displayed.Rows.Count)return;var row=displayed.Rows[grid.CurrentRow.Index];chosen=row.EventIndex;
        var e=model.Document!.Structure.Events[chosen];details.Text=$"Event {chosen}; parent {row.ParentIndex}; ancestors: {string.Join(" > ",model.AncestorsOf(chosen))}\r\nOpaque identifier: {row.OpaqueIdentifier}; class: {(e.ClassIdentifier is uint id?$"0x{id:X8}":"—")}\r\nTag {e.Tag}; width {e.Width}; count {e.Count}; mode {e.Mode}; coordinate {row.Coordinate}\r\n{row.RawInterpretation}\r\n\r\n{model.TextAt(chosen).Replace("\0","␀")}";
        var raw=model.PrimitiveBytes(chosen);hex.Text=raw is null?"":Convert.ToHexString(raw);hex.ReadOnly=true;UpdateActions();
    }
    private void UpdateActions(){updateToolbarContext?.Invoke();export.Visible=pending.Visible=undo.Visible=redo.Visible=reviewChanges.Visible=clear.Visible=model.Document is not null;restore.Visible=lastBackup is not null;openExport.Visible=lastOutput is not null;if(emptyGuide is not null){bool loaded=model.Document is not null;bool showSidebar=candidates.Count>0;slotSidebar.Visible=showSidebar;navigationLayout.ColumnStyles[0].Width=showSidebar?205:0;emptyGuide.Visible=!loaded;mainTabs.Visible=loaded;mainTabs.Enabled=!busy;if(!loaded)emptyGuide.BringToFront();}apply.Enabled=!busy&&chosen>=0&&model.Document is not null&&model.Document.Structure.Events.Count>chosen&&model.Document.Structure.Events[chosen].DataCoordinate!=null;undo.Enabled=!busy&&directoryModel.CanUndo;redo.Enabled=!busy&&directoryModel.CanRedo;reviewChanges.Enabled=!busy&&model.Document is not null;clear.Enabled=!busy&&directoryModel.StagedCount>0;export.Enabled=!busy&&model.Document is not null;restore.Enabled=!busy&&lastBackup is not null;openExport.Enabled=!busy&&lastOutput is not null;slots.Enabled=!busy;grid.Enabled=!busy;search.Enabled=!busy;pending.Text=$"Staged: {directoryModel.StagedCount:N0}";domainPages.Enabled=!busy&&model.Document is not null;}
    private async Task Export()
    {
        if(busy||model.Document is null)return;if(candidates.Count==4&&directoryModel.SlotNumber is not null){await ExportDirectory();return;}using var dialog=new SaveFileDialog(){Title="Export to a NEW file",Filter="Binary files (*.bin)|*.bin|All files (*.*)|*.*",FileName=Path.GetFileNameWithoutExtension(model.SourcePath)+"-edited.bin",OverwritePrompt=true};if(UiLanguage.Show(dialog,this)!=DialogResult.OK)return;
        string parentFolder=Path.GetDirectoryName(dialog.FileName)!;busy=true;UpdateActions();status.Text="Validating staging, backing up original, exporting new copy…";
        try{var receipt=await Task.Run(()=>model.Export(dialog.FileName,parentFolder));lastBackup=receipt.Backup;lastOutput=receipt.OutputPath;status.Text="Export completed; original preserved. Backup: "+UiLanguage.Data(receipt.BackupPath);UiLanguage.Message(this,$"Experimental copy created:\n{UiLanguage.Data(receipt.OutputPath)}\n\nVerified-at-creation original backup:\n{UiLanguage.Data(receipt.BackupPath)}\n\nGame acceptance remains unverified.","Export completed",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        catch(ExperimentalExportException e){if(e.CompletedBackup is not null)lastBackup=e.CompletedBackup;ShowError(e);}catch(Exception e){ShowError(e);}finally{busy=false;UpdateActions();}
    }
    private async Task Restore()
    {
        if(busy||lastBackup is null)return;using var dialog=new SaveFileDialog(){Title="Restore original of "+UiLanguage.Data(Path.GetFileName(lastOutput))+" to a NEW file",FileName="restored-original.bin",Filter="Binary files (*.bin)|*.bin|All files (*.*)|*.*"};if(UiLanguage.Show(dialog,this)!=DialogResult.OK)return;busy=true;UpdateActions();try{var path=await Task.Run(()=>model.RestoreOriginal(lastBackup,dialog.FileName));status.Text="Original backup restored to new file: "+UiLanguage.Data(path);}catch(Exception e){ShowError(e);}finally{busy=false;UpdateActions();}
    }
    private async Task ExportDirectory()
    {
        using var dialog=new FolderBrowserDialog(){Description="Choose parent for a NEW directory of slot/system copies"};if(UiLanguage.Show(dialog,this)!=DialogResult.OK)return;
        string destination=Path.Combine(dialog.SelectedPath,"edited-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));busy=true;UpdateActions();
        try
        {
            var results=await Task.Run(()=>directoryModel.ExportCopies(destination));
            foreach(var result in results)if(result.Receipt is not null&&result.Label==$"Slot {directoryModel.SlotNumber}"){lastBackup=result.Receipt.Backup;lastOutput=result.OutputPath;}
            string report=string.Join("\n\n",results.Select(r=>r.Error is null?$"{r.Label}: completed\n{UiLanguage.Data(r.OutputPath)}\nOriginal backup: {UiLanguage.Data(r.Receipt!.BackupPath)}":$"{r.Label}: FAILED\n{UiLanguage.Data(r.Error)}"));
            status.Text="Directory-copy export finished. Check each file result; this is not an atomic multi-file transaction.";
            UiLanguage.Message(this,report+"\n\nPer-file completion only. Game acceptance unverified.","Directory results",MessageBoxButtons.OK,results.Any(r=>r.Error!=null)?MessageBoxIcon.Warning:MessageBoxIcon.Information);
        }
        catch(Exception e){ShowError(e);}finally{busy=false;UpdateActions();}
    }
    internal bool SmokeCandidateGate()=>domainPages.SmokeCandidateGate();
    internal bool SmokeRawReadOnly()=>hex.ReadOnly&&!apply.Visible;
    internal bool SmokeOpenEntryVisibility(bool hasContext)=>Controls.Find("ToolbarOpenFile",true).Single().Visible==hasContext&&Controls.Find("ToolbarOpenFolder",true).Single().Visible==hasContext&&Controls.Find("ValidationEntry",true).Single().Visible;
    internal bool SmokeLanguageSwitch()
    {
        int value=directoryModel.Domain!.Character().Money!.Value;directoryModel.SetMoney(value-1);int count=directoryModel.StagedCount;string selection=domainPages.SmokeSelectionIdentity();var instance=directoryModel;string raw=hex.Text;
        SetSmokeLanguage("zh-CN");SetSmokeLanguage("en");bool ok=ReferenceEquals(instance,directoryModel)&&directoryModel.StagedCount==count&&directoryModel.Domain.Character().Money==value-1&&selection==domainPages.SmokeSelectionIdentity()&&hex.Text==raw;directoryModel.Undo();return ok;
    }
    internal void SetSmokeLanguage(string language){var control=Controls.Find("LanguageChoice",true).OfType<ComboBox>().Single();control.SelectedIndex=language=="zh-CN"?1:0;UiLanguage.Set(language);}
    internal void LoadSyntheticForSmoke(string path){directoryModel.OpenSlot(path);model=directoryModel.Slot;domainPages.Bind(directoryModel);candidates=[new SlotCandidate(null,"Synthetic file",path,SlotStatus.Recognized,"Synthetic smoke only")];FillSlots();displayed=model.Browse();grid.DataSource=displayed.Rows.ToList();pageInfo.Text=$"Page 1 · {displayed.Total} roots";status.Text="Synthetic fixture loaded for local UI smoke.";summary.Text="Synthetic smoke fixture — no real save loaded";UpdateActions();}
    internal void SelectDomainSmokeTab(int index){mainTabs.SelectedIndex=index;}
    internal bool SmokeSelectionRetention()=>domainPages.SmokeSelectionRetention();
    internal IReadOnlyList<object> SmokeActionLayout(){var rows=new List<object>();void Walk(Control parent){foreach(Control c in parent.Controls){if(c is Button&&c.Visible){var p=c.Parent!;rows.Add(new{text=c.Text,fullyInsideParent=c.Top>=0&&c.Bottom<=p.ClientSize.Height&&c.Left>=0&&c.Right<=p.ClientSize.Width,bounds=c.Bounds.ToString(),parentSize=p.ClientSize.ToString()});}Walk(c);}}Walk(this);return rows;}
    internal IReadOnlyList<string> SmokeTabs(){var names=new List<string>();foreach(TabPage tab in mainTabs.TabPages)names.Add(tab.Text);return names;}
    internal void SelectSmokeTab(int index){mainTabs.SelectedIndex=index==1?3:index;}
    private void ShowError(Exception e){status.Text=UiLanguage.Data(e.Message);UiLanguage.Message(this,UiLanguage.Data(e.Message+(e.InnerException is null?"":"\n\n"+e.InnerException.Message)),"Operation not completed",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    protected override void Dispose(bool disposing)
    {
        if(disposing)
        {
            filterCancellation?.Cancel();
            queryGeneration++;
            debounce.Stop();
            debounce.Dispose();
            // Its pages are reparented into mainTabs, so this controller is an
            // owned component rather than a child in the window's control tree.
            domainPages?.Dispose();
        }
        base.Dispose(disposing);
    }
}
