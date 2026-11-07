using Avalonia.VisualTree;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Core.Models;
using SceneGenerator.Desktop;
using SceneGenerator.Desktop.Controls;
using SceneGenerator.Desktop.Services;
using SceneGenerator.Desktop.ViewModels;
using SceneGenerator.Desktop.Views;
using SceneGenerator.OpenStreetMap;

int checks=0;var failures=new List<string>();
string artifacts=Path.Combine(AppContext.BaseDirectory,"artifacts");Directory.CreateDirectory(artifacts);
string temp=Path.Combine(Path.GetTempPath(),"osm-native-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
async Task Wait(Func<bool> condition,string label){for(int i=0;i<1000;i++){if(condition()){await Task.Delay(70);return;}await Task.Delay(15);}throw new Exception("Timeout: "+label);}
if (args.Contains("--viewmodel-only"))
{
 try { await RunViewModelChecks(); }
 catch (Exception error) { failures.Add(error.ToString()); }
 Console.WriteLine(JsonSerializer.Serialize(new { checks, failures }, new JsonSerializerOptions { WriteIndented = true }));
 Directory.Delete(temp,true);
 return failures.Count == 0 ? 0 : 1;
}
var factory=typeof(App).Assembly.GetType("SceneGenerator.Desktop.Program")!.GetMethod("BuildAvaloniaApp",BindingFlags.Public|BindingFlags.Static)!;
var builder=(AppBuilder)factory.Invoke(null,null)!;
builder.AfterSetup(_=>{
 var desktop=(IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
 desktop.Startup+=(_,_)=>Dispatcher.UIThread.Post(async()=>{
  try{await Run(desktop);}
  catch(Exception e){failures.Add(e.ToString());}
  finally{
   string result=JsonSerializer.Serialize(new{checks,failures},new JsonSerializerOptions{WriteIndented=true});
   Console.WriteLine(result);File.WriteAllText(Path.Combine(artifacts,"summary.json"),result);
   if(Directory.Exists(temp))Directory.Delete(temp,true);
   desktop.Shutdown(failures.Count==0?0:1);
  }
 });
});
return builder.StartWithClassicDesktopLifetime(args);

async Task RunViewModelChecks()
{
 string example = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../docs/examples/territory-demo.osm-project.json"));
 var model = new OsmImportViewModel(new SlowProvider(), () => Task.FromResult<string?>(example));
 ISceneSource? result = null;
 int delivered = 0;
 model.SceneReady += source => { result = source; delivered++; };
 await model.OpenFileCommand.ExecuteAsync(null);
 Check(model.Buildings.Count == 4 && model.SelectedBuilding?.Id == "way/1", "demo opens with its saved KII");
 Check(model.CreateSceneCommand.CanExecute(null), "demo can be used immediately after opening");
 Check(model.QualityText.Contains("Предупреждений: 1"), "quality warnings remain visible before import");
 await model.CreateSceneCommand.ExecuteAsync(null);
 Check(delivered == 1 && result is not null && model.ErrorMessage == "", "using demo delivers a scene without an extra acknowledgement");
 var scene = result!.GetScenes().Single();
 Check(scene.Objects.Count == 3 && scene.Objects[0].Id == "way/1" && scene.Bounds.Height == 75, "demo retains exclusions, selected KII and manual height");
 Check(model.QualityText.Contains("Предупреждений: 1"), "nonblocking quality summary is preserved");
 model.SelectedBuilding!.TopHeight = 0;
 await model.CreateSceneCommand.ExecuteAsync(null);
 Check(delivered == 1 && model.HasErrors, "invalid geometry still prevents transfer and explains why");
 model.SelectedBuilding.TopHeight = 75;
 await model.CreateSceneCommand.ExecuteAsync(null);
 Check(delivered == 2 && !model.HasErrors, "correcting height makes the next attempt succeed");
 model.SelectedBuilding.Included = false;
 await model.CreateSceneCommand.ExecuteAsync(null);
 Check(delivered == 2 && !model.CreateSceneCommand.CanExecute(null) && model.KiiCount == 0, "excluding last KII disables transfer and clears role");
 model.SelectedBuilding.Included = true;
 model.SelectedBuilding = null;
 Check(!model.CreateSceneCommand.CanExecute(null), "missing KII keeps use command disabled");
 Check(model.CreateSceneHint.Contains("Отметьте"), "disabled use command has a visible KII selection hint");

 var first = model.Buildings.Single(b => b.Id == "way/1");
 var second = model.Buildings.Single(b => b.Id == "way/2");
 first.IsKii = true; second.IsKii = true;
 Check(model.SelectedBuilding is null && model.KiiCount == 2 && model.CreateSceneCommand.CanExecute(null), "KII roles independent of inspector selection");
 await model.CreateSceneCommand.ExecuteAsync(null);
 Check(result!.GetScenes().Single().KiiObjects.Count == 2 && delivered == 3, "viewmodel delivers every marked KII");
 second.Included = false;
 Check(!second.IsKii && model.KiiCount == 1, "excluding a KII clears its role");
 second.IsKii = true;
 Check(second.Included && model.KiiCount == 2, "marking excluded building as KII includes it");


 string overlapJson = JsonSerializer.Serialize(new { elements = new object[] {
     new { type="way", id=11, tags=new { building="yes", height="18" }, geometry=new[] {
         new { lat=-.002, lon=-.002 }, new { lat=-.002, lon=-.001 }, new { lat=-.001, lon=-.001 }, new { lat=-.001, lon=-.002 }, new { lat=-.002, lon=-.002 } } },
     new { type="way", id=12, tags=new { building="yes", height="300" }, geometry=new[] {
         new { lat=-.002, lon=-.002 }, new { lat=-.002, lon=-.001 }, new { lat=-.001, lon=-.001 }, new { lat=-.001, lon=-.002 }, new { lat=-.002, lon=-.002 } } }
 } });
 string overlapPath=Path.Combine(temp,"overlap.json");await File.WriteAllTextAsync(overlapPath,overlapJson);
 var overlapModel = new OsmImportViewModel(new SlowProvider(),()=>Task.FromResult<string?>(overlapPath)){Latitude=0,Longitude=0};
 ISceneSource? overlapResult=null;overlapModel.SceneReady+=source=>overlapResult=source;
 await overlapModel.OpenFileCommand.ExecuteAsync(null);
 overlapModel.SelectedBuilding=overlapModel.Buildings.Single(b=>b.Id=="way/11");
 overlapModel.SelectedBuilding.IsKii=true;
 await overlapModel.CreateSceneCommand.ExecuteAsync(null);
 Check(overlapResult is OsmSceneSource resultSource && resultSource.SkippedBuildings.Count==1 && !overlapModel.HasErrors,"Use Scene completes after excluding overlap");
 Check(!overlapModel.Buildings.Single(b=>b.Id=="way/12").Included && overlapModel.SelectedBuilding.Included,"only conflicting obstacle unchecked, KII retained");
 var main=new MainViewModel(new Folder(temp),new Confirm(),new Settings(),new ReadyDialog(overlapResult!));
 await main.ImportOsmCommand.ExecuteAsync(null);
 Check(main.Scenes.Count==1 && main.StatusText.Contains("исключено") && !main.HasErrors,"main window receives scene and displays exclusion count");
}

async Task Run(IClassicDesktopStyleApplicationLifetime desktop)
{
 var main=(MainWindow)desktop.MainWindow!;await Task.Delay(150);
 Check(((MainViewModel)main.DataContext!).ImportOsmCommand.CanExecute(null),"App connects real OSM dialog");
 var dialog=new Dialog(main,temp);
 var vm=new MainViewModel(new Folder(temp),new Confirm(),new Settings(),dialog){SceneCount=2,ObstacleCount=3};
 main.DataContext=vm;await vm.GenerateCommand.ExecuteAsync(null);
 var old=vm.Scenes;
 Check(old.Count==2,"legacy generation still works");
 var task=vm.ImportOsmCommand.ExecuteAsync(null);
 await Wait(()=>dialog.Window?.IsVisible==true,"owned import window");
 Check(vm.IsWorkingWithSettings&&!vm.GenerateCommand.CanExecute(null)&&!vm.ImportOsmCommand.CanExecute(null),"main commands locked during import");
 var model=dialog.Model!;var window=dialog.Window!;
 await model.LoadOnlineCommand.ExecuteAsync(null);
 Check(model.Buildings.Count==4&&model.SelectedBuilding is null,"four imported buildings and explicit KII selection");
 Check(model.QualityText.Contains("по умолчанию: 1"),"height quality shown");
 var preview=window.GetLogicalDescendants().OfType<OsmBuildingsPreview>().Single();
 Check(ReferenceEquals(preview.Buildings,model.Buildings)&&preview.AreaLength==1000,"preview bindings");
 var list=window.GetLogicalDescendants().OfType<ListBox>().Single();
 list.SelectedIndex=model.Buildings.ToList().FindIndex(b=>b.Id=="way/1");await Task.Delay(80);
 Check(model.SelectedBuilding?.Id=="way/1"&&preview.SelectedBuilding?.Id=="way/1","list selection synchronizes preview and KII");
 var target=model.Buildings.Single(b=>b.Id=="way/2");
 var centre=target.Building.Footprint.GetCenterPoint();
 double scale=Math.Min((preview.Bounds.Width-40)/preview.AreaLength,(preview.Bounds.Height-55)/preview.AreaWidth);
 var click=new Point((preview.Bounds.Width-preview.AreaLength*scale)/2+centre.X*scale,30+preview.AreaWidth*scale-centre.Y*scale);
 var pointer=new Avalonia.Input.Pointer(1,PointerType.Mouse,true);
 var input=new PointerPressedEventArgs(preview,pointer,window,preview.TranslatePoint(click,window)!.Value,0,new PointerPointProperties(RawInputModifiers.LeftMouseButton,PointerUpdateKind.LeftButtonPressed),KeyModifiers.None,1);
 typeof(OsmBuildingsPreview).GetMethod("OnPointerPressed",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(preview,[input]);
 await Task.Delay(40);
 Check(model.SelectedBuilding?.Id=="way/2",$"clicking footprint selects KII through binding; vm={model.SelectedBuilding?.Id}; preview={preview.SelectedBuilding?.Id}; input={input.GetPosition(preview)}; click={click}; bounds={preview.Bounds}; centre={centre}; enabled={preview.IsEffectivelyEnabled}");
 list.SelectedIndex=model.Buildings.ToList().FindIndex(b=>b.Id=="way/1");await Task.Delay(40);
 Check(preview.SelectedBuilding?.Id=="way/1","preview binding survives direct pointer selection");
 Check(ReferenceEquals(vm.Scenes,old),"old series preserved before commit");
 Check(model.QualityText.Contains("Предупреждений: 1"),"quality summary remains available without blocking scene use");
 model.SelectedBuilding!.TopHeight=75;
 var kiiChecks=window.GetVisualDescendants().OfType<CheckBox>().Where(c => c.Content?.ToString()=="КИИ").ToArray();
 Check(kiiChecks.Length>=2,"KII role checkboxes displayed in native list");
 foreach(var role in kiiChecks.Where(c => c.DataContext is OsmBuildingItemViewModel b && b.Id is "way/1" or "way/2")) role.IsChecked=true;
 Check(model.KiiCount==2,"native role checkboxes update multi selection");
 Application.Current!.RequestedThemeVariant=ThemeVariant.Dark;await Task.Delay(80);
 await Capture(window,"osm-dark.png");
 Application.Current.RequestedThemeVariant=ThemeVariant.Light;await Task.Delay(80);await Capture(window,"osm-light.png");
 window.Width=1000;window.Height=800;await Task.Delay(70);await Capture(window,"osm-minimum.png");
 Check(preview.Bounds.Width>400&&preview.Bounds.Height>50,"preview usable at minimum window size");
 await model.SaveProjectCommand.ExecuteAsync(null);
 Check(File.Exists(Path.Combine(temp,"review.osm-project.json")),"save reviewed project through native window model");
 await model.CreateSceneCommand.ExecuteAsync(null);await task;
 Check(!vm.IsWorkingWithSettings&&vm.Scenes.Count==1,"import replaces series only after success");
 Check(vm.SelectedScene!.Objects[0].Id=="way/1"&&vm.SelectedScene.KiiObjects.Count==2&&vm.SelectedScene.Bounds.Height==75,"chosen KII and manual height transferred");
 var viewer=main.FindControl<SceneView>("SceneViewport")!;await Task.Delay(80);
 Check(ReferenceEquals(viewer.Scene,vm.SelectedScene)&&viewer.Has3DScene,"existing 3D viewer consumes imported Scene");
 viewer.FocusKii();
 Check(vm.SelectedScene.KiiObjects.SelectMany(o=>o.Geometry.GetBasePoints().Concat(o.Geometry.GetRoofPoints())).All(p=>viewer.TryProjectPoint(p,out var screen)&&screen.X>=0&&screen.Y>=0&&screen.X<=viewer.Bounds.Width&&screen.Y<=viewer.Bounds.Height),"focus keeps both KII within viewport");
 await vm.SaveAllCommand.ExecuteAsync(null);
 using(var export=JsonDocument.Parse(File.ReadAllText(Path.Combine(temp,"scene-000001.json"))))
  Check(export.RootElement.GetProperty("source").GetProperty("type").GetString()=="openstreetmap","same exporter emits OSM source");
 var imported=vm.Scenes;
 task=vm.ImportOsmCommand.ExecuteAsync(null);await Wait(()=>dialog.Window?.IsVisible==true,"reopen import");
 model=dialog.Model!;window=dialog.Window!;
 await model.OpenFileCommand.ExecuteAsync(null);
 Check(model.SelectedBuilding?.Id=="way/1"&&model.SelectedBuilding.TopHeight==75&&model.KiiCount==2,"saved project restores KII and edited height");
 Check(model.Latitude==0&&model.Longitude==0,"saved project restores its coordinates instead of current form");
 model.IsThreeD=false;
 await model.CreateSceneCommand.ExecuteAsync(null);await task;
 Check(vm.SelectedScene!.Mode==SceneMode.TwoD&&vm.SelectedScene.Objects.All(o=>o.Geometry.Height==0),"2D import through same window");
 imported=vm.Scenes;
 task=vm.ImportOsmCommand.ExecuteAsync(null);await Wait(()=>dialog.Window?.IsVisible==true,"third dialog");
 model=dialog.Model!;window=dialog.Window!;
 var loading=model.LoadOnlineCommand.ExecuteAsync(null);model.Cancel();await loading;
 Check(model.Buildings.Count==0&&!model.IsBusy&&model.StatusText.Contains("отменена"),"load cancellation");
 window.Close();await task;
 Check(ReferenceEquals(vm.Scenes,imported)&&vm.CanEditSettings,"cancelled dialog preserves previous scenes");
 await vm.GenerateCommand.ExecuteAsync(null);
 Check(vm.Scenes.Count==2&&vm.SelectedScene!.Objects.Count==4,"random generator still works after OSM mode");
 main.Width=1100;main.Height=650;await Task.Delay(60);
 var osmButton=main.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content is string t&&t=="Территория OSM");
 Check(osmButton.IsEffectivelyVisible&&osmButton.Bounds.Width>100,"OSM entry remains visible at minimum main size");
 await Capture(main,"main-with-osm.png");
}
async Task Capture(Window window,string name)
{
 if(window.Content is Panel panel)panel.Background=window.Background;
 await Task.Delay(70);
 using var bitmap=new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width,(int)window.ClientSize.Height),new Vector(96,96));
 bitmap.Render((Visual)window.Content!);bitmap.Save(Path.Combine(artifacts,name),PngBitmapEncoderOptions.Default);
}
sealed class Folder(string path):IOutputFolderPicker{public Task<string?> PickFolderAsync()=>Task.FromResult<string?>(path);}
sealed class Confirm:IConfirmationService{public Task<bool> ConfirmAsync(string title,string message)=>Task.FromResult(true);}
sealed class Settings:ISettingsFilePicker{public Task<string?> PickOpenFileAsync()=>Task.FromResult<string?>(null);public Task<string?> PickSaveFileAsync()=>Task.FromResult<string?>(null);}
sealed class SlowProvider:IOsmDataProvider
{
 public async Task<OsmData> LoadAsync(OsmImportOptions options,CancellationToken token=default)
 {
  await Task.Delay(90,token);
  string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../docs/examples/territory-demo.osm-project.json"));
  var project=OsmImportProject.TryRead(await File.ReadAllTextAsync(path,token))!;
  return new(project.RawOverpassJson,"synthetic native fixture",DateTimeOffset.UtcNow);
 }
}
sealed class Dialog(Window owner,string folder):IOsmImportDialog
{
 public OsmImportWindow? Window{get;private set;}public OsmImportViewModel? Model{get;private set;}
 public async Task<ISceneSource?> ShowAsync()
 {
  var window=new OsmImportWindow();Window=window;
  var model=new OsmImportViewModel(new SlowProvider(),()=>Task.FromResult<string?>(Path.Combine(folder,"review.osm-project.json")),()=>Task.FromResult<string?>(Path.Combine(folder,"review.osm-project.json"))){Latitude=0,Longitude=0};
  Model=model;window.DataContext=model;model.SceneReady+=source=>window.Close(source);
  return await window.ShowDialog<ISceneSource?>(owner);
 }
}

sealed class ReadyDialog(ISceneSource source):IOsmImportDialog
{
 public Task<ISceneSource?> ShowAsync()=>Task.FromResult<ISceneSource?>(source);
}
