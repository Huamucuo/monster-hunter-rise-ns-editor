using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace RiseWorkshopDesktop;
internal static class UiLanguage
{
    private sealed class State { public string Source=""; public string Placeholder=""; public bool Updating; }
    private static readonly ConditionalWeakTable<Control,State> states=new();
    private static readonly List<WeakReference<Control>> controls=[];
    internal static string Current {get;private set;}="en";
    internal static string SettingsPath {get;private set;}="";
    internal static event Action? Changed;
    internal static void Initialize(string? testPath=null)
    {
        SettingsPath=testPath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RiseStructuralWorkshop","preferences.json");Current="en";
        try{if(File.Exists(SettingsPath)&&JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(SettingsPath))?.GetValueOrDefault("language")=="zh-CN")Current="zh-CN";}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){}
    }
    internal static void Set(string language,bool persist=true)
    {
        if(language is not ("en" or "zh-CN"))throw new ArgumentException("Unsupported language.");
        if(persist){Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);File.WriteAllText(SettingsPath,JsonSerializer.Serialize(new{language}));}
        Current=language;
        foreach(var weak in controls.ToArray())if(weak.TryGetTarget(out var c)&&!c.IsDisposed){Apply(c);c.Invalidate();}Changed?.Invoke();
    }
    internal static string Data(string? value)=>"\uE000"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value??""))+"\uE001";
    private static string Restore(string text)=>Regex.Replace(text,"\\uE000([A-Za-z0-9+/=]*)\\uE001",m=>System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[1].Value)));
    internal static string T(string text)
    {
        if(Current!="zh-CN"||text.Length==0)return Restore(text);if(Words.TryGetValue(text,out string? translated))return translated;
        foreach(var pair in Fragments.OrderByDescending(p=>p.Key.Length))text=text.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
        text=Regex.Replace(text,@"\bSlot (\d+)\b","角色 $1");text=Regex.Replace(text,@"\bSkill (\d+) / (?:level|等级) (\d+)\b","技能 $1 / 等级 $2");text=Regex.Replace(text,@"\b(Weapon|Armor|Talisman|Item) (\d+)\b",m=>(m.Groups[1].Value switch{"Weapon"=>"武器","Armor"=>"防具","Talisman"=>"护石",_=>"物品"})+" "+m.Groups[2].Value);return Restore(text);
    }
    internal static void Attach(Control root)
    {
        if(states.TryGetValue(root,out _))return;
        // Editable data, including raw hex and search input, must never be translated.
        if(root is not ComboBox && root is not ListBox && !Equals(root.Tag,"RawContent"))
        {
            var state=new State{Source=root.Text,Placeholder=(root as TextBox)?.PlaceholderText??""};states.Add(root,state);controls.Add(new(root));
            root.TextChanged+=(_,_)=>{if(root is TextBox input&&!input.ReadOnly)return;if(!state.Updating){state.Source=root.Text;Apply(root);}};
        }
        if(root is ComboBox||root is ListBox)controls.Add(new(root));
        if(root is ComboBox combo&&combo.DropDownStyle==ComboBoxStyle.DropDownList){combo.DrawMode=DrawMode.OwnerDrawFixed;combo.DrawItem+=(_,e)=>{e.DrawBackground();if(e.Index>=0)TextRenderer.DrawText(e.Graphics,T(combo.GetItemText(combo.Items[e.Index])??""),e.Font,e.Bounds,e.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};}
        if(root is ListBox list){list.DrawMode=DrawMode.OwnerDrawFixed;list.DrawItem+=(_,e)=>{e.DrawBackground();if(e.Index>=0)TextRenderer.DrawText(e.Graphics,T(list.GetItemText(list.Items[e.Index])??""),e.Font,e.Bounds,e.ForeColor,TextFormatFlags.VerticalCenter);e.DrawFocusRectangle();};}
        if(root is DataGridView grid){grid.CellPainting+=(_,e)=>{if(e.RowIndex==-1&&e.ColumnIndex>=0&&e.Graphics is not null){e.PaintBackground(e.ClipBounds,true);TextRenderer.DrawText(e.Graphics,T(grid.Columns[e.ColumnIndex].HeaderText),grid.Font,e.CellBounds,grid.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.Handled=true;}};grid.CellFormatting+=(_,e)=>{if(e.RowIndex>=0&&grid.Columns[e.ColumnIndex].Name is "DisplayName" or "DisplayType"&&e.Value is string text)e.Value=T(text);};}
        root.ControlAdded+=(_,e)=>{if(e.Control is not null)Attach(e.Control);};foreach(Control c in root.Controls)Attach(c);Apply(root);
    }
    private static void Apply(Control c){if(!states.TryGetValue(c,out var s))return;s.Updating=true;try{if(c is TextBox box){box.PlaceholderText=T(s.Placeholder);if(box.ReadOnly)box.Text=T(s.Source);}else c.Text=T(s.Source);}finally{s.Updating=false;}}
    internal static DialogResult Message(IWin32Window owner,string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon)=>MessageBox.Show(owner,T(text),T(caption),buttons,icon);
    internal static DialogResult Show(CommonDialog dialog,IWin32Window owner)
    {
        if(dialog is FileDialog f){f.Title=T(f.Title);f.Filter=T(f.Filter);}if(dialog is FolderBrowserDialog folder)folder.Description=T(folder.Description);return dialog.ShowDialog(owner);
    }
    private static readonly Dictionary<string,string> Words=new(StringComparer.Ordinal)
    {
        ["Decoration references (draft clear/restore): "]="装饰珠引用（草稿清除/恢复）：",["Use the equipment draft to edit both observed skill pairs. Socket capacities remain unknown."]="在装备草稿中编辑两组已观察技能配对。槽容量仍未知。",["Split / move / merge…"]="拆分 / 移动 / 合并…",["Edit equipment draft…"]="编辑装备草稿…",["Redo"]="重做",["Staged changes…"]="暂存修改…",["File"]="文件",["Capability"]="修改项目",["Original"]="原始值",["Current"]="当前值",["Equipment information…"]="装备信息…",["Declared skill level"]="目录声明技能上限",["Declared slot requirement"]="目录声明槽需求",["Skill relations"]="技能关系",["Source"]="来源",["License"]="许可",["Edition"]="版本声明",["Catalog declarations are not independent verification."]="目录声明不等于独立核验。",["Kind"]="种类",["Category"]="分组",["Owned"]="已有",["Name"]="名称",
        ["Catalog values are unverified claims. Source-based conservative bounds still apply. Decoration filtering is read-only."]="目录数值只是未经核验的声明；仍受原始存档保守范围限制。装饰珠筛选仅供查看。",["Item"]="物品",["Skill"]="技能",["Decoration"]="装饰珠",["Rampage"]="百龙",["Select a primitive occurrence to inspect its bytes"]="选择基础值节点以查看原始字节",["Read-only primitive bytes"]="只读原始字节",
        ["Show unverified name candidates"]="显示未验证名称候选",
        ["Names and sources…"]="名称与来源…",
        ["Local catalog"]="本地名称目录",["Load local catalog…"]="加载本地名称目录…",["Choose a local directory containing catalog.json"]="选择含 catalog.json 的本地目录",["Name or numeric ID"]="名称或数字 ID",["All categories"]="全部类别",["All groups"]="全部分组",["Owned only"]="仅已拥有",["Sort by ID"]="按 ID 排序",["Sort by name"]="按名称排序",["Declared maximum"]="目录声明上限",["Original total"]="原始总量",
        ["Start with a save copy"]="从存档副本开始",["Open a folder to select a character, or open one file.\nYour source stays unchanged. Review staged edits, then export a new copy."]="打开文件夹选择角色，或打开单个文件。\n原存档保持不变。确认暂存修改后，再导出新副本。",["Character overview"]="角色概览",["Currency"]="货币",["Money"]="金钱",
        ["Rise Structural Workshop — Experimental"]="崛起存档工坊 — 实验版",["Open file…"]="打开文件…",["Open folder…"]="打开文件夹…",["Export copy…"]="导出副本…",["Restore backup…"]="恢复备份…",["Open export"]="打开导出副本",["Undo"]="撤销",["Clear staged"]="清除暂存",["Game validation…"]="游戏验证…",["Double-click a slot to open"]="双击角色以打开",["Character, inventory, equipment"]="角色、物品与装备",["Structure / raw research"]="结构 / 原始数据研究",["Character / currency"]="角色 / 货币",["Inventory"]="物品箱",["Equipment / talismans"]="装备 / 护石",["Roots"]="根节点",["Up"]="上一级",["Previous"]="上一页",["Next"]="下一页",["Root records"]="根记录",["No file selected"]="尚未选择文件",["Global search"]="全局搜索",["Apply to staging"]="应用到暂存",["Selected occurrence / local text"]="选中节点 / 本地文本",["Research-only primitive hex (exact width)"]="仅供研究：定宽十六进制值",["Author: 花木错"]="作者：花木错",
        ["Stage money"]="暂存金钱",["Stage points"]="暂存点数",["Points"]="点数",["Money (staged changes are saved only when you export a copy)."]="金钱（暂存修改仅在导出副本时保存）",["Show empty records"]="显示空记录",["Quantity"]="数量",["Apply quantity"]="应用数量",["Delete / empty"]="删除 / 清空",["Add known ID"]="添加已知 ID",["Show empty equipment"]="显示空装备",["All types"]="全部类型",["Empty"]="空",["Weapon"]="武器",["Armor"]="防具",["Talisman"]="护石",["Unknown"]="未知",["Skill 1"]="技能 1",["Skill 2"]="技能 2",["Stage observed pair"]="暂存已知技能组合",["Read-only fields…"]="只读字段…",["Read-only equipment fields"]="装备只读字段",
        ["Record"]="记录",["Item name"]="物品名称",["Equipment name"]="装备名称",["Equipment"]="装备",["Item ID"]="物品 ID",["Equipment ID"]="装备 ID",["Type"]="类型",["Availability"]="可用性",["Event"]="事件",["Opaque ID"]="字段 ID",["Tag"]="标签",["Width"]="宽度",["Coordinate"]="坐标",["Children"]="子节点",["Raw hex"]="原始十六进制",["Raw interpretations (not game meanings)"]="原始数值解释（非游戏含义）",["UTF-16 text"]="UTF-16 文本",["Field"]="字段",["Identifier"]="标识符",["Count"]="数量",["Values"]="数值",["Meaning"]="含义",["Status"]="状态",
        ["Game validation"]="游戏验证",["Slot"]="角色",["Prepare cases from a plan…"]="按计划准备测试副本…",["Check returned game save…"]="检查游戏回存…",["Working…"]="处理中…",["Source folder with all four files"]="包含全部四个文件的原始文件夹",["Choose a hash-bound case plan"]="选择绑定源文件哈希的测试计划",["Parent for a new validation run"]="选择新验证批次的父文件夹",["Original case manifest"]="原始测试清单",["Completed human game report"]="填写后的人工游戏报告",["Fresh returned four-file game save folder"]="游戏重新保存后导出的四文件存档文件夹",["Create a NEW verification result"]="创建新的验证结果文件",
        ["Choose a save file explicitly"]="选择存档文件",["Select a directory containing slot files (no recursive scan)"]="选择包含角色存档的文件夹（不扫描子文件夹）",["Export to a NEW file"]="导出到新文件",["Choose parent for a NEW directory of slot/system copies"]="选择新角色 / 系统副本文件夹的父目录",["Staged changes"]="暂存修改",["Discard staged changes and close?"]="放弃暂存修改并关闭？",["Discard current staged changes to open another file?"]="放弃当前暂存修改并打开另一文件？",["Operation not completed"]="操作未完成",["Change not staged"]="修改未暂存",["Export completed"]="导出完成",["Directory results"]="文件夹导出结果",
        ["Search opaque ID or text"]="搜索字段 ID 或文本",["Filter numeric item ID"]="筛选物品数字 ID",["Filter equipment ID"]="筛选装备 ID",["Select a primitive occurrence to stage exact-width hexadecimal bytes"]="选择基础值节点，暂存等宽十六进制字节",
        ["Open a folder or file. No previous save is loaded."]="请打开文件夹或文件。未自动载入以前的存档。",["Experimental structural editor. Game identity, meanings and game acceptance are unverified."]="实验性编辑器：游戏身份、字段含义与游戏内接受情况尚未验证。",["Select a recognized candidate explicitly."]="请选择已识别的文件。",["Inspecting four known filename candidates…"]="正在检查四个已知文件名…",["Double-click a recognized candidate to select it. Other candidates remain independent."]="双击已识别的文件进行选择；其他文件的状态独立。",["Reading explicitly selected file…"]="正在读取所选文件…",["Selected. Double-click rows with children to browse; search checks all IDs and text."]="已选中。双击含子节点的记录继续浏览；搜索覆盖全部 ID 与文本。",["Primitive change staged. Source file is unchanged."]="基础值修改已暂存，原文件未改变。",["Validating staging, backing up original, exporting new copy…"]="正在校验暂存修改、备份原件并导出新副本…",["Directory-copy export finished. Check each file result; this is not an atomic multi-file transaction."]="文件夹副本导出结束。请逐项检查结果；多个文件不会作为整体同时成功或失败。",["Open a recognized slot to read available named fields."]="请打开已识别的角色文件以查看字段。",
        ["Numeric IDs are shown until item names are available. Add places an item already known in this file into a free inventory record. Delete empties the selected record."]="暂无名称时显示数字 ID。添加功能将本文件中已有的物品放入空记录；删除功能清空所选记录。",
        ["Prepare independent test copies from a hash-bound plan. Each case starts from the original folder. This window does not operate the game. Record actual game observations in the generated human-report.json, then check a separately returned four-file bundle. Unknown answers stay pending; only scoped evidence can support a verification recommendation."]="按绑定哈希的计划生成独立测试副本，每项都从原始文件夹开始。本窗口不操作游戏。请在生成的 human-report.json 中填写实际游戏观察，再检查单独导出的四文件游戏回存。未知答案保持待确认；验证结论仅适用于明确的测试范围。"
    };
    private static readonly Dictionary<string,string> Fragments=new(StringComparer.Ordinal)
    {
        ["Decoration references (draft clear/restore): "]="装饰珠引用（草稿清除/恢复）：",["Use the equipment draft to edit both observed skill pairs. Socket capacities remain unknown."]="在装备草稿中编辑两组已观察技能配对。槽容量仍未知。",["Plan format and CLI examples: "]="计划格式与命令行示例：",["No save is loaded automatically. Validation uses its own fresh selections and does not export the current editor's staged work."]="不会自动加载存档。验证使用独立的新选择，不导出主编辑器当前的暂存修改。",[" independent cases including baseline."]=" 个独立测试（含基线）。",["Only each case's import folder is the four-file game bundle. Manifests, backups and blank human reports stay beside it. No gameplay success has been recorded."]="每项测试的 import 文件夹才是四文件游戏存档。清单、备份和人工报告位于其旁边。尚未记录任何游戏内成功证据。",["Not completed: "]="未完成：",["Result: "]="结果：",["Staged: "]="已暂存：",["Name: "]="角色名：",["Hunter rank: "]="猎人等级：",["Master rank: "]="大师等级：",["Money: "]="金钱：",["Points: "]="点数：",["Unknown / absent"]="未知 / 不存在",["Conservative changes cannot exceed original values; game maxima are unknown."]="保守修改不得超过原始值；游戏真实上限未知。",["Name and ranks are read-only."]="角色名与等级只读。",[" is unavailable for this file."]="在此文件中不可用。",["Directory context: primary money and corresponding system cached money will be staged together."]="文件夹模式：角色金钱与对应系统缓存金钱会一同暂存。",["Primary-only: system cached preview remains unchanged."]="仅角色文件：系统缓存预览保持不变。",["Primary-only: system cached preview not linked. "]="仅角色文件：未关联系统缓存预览。",["Decoration IDs (read-only): "]="装饰品 ID（只读）：",["Pair 1: ID "]="技能 1：ID ",["; Pair 2: ID "]="；技能 2：ID ",[" / level "]=" / 等级 ",["Choose an existing skill/level pair from this file. Other socket-related data is not editable."]="请选择本文件中已有的技能 / 等级组合。其他插槽相关数据不可编辑。",["Record "]="记录 ",[": Weapon, numeric ID "]=": 武器，数字 ID ",[": Armor, numeric ID "]=": 防具，数字 ID ",[": Talisman, numeric ID "]=": 护石，数字 ID ",["Page "]="第 ",[" matches"]=" 条结果",[" roots"]=" 个根节点",["Children of event "]="事件的子节点：",["Experimental editor — game acceptance is unverified. Changes are staged; export creates new copies."]="实验性编辑器：游戏内接受情况尚未验证。修改先暂存，导出时创建新副本。",["Experimental copy created:"]="已创建实验性副本：",["Verified-at-creation original backup:"]="创建时已校验的原始备份：",["Game acceptance remains unverified."]="游戏内接受情况尚未验证。",["Export completed; original preserved. Backup: "]="导出完成；原件保留。备份：",["Original backup restored to new file: "]="原始备份已恢复到新文件：",["Restore original of "]="恢复原始备份：",[" to a NEW file"]=" 到新文件",["Original backup: "]="原始备份：",[": completed"]="：已完成",["Per-file completion only. Game acceptance unverified."]="请以各文件结果为准。游戏内接受情况尚未验证。",["Binary save files (*.bin)"]="二进制存档 (*.bin)",["Binary files (*.bin)"]="二进制文件 (*.bin)",["All files (*.*)"]="所有文件 (*.*)",["Synthetic smoke fixture — no real save loaded"]="合成界面测试 — 未加载真实存档",["Synthetic fixture loaded for local UI smoke."]="已加载用于本地界面测试的合成数据。",["Synthetic file"]="合成文件",["Recognized"]="已识别"
    };
}
