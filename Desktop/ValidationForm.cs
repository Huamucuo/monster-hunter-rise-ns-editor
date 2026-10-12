using FreshFoundation;
namespace RiseWorkshopDesktop;
internal sealed class ValidationForm : Form
{
    private readonly TextBox log=new(){Tag="RawContent",Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    private readonly Button prepare=new(){Text="Prepare cases from a plan…",AutoSize=true},verify=new(){Text="Check returned game save…",AutoSize=true};
    private readonly NumericUpDown slot=new(){Minimum=1,Maximum=3,Value=2,Width=65};
    public ValidationForm()
    {
        Text="Game validation";Size=new Size(820,540);MinimumSize=new Size(650,430);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);
        var panel=new TableLayoutPanel(){Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(12)};panel.RowStyles.Add(new(SizeType.AutoSize));panel.RowStyles.Add(new(SizeType.AutoSize));panel.RowStyles.Add(new(SizeType.Percent,100));Controls.Add(panel);
        var intro=new Label(){AutoSize=true,MaximumSize=new Size(750,0),Text="Prepare independent test copies from a hash-bound plan. Each case starts from the original folder. This window does not operate the game. Record actual game observations in the generated human-report.json, then check a separately returned four-file bundle. Unknown answers stay pending; only scoped evidence can support a verification recommendation."};panel.Controls.Add(intro,0,0);
        var actions=new FlowLayoutPanel(){Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};actions.Controls.AddRange([new Label(){Text="Slot",AutoSize=true},slot,prepare,verify]);panel.Controls.Add(actions,0,1);panel.Controls.Add(log,0,2);
        prepare.Click+=async(_,_)=>await Prepare();verify.Click+=async(_,_)=>await Verify();
        log.Text="Plan format and CLI examples: GAME_VALIDATION.md\r\nNo save is loaded automatically. Validation uses its own fresh selections and does not export the current editor's staged work.";
    }
    protected override void OnShown(EventArgs e){base.OnShown(e);log.Text=UiLanguage.T(log.Text);UiLanguage.Attach(this);}
    private string? FileChoice(string title){using var d=new OpenFileDialog(){Title=title,Filter="JSON (*.json)|*.json"};return UiLanguage.Show(d,this)==DialogResult.OK?d.FileName:null;}
    private string? FolderChoice(string title){using var d=new FolderBrowserDialog(){Description=title};return UiLanguage.Show(d,this)==DialogResult.OK?d.SelectedPath:null;}
    private async Task Work(Func<string> run){prepare.Enabled=verify.Enabled=false;try{log.Text=UiLanguage.T("Working…");log.Text=UiLanguage.T(await Task.Run(run));}catch(Exception e){log.Text=UiLanguage.T("Not completed: ")+e.Message;}finally{prepare.Enabled=verify.Enabled=true;}}
    private async Task Prepare()
    {
        string? source=FolderChoice("Source folder with all four files");if(source is null)return;string? plan=FileChoice("Choose a hash-bound case plan");if(plan is null)return;string? parent=FolderChoice("Parent for a new validation run");if(parent is null)return;int selected=(int)slot.Value;string output=Path.Combine(parent,"validation-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        await Work(()=>{var paths=GameValidation.Prepare(source,selected,GameValidation.Read<ValidationPlan>(plan),output);return $"EXPORTED: {paths.Count} independent cases including baseline.\r\n{UiLanguage.Data(output)}\r\n\r\nOnly each case's import folder is the four-file game bundle. Manifests, backups and blank human reports stay beside it. No gameplay success has been recorded.";});
    }
    private async Task Verify()
    {
        string? manifest=FileChoice("Original case manifest");if(manifest is null)return;string? report=FileChoice("Completed human game report");if(report is null)return;string? returned=FolderChoice("Fresh returned four-file game save folder");if(returned is null)return;using var output=new SaveFileDialog(){Title="Create a NEW verification result",Filter="JSON (*.json)|*.json",FileName="verification-result-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"};if(UiLanguage.Show(output,this)!=DialogResult.OK)return;string destination=output.FileName;
        await Work(()=>{var result=GameValidation.VerifyReturn(manifest,report,returned,destination);return result.Status+"\r\n"+UiLanguage.Data(string.Join("\r\n",result.Comparisons))+"\r\n\r\n"+result.EvidenceLabel+"\r\nResult: "+UiLanguage.Data(destination);});
    }
}


