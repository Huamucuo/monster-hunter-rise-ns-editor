using FreshFoundation;
namespace RiseWorkshopDesktop;
internal sealed class ChangesForm : Form
{
    internal ChangesForm(DirectoryEditor editor)
    {
        string L(string en,string zh)=>EquipmentDraftForm.L(en,zh);Text=L("Staged changes — read-only","暂存修改 — 只读");Size=new(1000,600);MinimumSize=new(760,480);Font=new("Microsoft YaHei UI",9.5f);StartPosition=FormStartPosition.CenterParent;var rows=editor.ReviewChanges();var root=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new(12)};root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));Controls.Add(root);root.Controls.Add(new Label(){AutoSize=true,Text=rows.Count==0?L("No staged differences.","没有暂存差异。"):L("Current differences from original. Opaque values are raw bytes only; this view cannot edit.","当前与原始值的差异。未知值仅显示原始字节，此窗口不可编辑。")},0,0);var grid=new DataGridView(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,RowHeadersVisible=false};grid.DataSource=rows.Select(r=>new{File=r.FileLabel,Capability=Local(r.Capability),Event=r.EventIndex,Field=r.Identifier,Original=Value(r,r.OriginalBytes),Current=Value(r,r.CurrentBytes),OriginalHex=Convert.ToHexString(r.OriginalBytes),CurrentHex=Convert.ToHexString(r.CurrentBytes)}).ToList();root.Controls.Add(grid,0,1);WorkshopStyle.Apply(this);UiLanguage.Attach(this);
        string Local(string text)=>UiLanguage.Current!="zh-CN"?text:text.Replace("Money","金钱").Replace("Points","点数").Replace("cached preview","缓存预览").Replace("Inventory","物品记录").Replace("Quantity","数量").Replace("Equipment","装备记录").Replace("Decoration ID","装饰珠 ID").Replace("Talisman","护石").Replace("Skill ID","技能 ID").Replace("Skill level","技能等级").Replace("Raw / opaque (read-only)","原始 / 未知（只读）");
        string Value(StagedChange row,byte[] bytes)=>row.Capability.StartsWith("Raw")?"—":bytes.Length==1?bytes[0].ToString():bytes.Length==4?(row.Capability.Contains("Money")||row.Capability.Contains("Quantity")?BitConverter.ToInt32(bytes).ToString():BitConverter.ToUInt32(bytes).ToString()):"—";
    }
}
