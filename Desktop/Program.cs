using System.Drawing.Imaging;
using System.Text.Json;
using FreshFoundation;
namespace RiseWorkshopDesktop;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Length==2&&args[0]=="--smoke-r11")
        {
            UiLanguage.Initialize(Path.GetFullPath(Path.Combine(args[1],"test-preferences.json")));
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            // The actual callback smoke needs the same persistent UI dispatcher
            // as normal startup. A Show/DoEvents-only runner loses its Windows
            // Forms synchronization context around nested modal message loops.
            using var pump=new Form(){ShowInTaskbar=false,Opacity=0,Size=new(100,100)};
            pump.Shown+=(_,_)=>pump.BeginInvoke((Action)(()=>
            {
                try{R11Smoke.Run(args[1]);}
                catch(Exception e){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"r11-smoke-error.txt"),e.ToString());Environment.ExitCode=1;}
                finally{pump.Close();}
            }));
            Application.Run(pump);
            return;
        }
        UiLanguage.Initialize(args.Length==2&&args[0] is "--smoke" or "--smoke-r9" or "--smoke-r10" or "--smoke-r10-review"?Path.GetFullPath(Path.Combine(args[1],"test-preferences.json")):null);
        if(args.Length==2&&args[0] is "--smoke" or "--smoke-r9" or "--smoke-r10" or "--smoke-r10-review") {Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);try{if(args[0]=="--smoke-r10-review")R10ReviewSmoke.Run(args[1]);else if(args[0]=="--smoke-r10")R10Smoke.Run(args[1]);else if(args[0]=="--smoke-r9")R9Smoke.Run(args[1]);else Smoke(args[1]);}catch(Exception e){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"smoke-error.txt"),e.ToString());Environment.ExitCode=1;}return;}
        Application.Run(new WorkshopForm());
    }
    private static void Smoke(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);string input=Path.Combine(outputDirectory,"synthetic-smoke.bin");File.WriteAllBytes(input,WorkspaceSyntheticSamples.Slot());
        var results=new List<object>();using var form=new WorkshopForm(){StartPosition=FormStartPosition.Manual,Location=new Point(0,0),ShowInTaskbar=false,Opacity=0};form.Show();bool emptyOpenEntries=form.SmokeOpenEntryVisibility(false);foreach(string emptyLanguage in new[]{"en","zh-CN"})foreach(var emptySize in new[]{new Size(1120,760),new Size(1500,940)}){form.SetSmokeLanguage(emptyLanguage);form.Size=emptySize;form.PerformLayout();Application.DoEvents();using var emptyImage=new Bitmap(form.Width,form.Height);form.DrawToBitmap(emptyImage,new Rectangle(Point.Empty,form.Size));emptyImage.Save(Path.Combine(outputDirectory,$"{emptyLanguage}-{emptySize.Width}x{emptySize.Height}-empty.png"),ImageFormat.Png);}form.SetSmokeLanguage("en");form.LoadSyntheticForSmoke(input);Application.DoEvents();bool loadedOpenEntries=form.SmokeOpenEntryVisibility(true);bool ordinaryRawReadOnly=form.SmokeRawReadOnly();form.SelectDomainSmokeTab(2);Application.DoEvents();bool selectionRetained=form.SmokeSelectionRetention();bool languageSwitchPreservedStaging=form.SmokeLanguageSwitch();bool candidateGatePreservedStaging=form.SmokeCandidateGate();UiLanguage.Set("zh-CN");string preferencePath=UiLanguage.SettingsPath;UiLanguage.Initialize(preferencePath);bool preferenceRemembered=UiLanguage.Current=="zh-CN";UiLanguage.Initialize(Path.Combine(outputDirectory,"missing-preferences.json"));bool defaultEnglish=UiLanguage.Current=="en";UiLanguage.Initialize(preferencePath);
        UiLanguage.Set("zh-CN");string rawData=@"C:\SYNTHETIC\Weapon 123 Item 7 Name: Hunter.bin";bool rawDataPreserved=UiLanguage.T("Name: "+UiLanguage.Data(rawData))=="角色名："+rawData&&UiLanguage.T(UiLanguage.Data(rawData))==rawData;using var placeholder=new TextBox(){PlaceholderText="Filter equipment ID",Text=rawData};UiLanguage.Attach(placeholder);bool placeholderChinese=placeholder.PlaceholderText=="筛选装备 ID";UiLanguage.Set("en");bool englishDataPreserved=UiLanguage.T(UiLanguage.Data(rawData))==rawData;bool placeholderRoundTrip=placeholderChinese&&placeholder.PlaceholderText=="Filter equipment ID"&&placeholder.Text==rawData;UiLanguage.Set("zh-CN");bool skillTranslated=UiLanguage.T("Skill 5 / level 7")=="技能 5 / 等级 7";
        foreach(string language in new[]{"en","zh-CN"})
        foreach(var size in new[]{new Size(1120,760),new Size(1500,940)})
        {
            form.SetSmokeLanguage(language);form.Size=size;
            for(int tab=0;tab<4;tab++)
            {
                if(tab<3)form.SelectDomainSmokeTab(tab);else form.SelectSmokeTab(1);form.PerformLayout();Application.DoEvents();
                string name=$"{language}-{size.Width}x{size.Height}-{new[]{"character","inventory","equipment","structure"}[tab]}.png";
                using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(outputDirectory,name),ImageFormat.Png);
                results.Add(new{size=size.ToString(),tab,name,formCreated=form.IsHandleCreated,deviceDpi=form.DeviceDpi,formSize=new{form.Width,form.Height},syntheticOnly=true,actions=form.SmokeActionLayout()});
            }
        }
        foreach(string language in new[]{"en","zh-CN"}){UiLanguage.Set(language);using var validation=new ValidationForm(){StartPosition=FormStartPosition.Manual,Location=new Point(0,0),ShowInTaskbar=false,Opacity=0};validation.Show();Application.DoEvents();using var bitmap=new Bitmap(validation.Width,validation.Height);validation.DrawToBitmap(bitmap,new Rectangle(Point.Empty,validation.Size));bitmap.Save(Path.Combine(outputDirectory,language+"-validation.png"),ImageFormat.Png);validation.Close();}
        var syntheticCatalog=new GameCatalog([new(GameCatalogKind.Item,0x04000031,new(){["en"]="SYNTHETIC sample item",["zh-CN"]="合成示例物品"},"Synthetic",Origin:new("SYNTHETIC TEST","CC0 synthetic labels"),DeclaredStatus:CatalogItemStatus.VERIFIED_ITEM,DeclaredMaximumQuantity:123),new(GameCatalogKind.Skill,2,new(){["en"]="SYNTHETIC skill",["zh-CN"]="合成技能"},Origin:new("SYNTHETIC TEST","CC0 synthetic labels"),DeclaredStatus:CatalogItemStatus.KNOWN_ITEM)],()=>UiLanguage.Current);var syntheticEditor=new DirectoryEditor();syntheticEditor.OpenSlot(input);
        foreach(string language in new[]{"en","zh-CN"}){UiLanguage.Set(language);using var browser=new CatalogForm(syntheticCatalog,syntheticEditor){StartPosition=FormStartPosition.Manual,Location=new Point(0,0),ShowInTaskbar=false,Opacity=0};browser.Show();Application.DoEvents();using var bitmap=new Bitmap(browser.Width,browser.Height);browser.DrawToBitmap(bitmap,new Rectangle(Point.Empty,browser.Size));bitmap.Save(Path.Combine(outputDirectory,language+"-catalog.png"),ImageFormat.Png);browser.Close();}
        var resourceChecks=new List<object>();string bundled=Path.Combine(AppContext.BaseDirectory,"data","mhrice");if(File.Exists(Path.Combine(bundled,"catalog.json"))){var external=GameCatalog.LoadDirectory(bundled,()=>UiLanguage.Current);foreach(string language in new[]{"en","zh-CN"}){UiLanguage.Set(language);using var browser=new CatalogForm(external,syntheticEditor){StartPosition=FormStartPosition.Manual,Location=new Point(0,0),ShowInTaskbar=false,Opacity=0};browser.Show();Application.DoEvents();bool searchPassed=browser.SmokeCandidateSearch();resourceChecks.Add(new{language,searchPassed,entries=external.Entries.Count,scope="Real admitted name resource, synthetic inventory only; no NS mapping verification"});foreach(bool show in new[]{false,true}){browser.SmokeSetCandidates(show);Application.DoEvents();using var bitmap=new Bitmap(browser.Width,browser.Height);browser.DrawToBitmap(bitmap,new Rectangle(Point.Empty,browser.Size));bitmap.Save(Path.Combine(outputDirectory,$"{language}-mhrice-candidates-{show}.png"),ImageFormat.Png);}browser.Close();}}
        File.WriteAllText(Path.Combine(outputDirectory,"smoke-results.json"),JsonSerializer.Serialize(new{executedAtUtc=DateTimeOffset.UtcNow,scope="Synthetic desktop render and selection-retention smoke only",emptyOpenEntries,loadedOpenEntries,ordinaryRawReadOnly,candidateGatePreservedStaging,selectionRetained,languageSwitchPreservedStaging,preferenceRemembered,defaultEnglish,rawDataPreserved,englishDataPreserved,placeholderRoundTrip,skillTranslated,resourceChecks,results},new JsonSerializerOptions{WriteIndented=true}));form.Close();
    }
}
