using FreshFoundation;
using System.Drawing.Imaging;
using System.Text.Json;
namespace RiseWorkshopDesktop;
internal static class R10Smoke
{
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);var checks=new List<object>();
        foreach(string language in new[]{"en","zh-CN"}){
            UiLanguage.Set(language,false);var catalog=R10Checks.Catalog();
            using(var form=new BuildForm(catalog){ShowInTaskbar=false,Opacity=0,Size=new(900,600)}){form.Show();Application.DoEvents();Render(form,language+"-build-empty.png");form.SmokeRoundtrip(Path.Combine(folder,language+"-plan.json"));Application.DoEvents();Render(form,language+"-build-selected.png");form.Close();}
            var planner=new BuildPlanner(catalog){ShowCandidates=true};foreach(var pos in new[]{BuildPosition.Weapon,BuildPosition.Head,BuildPosition.Talisman}){using var form=new BuildPieceForm(planner,pos){ShowInTaskbar=false,Opacity=0,Size=new(840,620)};form.Show();Application.DoEvents();bool canceled=form.SmokeCancelKeepsPlan();form.SmokeConfirm();bool virtualControls=pos!=BuildPosition.Talisman||form.SmokeVirtual();bool relatedSearch=form.SmokeSkillSearch();Application.DoEvents();Render(form,language+"-piece-"+pos+".png");checks.Add(new{language,position=pos.ToString(),cancelKeptPlan=canceled,virtualControls,relatedSearch,deviceDpi=form.DeviceDpi});form.Close();}
            planner.Confirm(new(BuildPosition.Legs,999999,[123,0,0]));checks.Add(new{language,unresolvedVisible=planner.Summarize().Partial});
            using var browser=new CatalogForm(catalog,new DirectoryEditor()){ShowInTaskbar=false,Opacity=0};browser.Show();Application.DoEvents();browser.SmokeSetCandidates(true);Render(browser,language+"-catalog-reference.png");browser.Close();
        }
        string synthetic=Path.Combine(folder,"history.bin");File.WriteAllBytes(synthetic,WorkspaceSyntheticSamples.Slot());var history=new DirectoryEditor();history.OpenSlot(synthetic);history.SetMoney(1500);foreach(string language in new[]{"en","zh-CN"}){UiLanguage.Set(language,false);using var changes=new ChangesForm(history){ShowInTaskbar=false,Opacity=0,Size=new(760,480)};changes.Show();Application.DoEvents();Render(changes,language+"-staged-changes.png");changes.Close();}
        File.WriteAllText(Path.Combine(folder,"r10-ui-results.json"),JsonSerializer.Serialize(new{atUtc=DateTimeOffset.UtcNow,scope="Synthetic only; local plan files only",checks},new JsonSerializerOptions{WriteIndented=true}));
        void Render(Form form,string file){using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(folder,file),ImageFormat.Png);}
    }
}
