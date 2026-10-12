using FreshFoundation;
namespace RiseWorkshopDesktop;
internal sealed class CatalogForm : Form
{
    private GameCatalog catalog;
    private readonly Action<GameCatalog>? onLocalLoaded;
    private readonly DirectoryEditor editor;
    private readonly HashSet<(GameCatalogKind,uint)> ownedKeys=[];
    private readonly DataGridView grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false};
    private readonly TextBox query=new(){Width=190,PlaceholderText="Name or numeric ID"};
    private readonly ComboBox kind=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=130},category=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=150},order=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=180};
    private readonly ComboBox slotFilter=new(){Width=130,DropDownStyle=ComboBoxStyle.DropDownList};
    private GameCatalogEntry[] shown=[];
    private readonly TextBox detail=new(){Tag="RawContent",Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    private readonly CheckBox candidates=new(){Text="Show unverified name candidates",AutoSize=true,Checked=false};
    private readonly CheckBox owned=new(){Text="Owned only",AutoSize=true};
    internal CatalogForm(GameCatalog catalog,DirectoryEditor editor,Action<GameCatalog>? onLocalLoaded=null)
    {
        this.catalog=catalog;this.editor=editor;this.onLocalLoaded=onLocalLoaded;var domain=editor.Domain;if(domain is not null){foreach(var item in domain.Inventory().Where(i=>i.Quantity>0&&i.Id!=null))ownedKeys.Add((GameCatalogKind.Item,item.Id!.Value));foreach(var pair in domain.KnownSkillPairs)ownedKeys.Add((GameCatalogKind.Skill,pair.Id));foreach(var equipment in domain.Equipment()){if(equipment.Id is uint id&&equipment.Type is 1 or 2)ownedKeys.Add((equipment.Type==1?GameCatalogKind.Weapon:GameCatalogKind.Armor,id));try{foreach(uint decoration in domain.DecorationIds(equipment.Index))ownedKeys.Add((GameCatalogKind.Decoration,decoration));}catch{}}}Text="Local catalog";Size=new Size(1100,650);MinimumSize=new Size(800,500);Font=new Font("Microsoft YaHei UI",9.5f);StartPosition=FormStartPosition.CenterParent;
        var root=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(12)};root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,160));root.RowStyles.Add(new(SizeType.AutoSize));Controls.Add(root);
        var tools=new FlowLayoutPanel(){Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};kind.Items.Add("All categories");foreach(var k in Enum.GetValues<GameCatalogKind>())kind.Items.Add(k);kind.SelectedIndex=0;category.Items.Add("All groups");foreach(string c in catalog.Entries.Select(e=>e.Category).OfType<string>().Distinct().Order())category.Items.Add(c);category.SelectedIndex=0;order.Items.AddRange(["Sort by ID","Sort by name"]);order.SelectedIndex=0;var local=new Button(){Text="Load local catalog…",AutoSize=true};local.Click+=(_,_)=>{using var pick=new FolderBrowserDialog(){Description="Choose a local directory containing catalog.json"};if(UiLanguage.Show(pick,this)!=DialogResult.OK)return;try{LoadLocalCatalog(pick.SelectedPath);category.Items.Clear();category.Items.Add("All groups");foreach(string group in this.catalog.Entries.Select(e=>e.Category).OfType<string>().Distinct().Order())category.Items.Add(group);category.SelectedIndex=0;RefreshRows();}catch(Exception e){UiLanguage.Message(this,UiLanguage.Data(e.Message),"Operation not completed",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};slotFilter.Items.Add(UiLanguage.Current=="zh-CN"?"全部需求槽":"Any slot");for(int n=1;n<=4;n++)slotFilter.Items.Add(n);slotFilter.SelectedIndex=0;slotFilter.SelectedIndexChanged+=(_,_)=>RefreshRows();tools.Controls.AddRange([query,kind,category,owned,order,slotFilter,candidates,local]);category.Enabled=false;slotFilter.Enabled=false;candidates.CheckedChanged+=(_,_)=>{category.Enabled=slotFilter.Enabled=candidates.Checked;if(!candidates.Checked){category.SelectedIndex=0;slotFilter.SelectedIndex=0;}RefreshRows();};root.Controls.Add(tools,0,0);root.Controls.Add(grid,0,1);root.Controls.Add(detail,0,2);root.Controls.Add(new Label(){AutoSize=true,MaximumSize=new Size(1000,0),Text="Catalog values are unverified claims. Source-based conservative bounds still apply. Decoration filtering is read-only."},0,3);
        grid.CurrentCellChanged+=(_,_)=>ShowDetail();grid.CellFormatting+=(_,e)=>{if(e.Value is string text)e.Value=UiLanguage.T(text);};query.TextChanged+=(_,_)=>RefreshRows();kind.SelectedIndexChanged+=(_,_)=>RefreshRows();category.SelectedIndexChanged+=(_,_)=>RefreshRows();owned.CheckedChanged+=(_,_)=>RefreshRows();order.SelectedIndexChanged+=(_,_)=>RefreshRows();RefreshRows();WorkshopStyle.Apply(this);UiLanguage.Attach(this);
    }
    internal void LoadLocalCatalog(string path){var loaded=GameCatalog.LoadDirectory(path,()=>UiLanguage.Current);onLocalLoaded?.Invoke(loaded);catalog=loaded;}
    internal bool SmokeCandidateSearch()
    {
        var entry=catalog.Entries.First(e=>e.Kind==GameCatalogKind.Item);candidates.Checked=false;query.Text=entry.Id.ToString();int index=Array.FindIndex(shown,e=>e.Kind==entry.Kind&&e.Id==entry.Id);bool raw=index>=0&&Convert.ToString(grid.Rows[index].Cells["Name"].FormattedValue)==$"{entry.Kind} {entry.Id}";candidates.Checked=true;query.Text=entry.Name(UiLanguage.Current);index=Array.FindIndex(shown,e=>e.Kind==entry.Kind&&e.Id==entry.Id);bool named=index>=0&&Convert.ToString(grid.Rows[index].Cells["Name"].FormattedValue)!.Contains(entry.Name(UiLanguage.Current));return raw&&named;
    }
    internal void SmokeSetCandidates(bool show){candidates.Checked=show;}
    private bool IsOwned(GameCatalogEntry e)=>ownedKeys.Contains((e.Kind,e.Id));
    private void RefreshRows()
    {
        var rows=catalog.Entries.Where(e=>(kind.SelectedItem is not GameCatalogKind k||e.Kind==k)&&(!candidates.Checked||category.SelectedIndex<=0||e.Category==category.SelectedItem as string)&&($"{e.Id} 0x{e.Id:X8}".Contains(query.Text,StringComparison.OrdinalIgnoreCase)||candidates.Checked&&(e.Names.Values.Any(n=>n.Contains(query.Text,StringComparison.OrdinalIgnoreCase))||e.Kind==GameCatalogKind.Decoration&&(e.SkillRelations??[]).Any(r=>catalog.Find(GameCatalogKind.Skill,r.SkillId)?.Names.Values.Any(n=>n.Contains(query.Text,StringComparison.OrdinalIgnoreCase))==true)))&&(!candidates.Checked||slotFilter.SelectedIndex<=0||e.Kind==GameCatalogKind.Decoration&&e.RequiredSlotLevel is >=1 and <=4&&e.RequiredSlotLevel<=slotFilter.SelectedIndex)).Where(e=>!owned.Checked||IsOwned(e));rows=candidates.Checked&&order.SelectedIndex==1?rows.OrderBy(e=>e.Name(UiLanguage.Current)):rows.OrderBy(e=>e.Id);
        shown=rows.ToArray();grid.DataSource=shown.Select(e=>new{Name=UiLanguage.Data(candidates.Checked?e.Name(UiLanguage.Current)+(UiLanguage.Current=="zh-CN"?" [未验证候选]":" [unverified candidate]"):$"{e.Kind} {e.Id}"),Id=e.Id,Kind=e.Kind.ToString(),Category=UiLanguage.Data(e.Category??""),Owned=IsOwned(e),Status=UiLanguage.Current=="zh-CN"?"未验证映射":"UNVERIFIED_MAPPING"}).ToList();ConfigureColumns();ShowDetail();

    }
    private void ConfigureColumns()
    {
        foreach(DataGridViewColumn column in grid.Columns){column.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;column.Width=110;}
        grid.Columns["Name"]!.Width=320;grid.Columns["Name"]!.MinimumWidth=280;grid.Columns["Name"]!.AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;grid.Columns["Kind"]!.Width=80;grid.Columns["Owned"]!.Width=60;grid.Columns["Status"]!.Width=180;grid.Columns["Category"]!.Visible=false;
    }
    private void ShowDetail()
    {
        int index=grid.CurrentRow?.Index??-1;if(index<0||index>=shown.Length){detail.Clear();return;}var e=shown[index];string original=e.Kind==GameCatalogKind.Item?editor.Domain?.OriginalItemTotalCeiling(e.Id)?.ToString()??"Unknown":"—";
        detail.Text=CatalogReferenceView.Describe(e,candidates.Checked,id=>catalog.Find(GameCatalogKind.Skill,id)?.Name(UiLanguage.Current)??"ID")+"\r\n"+UiLanguage.T("Original total")+": "+original;
    }}
