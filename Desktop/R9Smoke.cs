using FreshFoundation;
using System.Drawing.Imaging;
using System.Text.Json;
namespace RiseWorkshopDesktop;
internal static class R9Smoke
{
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);string input=Path.Combine(folder,"synthetic.bin");File.WriteAllBytes(input,WorkspaceSyntheticSamples.Slot(transferFixture:true));var editor=new DirectoryEditor();editor.OpenSlot(input);var names=new CatalogProviderNames(()=>UiLanguage.Current);names.ShowUnverifiedCandidates=true;names.SetProvider(new LocalGameCatalogProvider(new GameCatalog([new(GameCatalogKind.Skill,2,new(){["en"]="SYNTHETIC extraordinarily long candidate skill name for layout validation",["zh-CN"]="合成超长技能名称用于布局测试"},Origin:new("synthetic","synthetic")),new(GameCatalogKind.Decoration,11,new(){["en"]="SYNTHETIC extraordinarily long decoration candidate for layout validation",["zh-CN"]="合成装饰珠超长候选名称"},Origin:new("synthetic","synthetic"))])));var results=new List<object>();
        names.ShowUnverifiedCandidates=false;string hidden=names.Describe(GameCatalogKind.Skill,2);bool hiddenNames=!hidden.Contains("SYNTHETIC extraordinarily")&&hidden.Contains("Source:")&&hidden.Contains("License:")&&hidden.Contains("ID=2")&&hidden.Contains("UNVERIFIED_MAPPING");names.ShowUnverifiedCandidates=true;bool visibleNames=names.Describe(GameCatalogKind.Skill,2).Contains("SYNTHETIC extraordinarily");using(var host=new Form(){Size=new(840,650),ShowInTaskbar=false,Opacity=0}){var pages=new DomainPages(()=>{});host.Controls.Add(pages);pages.Bind(editor);UiLanguage.Attach(pages);host.Show();Application.DoEvents();bool localized=pages.SmokeReviewLocalization();results.Add(new{reviewLocalization=localized,hiddenNames,visibleNames});host.Close();if(!localized||!hiddenNames||!visibleNames)throw new Exception("Review gate/localization check failed");}
        foreach(string language in new[]{"en","zh-CN"}){
            UiLanguage.Set(language,false);
            foreach(int index in new[]{1,2,3}){
                using var dialog=new EquipmentDraftForm(editor,index,names){ShowInTaskbar=false,Opacity=0,Size=new(780,580)};dialog.Show();Application.DoEvents();Render(dialog,$"{language}-equipment-{index}-original.png");int before=editor.StagedCount;dialog.ClearDraft();bool widgetsDoNotStage=before==editor.StagedCount;Render(dialog,$"{language}-equipment-{index}.png");dialog.Close();bool cancelPreserves=before==editor.StagedCount;
                using var apply=new EquipmentDraftForm(editor,index,names);apply.ClearDraft();apply.ApplyDraft();bool applies=editor.Domain!.DecorationIds(index).All(v=>v==0);editor.Undo();bool undoes=editor.StagedCount==before;results.Add(new{language,index,widgetsDoNotStage,cancelPreserves,applies,undoes});
            }
            using var transfer=new InventoryTransferForm(editor,0){ShowInTaskbar=false,Opacity=0};transfer.Size=transfer.MinimumSize;transfer.Show();Application.DoEvents();Render(transfer,$"{language}-transfer.png");transfer.Close();
            string missing=Path.Combine(folder,"missing.bin");File.WriteAllBytes(missing,WorkspaceSyntheticSamples.Slot(decorationCount:2));var partial=new DirectoryEditor();partial.OpenSlot(missing);using var missingForm=new EquipmentDraftForm(partial,3,names){ShowInTaskbar=false,Opacity=0};missingForm.Show();Application.DoEvents();Render(missingForm,$"{language}-missing-decoration.png");missingForm.Close();
        }
        File.WriteAllText(Path.Combine(folder,"r9-ui-results.json"),JsonSerializer.Serialize(new{atUtc=DateTimeOffset.UtcNow,scope="Synthetic fixtures only; 144 DPI available host; no game acceptance",results},new JsonSerializerOptions{WriteIndented=true}));
        void Render(Form form,string file){using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(folder,file),ImageFormat.Png);}
    }
}
