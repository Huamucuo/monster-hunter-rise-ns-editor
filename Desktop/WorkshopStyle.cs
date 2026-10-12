namespace RiseWorkshopDesktop;
internal static class WorkshopStyle
{
    internal static readonly Color Canvas=Color.FromArgb(242,245,249),Ink=Color.FromArgb(35,49,68),Accent=Color.FromArgb(43,91,147);
    internal static void Apply(Control root)
    {
        root.ForeColor=Ink;
        if(root is Form)root.BackColor=Canvas;
        else if(root is TabPage||root is FlowLayoutPanel||root is TableLayoutPanel||root is Panel)root.BackColor=Color.White;
        if(root is Button b){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(210,219,229);b.BackColor=Color.White;b.Padding=new Padding(9,4,9,4);b.Margin=new Padding(4);b.AutoSize=true;bool primary=b.Name is "PrimaryOpen" or "PrimaryExport";void State(){b.BackColor=!b.Enabled?Color.FromArgb(240,243,247):primary?Accent:Color.White;b.ForeColor=!b.Enabled?Color.FromArgb(142,151,164):primary?Color.White:Ink;}b.EnabledChanged+=(_,_)=>State();State();}
        if(root is TextBox text)text.BorderStyle=BorderStyle.FixedSingle;
        if(root is ListBox list){list.BorderStyle=BorderStyle.None;list.BackColor=Color.White;}
        if(root is DataGridView grid){grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.GridColor=Color.FromArgb(228,234,241);grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(235,241,248);grid.ColumnHeadersDefaultCellStyle.ForeColor=Ink;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(235,241,248);grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(222,235,250);grid.DefaultCellStyle.SelectionForeColor=Ink;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(248,250,253);grid.RowTemplate.Height=34;grid.ColumnHeadersHeight=38;}
        if(Equals(root.Tag,"Canvas"))root.BackColor=Canvas;
        foreach(Control c in root.Controls)Apply(c);
    }
    internal static FlowLayoutPanel Card(string title,params Control[] contents)
    {
        var card=new FlowLayoutPanel(){AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(18),Margin=new Padding(0,0,0,14),BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
        card.Controls.Add(new Label(){Text=title,AutoSize=true,Font=new Font("Microsoft YaHei UI",11,FontStyle.Bold),Margin=new Padding(0,0,0,12)});foreach(var c in contents)card.Controls.Add(c);return card;
    }
}



