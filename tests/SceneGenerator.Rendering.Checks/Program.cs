using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Generation;
using SceneGenerator.Core.Models;
using SceneGenerator.Desktop;
using SceneGenerator.Desktop.Controls;
using SceneGenerator.Desktop.Controls.Rendering;
using SceneGenerator.Desktop.ViewModels;
using SceneGenerator.Desktop.Views;
using SceneGenerator.Desktop.Services;
using SceneGenerator.Serialization;
using SceneGenerator.Serialization.Dto;
using SceneGenerator.Serialization.Mapping;

string artifacts=Path.Combine(AppContext.BaseDirectory,"artifacts");Directory.CreateDirectory(artifacts);
AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
if(args.Contains("--reflect")){
 foreach(var type in new[]{typeof(Avalonia.Input.Pointer),typeof(PointerPointProperties),typeof(PointerEventArgs),typeof(PointerPressedEventArgs),typeof(PointerReleasedEventArgs),typeof(PointerWheelEventArgs),typeof(IPointer)}){
  Console.WriteLine(type.FullName);foreach(var ctor in type.GetConstructors())Console.WriteLine(ctor);
  if(type==typeof(IPointer))foreach(var member in type.GetMembers())Console.WriteLine(member);
 }
 return;
}
int checks=0;
void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
void Near(double a,double b,string name,double tolerance=1e-7)=>Check(Math.Abs(a-b)<tolerance,name+$" ({a} != {b})");
Point2D[] outer=[new(0,100),new(1000,100),new(1000,1100),new(600,1100),new(600,700),new(0,700)];
Point2D[][] holes=[[new(200,250),new(450,250),new(450,500),new(200,500)]];
Scene MakeScene(bool threeD=true,bool reverse=false){
 var scene=new Scene("test",threeD?SceneMode.ThreeD:SceneMode.TwoD,
  new Box3D(new Point3D(-500,-300,threeD?5:0),new Size3D(2200,2000,threeD?80:0)),
  new SceneObject("kii",SceneObjectType.Kii,new PolygonPrism3D(new Polygon2D(outer,holes),threeD?5:0,threeD?40:0)));
 var obstacles=new[]{
  new SceneObject("outside",SceneObjectType.Obstacle,new Box3D(new Point3D(1150,1150,threeD?5:0),new Size3D(200,150,threeD?30:0))),
  new SceneObject("courtyard",SceneObjectType.Obstacle,new Box3D(new Point3D(270,320,threeD?5:0),new Size3D(90,90,threeD?18:0))),
  new SceneObject("raised",SceneObjectType.Obstacle,new Box3D(new Point3D(1150,100,threeD?15:0),new Size3D(180,220,threeD?35:0)))
 };
 foreach(var item in reverse?obstacles.Reverse():obstacles)scene.AddObstacle(item);
 return scene;
}
var scene=MakeScene();
var mapper=new SceneDtoMapper();var metadata=new SceneSourceDto{Type="generated",Seed=20,SequenceIndex=1};
string beforeJson=JsonSerializer.Serialize(mapper.ToDto(scene,metadata));
SceneView View(Scene s){var v=new SceneView{Scene=s,KiiBrush=new SolidColorBrush(Color.Parse("#2563EB")),
 ObstacleBrush=new SolidColorBrush(Color.Parse("#64748B")),GridBrush=new SolidColorBrush(Color.Parse("#DEE4EF")),
 TextBrush=new SolidColorBrush(Color.Parse("#64748B")),InfoBackground=Brushes.White};
 v.Measure(new Size(800,600));v.Arrange(new Rect(0,0,800,600));return v;}
var view=View(scene);
Check(view.Is3DActive&&view.Has3DScene&&view.ViewTitle=="Объёмный вид","3D scene automatically activates volume view");
Check(view.HeightMultiplier==1&&view.NavigationHint.Contains("вращение"),"3D scale and navigation hints");
for(int i=0;i<3;i++){view.HeightScaleIndex=i;Near(view.HeightMultiplier,new[]{1,10,25}[i],"height scale selection");}
view.HeightScaleIndex=0;
SceneProjection3D Projection(SceneView v){
 object?[] args=[null];typeof(SceneView).GetMethod("TryGetProjection3D",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(v,args);
 return (SceneProjection3D)args[0]!;
}
Point Screen(SceneView v,double x,double y,double z){Check(v.TryProjectPoint(new Point3D(x,y,z),out Point p),"projection available");return p;}
Rect KiiBounds(SceneView v){var g=v.Scene!.Objects[0].Geometry;var points=g.GetBasePoints().Concat(g.GetRoofPoints()).Select(p=>Screen(v,p.X,p.Y,p.Z)).ToArray();double minX=points.Min(p=>p.X),minY=points.Min(p=>p.Y);return new Rect(minX,minY,points.Max(p=>p.X)-minX,points.Max(p=>p.Y)-minY);}

// Check the projection against its orthonormal camera basis, the inverse ray and the ground plane.
foreach(double yaw in new[]{-180d,-60,0,55,140})foreach(double elevation in new[]{10d,35,80})foreach(double heightScale in new[]{1d,10,25}){
 var projection=new SceneProjection3D(scene,new Rect(18,18,764,564),1,default,yaw,elevation,heightScale);
 var p=new Point3D(420,580,25);var screen=projection.ToScreen(p);double depth=projection.Depth(p.X,p.Y,p.Z);
 var roundtrip=projection.Unproject(screen,depth);
 Near(roundtrip.X,p.X,"roundtrip X");Near(roundtrip.Y,p.Y,"roundtrip Y");Near(roundtrip.Z,p.Z,"roundtrip Z");
 Near(projection.DepthOnHorizontalPlane(screen.Y,p.Z),depth,"horizontal plane depth");
 var ground=projection.ToGround(projection.ToScreen(420,580,5));Near(ground.X,420,"ground X");Near(ground.Y,580,"ground Y");
 foreach(var corner in scene.Bounds.Footprint.Outer)foreach(double z in new[]{5d,85})
  Check(projection.Plot.Contains(projection.ToScreen(corner.X,corner.Y,z)),"fit includes all scene volume corners");
}

void Snapshot(SceneView target,string name){
 using var bitmap=new RenderTargetBitmap(new PixelSize(800,600),new Vector(96,96));bitmap.Render(target);bitmap.Save(Path.Combine(artifacts,name),PngBitmapEncoderOptions.Default);
}
Snapshot(view,"3d-real-height.png");
view.HeightScaleIndex=1;Snapshot(view,"3d-height10.png");
view.HeightScaleIndex=0;view.RotateView(0,45);
Check(ReferenceEquals(view.FindObjectAt(Screen(view,800,500,45)),scene.Objects[0]),"roof can be selected");
Check(view.FindObjectAt(Screen(view,220,470,45)) is null,"hole has no roof");
Check(view.FindObjectAt(Screen(view,300,900,45)) is null,"concave cutout has no roof");
Check(ReferenceEquals(view.FindObjectAt(Screen(view,315,365,23)),scene.Objects[2]),"object inside courtyard visible through hole");
Check(ReferenceEquals(view.FindObjectAt(Screen(view,1200,180,50)),scene.Objects[3]),"elevated object's roof can be selected");
Snapshot(view,"3d-courtyard.png");

// Independent analytic ray intersections test visibility of roofs and all outer/inner walls.
SceneObject? RayHit(Scene scene,SceneProjection3D camera,Point screen){
 Point3D origin=camera.Unproject(screen,0);double best=double.NegativeInfinity;SceneObject? found=null;
 foreach(var item in scene.Objects){
  var g=item.Geometry;double roof=g.BaseElevation+g.Height;
  double topDepth=camera.DepthOnHorizontalPlane(screen.Y,roof);var onTop=camera.Unproject(screen,topDepth);
  if(g.Footprint.Contains(new Point2D(onTop.X,onTop.Y))&&topDepth>best){best=topDepth;found=item;}
  foreach(var ring in new[]{g.Footprint.Outer}.Concat(g.Footprint.Holes))for(int i=0;i<ring.Count;i++){
   var a=ring[i];var b=ring[(i+1)%ring.Count];double ex=b.X-a.X,ey=b.Y-a.Y;
   double den=camera.DepthX*ey-camera.DepthY*ex;if(Math.Abs(den)<1e-9)continue;
   double ax=a.X-origin.X,ay=a.Y-origin.Y;
   double depth=(ax*ey-ay*ex)/den;double segment=(ax*camera.DepthY-ay*camera.DepthX)/den;
   double z=camera.Unproject(screen,depth).Z;
   if(segment>=0&&segment<=1&&z>=g.BaseElevation&&z<=roof&&depth>best){best=depth;found=item;}
  }
 }
 return found;
}
double SegmentDistance(Point p,Point a,Point b){Vector d=b-a;double length=d.X*d.X+d.Y*d.Y;double t=length==0?0:Math.Clamp(((p.X-a.X)*d.X+(p.Y-a.Y)*d.Y)/length,0,1);Point q=a+d*t;return double.Hypot(p.X-q.X,p.Y-q.Y);}
bool NearEdge(Scene s,SceneProjection3D camera,Point p){
 foreach(var item in s.Objects)foreach(var ring in new[]{item.Geometry.Footprint.Outer}.Concat(item.Geometry.Footprint.Holes))for(int i=0;i<ring.Count;i++){
  var a=ring[i];var b=ring[(i+1)%ring.Count];double low=item.Geometry.BaseElevation,high=low+item.Geometry.Height;
  Point ab=camera.ToScreen(a.X,a.Y,low),bb=camera.ToScreen(b.X,b.Y,low),at=camera.ToScreen(a.X,a.Y,high),bt=camera.ToScreen(b.X,b.Y,high);
  if(SegmentDistance(p,ab,bb)<1.2||SegmentDistance(p,at,bt)<1.2||SegmentDistance(p,ab,at)<1.2)return true;
 }
 return false;
}
int hits=0,occluded=0;var timer=Stopwatch.StartNew();
foreach(double yaw in new[]{-160d,-90,-25,40,110,175})foreach(double elevation in new[]{15d,40,75})foreach(int scaleIndex in new[]{0,1}){
 view.FitScene();view.HeightScaleIndex=scaleIndex;view.RotateView(yaw-view.CameraAzimuth,elevation-view.CameraElevation);
 var camera=Projection(view);
 for(int y=28;y<570;y+=23)for(int x=28;x<770;x+=23){
  Point point=new(x+.25,y+.25);if(NearEdge(scene,camera,point))continue;
  var expected=RayHit(scene,camera,point);var actual=view.FindObjectAt(point);
  Check(actual?.Id==expected?.Id,$"depth hit yaw={yaw} elevation={elevation} scale={scaleIndex} at {point}: {actual?.Id} != {expected?.Id}");
  if(actual is not null){hits++;if(actual.Type==SceneObjectType.Obstacle)occluded++;}
 }
}
Check(hits>300&&occluded>20,"ray tests cover buildings and obstacles");
Console.WriteLine($"Ray visibility: {hits} object hits, {occluded} obstacle hits, {timer.ElapsedMilliseconds} ms.");
view.FitScene();view.HeightScaleIndex=1;

// Drawing order cannot select an object hidden behind a nearer object.
var reversed=View(MakeScene(reverse:true));reversed.HeightScaleIndex=1;
for(int y=30;y<570;y+=19)for(int x=30;x<770;x+=19){
 var p=new Point(x+.25,y+.25);Check(view.FindObjectAt(p)?.Id==reversed.FindObjectAt(p)?.Id,"visibility independent of object enumeration order");
}

view.FocusKii();Check(view.ZoomText=="100%","large KII does not need extra zoom");
var center=scene.Objects[0].Geometry.Footprint.GetCenterPoint();
Point focused=KiiBounds(view).Center;Near(focused.X,400,"focus centers KII X",.01);Near(focused.Y,300,"focus centers KII Y",.01);
view.ZoomIn();Check(view.ZoomText!="100%","3D zoom in");
view.RotateView(24,200);Near(view.CameraElevation,80,"elevation upper limit");
view.RotateView(5000,-5000);Near(view.CameraElevation,10,"elevation lower limit");
Check(view.CameraAzimuth>=-180&&view.CameraAzimuth<180,"azimuth wraps");
for(int i=0;i<25;i++)view.ZoomIn();Check(view.ZoomText=="6400%","3D maximum zoom");
for(int i=0;i<40;i++)view.ZoomOut();Check(view.ZoomText=="100%","3D minimum zoom");
view.FitScene();Near(view.CameraAzimuth,-55,"fit resets yaw");Near(view.CameraElevation,35,"fit resets elevation");
view.Show3D=false;Check(!view.Is3DActive&&view.Has3DScene&&view.ViewTitle=="Вид сверху","3D scene supports top view");
Check(ReferenceEquals(view.FindObjectAt(Screen(view,800,500,45)),scene.Objects[0]),"top view hit testing");
Snapshot(view,"top-view.png");
view.Show3D=true;Check(view.Is3DActive,"return to 3D");
view.Scene=MakeScene(false);Check(!view.Is3DActive&&!view.Has3DScene,"2D scene never becomes 3D");
var previousYaw=view.CameraAzimuth;view.RotateView(30,30);Near(view.CameraAzimuth,previousYaw,"2D ignores rotation");
view.FocusKii();Check(ReferenceEquals(view.FindObjectAt(Screen(view,center.X,center.Y,0)),view.Scene.Objects[0]),"2D supports focus");
view.Scene=scene;Check(view.Is3DActive&&view.ZoomText=="100%","scene selection resets camera");
Check(JsonSerializer.Serialize(mapper.ToDto(scene,metadata))==beforeJson,"view scale and camera do not change domain or exported JSON");
Check(view.FindObjectAt(new Point(1,1)) is null,"outside plot not selectable");
view.Scene=null;Check(!view.Has3DScene&&!view.Is3DActive&&view.FindObjectAt(new Point(400,300)) is null,"empty viewer safe");
view.FocusKii();view.RotateView(50,20);view.ZoomIn();
Check(!view.TryProjectPoint(new Point3D(0,0,0),out _),"empty viewer has no projection");

// Exercise the actual pointer handlers rather than only camera helper methods.
view.Scene=scene;view.HeightScaleIndex=0;view.FitScene();
var gestureHost=new Window{Content=view,Width=800,Height=600};gestureHost.Measure(new Size(800,600));gestureHost.Arrange(new Rect(0,0,800,600));
var pointer=new Avalonia.Input.Pointer(1,PointerType.Mouse,true);
void Input(string method,PointerEventArgs e)=>typeof(SceneView).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(view,[e]);
void Press(Point p,MouseButton button,KeyModifiers keys=KeyModifiers.None){
 var modifiers=button==MouseButton.Left?RawInputModifiers.LeftMouseButton:RawInputModifiers.RightMouseButton;
 var kind=button==MouseButton.Left?PointerUpdateKind.LeftButtonPressed:PointerUpdateKind.RightButtonPressed;
 Input("OnPointerPressed",new PointerPressedEventArgs(view,pointer,view,p,0,new PointerPointProperties(modifiers,kind),keys,1));
}
void Move(Point p,RawInputModifiers modifiers,KeyModifiers keys=KeyModifiers.None)=>Input("OnPointerMoved",new PointerEventArgs(InputElement.PointerMovedEvent,view,pointer,view,p,0,new PointerPointProperties(modifiers,PointerUpdateKind.Other),keys));
void Release(Point p,MouseButton button){var kind=button==MouseButton.Left?PointerUpdateKind.LeftButtonReleased:PointerUpdateKind.RightButtonReleased;
 Input("OnPointerReleased",new PointerReleasedEventArgs(view,pointer,view,p,0,new PointerPointProperties(RawInputModifiers.None,kind),KeyModifiers.None,button));}
Press(new Point(400,300),MouseButton.Left);Move(new Point(440,315),RawInputModifiers.LeftMouseButton);
Near(view.CameraAzimuth,-39,"left drag rotates yaw");Near(view.CameraElevation,30.5,"left drag tilts elevation");
Check(ReferenceEquals(pointer.Captured,view),"drag captures pointer");
var renderer=typeof(SceneView).GetField("_renderer3D",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(view)!;
var cachedProjection=renderer.GetType().GetField("_projection",BindingFlags.Instance|BindingFlags.NonPublic)!;
object? lastRenderedProjection=cachedProjection.GetValue(renderer);
Move(new Point(480,330),RawInputModifiers.LeftMouseButton);
Move(new Point(440,315),RawInputModifiers.LeftMouseButton);
Check(Equals(lastRenderedProjection,cachedProjection.GetValue(renderer)),"rotation pointer events reuse one gesture anchor without synchronous rerasterization");
Near(view.CameraAzimuth,-39,"rotation accumulates pointer deltas");
Release(new Point(440,315),MouseButton.Left);Check(pointer.Captured is null,"release frees pointer");
view.FitScene();Point initialScreen=Screen(view,800,500,45);
Press(new Point(300,300),MouseButton.Left,KeyModifiers.Shift);Move(new Point(330,320),RawInputModifiers.LeftMouseButton,KeyModifiers.Shift);
Point shiftedScreen=Screen(view,800,500,45);Near(shiftedScreen.X-initialScreen.X,30,"Shift drag pans X");Near(shiftedScreen.Y-initialScreen.Y,20,"Shift drag pans Y");Near(view.CameraAzimuth,-55,"Shift drag does not rotate");Release(new Point(330,320),MouseButton.Left);
view.FitScene();initialScreen=Screen(view,800,500,45);
Press(new Point(300,300),MouseButton.Right);Move(new Point(315,290),RawInputModifiers.RightMouseButton);shiftedScreen=Screen(view,800,500,45);
Near(shiftedScreen.X-initialScreen.X,15,"right drag pans X");Near(shiftedScreen.Y-initialScreen.Y,-10,"right drag pans Y");Release(new Point(315,290),MouseButton.Right);
view.FitScene();Input("OnPointerWheelChanged",new PointerWheelEventArgs(view,pointer,view,new Point(400,300),0,new PointerPointProperties(),KeyModifiers.None,new Vector(0,1)));
Check(view.ZoomText=="115%","mouse wheel changes 3D zoom");
view.FitScene();Press(new Point(400,300),MouseButton.Left);view.FitScene();Check(pointer.Captured is null,"fit during drag releases capture");
view.Show3D=false;initialScreen=Screen(view,800,500,45);
Press(new Point(300,300),MouseButton.Left);Move(new Point(320,315),RawInputModifiers.LeftMouseButton);shiftedScreen=Screen(view,800,500,45);
Near(shiftedScreen.X-initialScreen.X,20,"2D left drag pans X");Near(shiftedScreen.Y-initialScreen.Y,15,"2D left drag pans Y");Release(new Point(320,315),MouseButton.Left);view.Show3D=true;
gestureHost.Content=null;gestureHost.Close();view.Measure(new Size(800,600));view.Arrange(new Rect(0,0,800,600));

// Even the largest view-only height scale must keep a narrow, tall KII inside the viewport on focus.
var tallScene=new Scene("tall",SceneMode.ThreeD,new Box3D(new Point3D(0,0,0),new Size3D(10000,10000,50)),
 new SceneObject("kii-tall",SceneObjectType.Kii,new Box3D(new Point3D(4975,4975,0),new Size3D(50,50,30))));
view.Scene=tallScene;view.HeightScaleIndex=2;view.FocusKii();
Check(Projection(view).Plot.Contains(KiiBounds(view)),"focus fits the entire tall KII at height x25");
Snapshot(view,"tall-focus.png");

// Integration: controls operate on the selected scene, not on pending generation form settings.
var vm=new MainViewModel{Scenes=Array.AsReadOnly(new[]{scene,MakeScene(false)}),SelectedScene=scene,IsThreeD=false};
var window=new MainWindow{DataContext=vm};window.Measure(new Size(1280,800));window.Arrange(new Rect(0,0,1280,800));
var actualView=window.GetLogicalDescendants().OfType<SceneView>().Single();
Check(actualView.Is3DActive,"selected scene determines view despite pending 2D form");
Check(window.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text=="Объёмный вид"),"view title binding");
var toggle=window.GetLogicalDescendants().OfType<ToggleSwitch>().Single(t=>t.OnContent is "3D"&&t.OffContent is "Сверху");
var scalePicker=window.GetLogicalDescendants().OfType<ComboBox>().Single();
scalePicker.SelectedIndex=2;Check(actualView.HeightMultiplier==25,"height combo binding");
toggle.IsChecked=false;Check(!actualView.Is3DActive,"toggle changes display only");
toggle.IsChecked=true;Check(actualView.Is3DActive,"toggle restores 3D display");
typeof(MainWindow).GetMethod("OnFocusKii",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[null,new Avalonia.Interactivity.RoutedEventArgs()]);
Point focusedInWindow=KiiBounds(actualView).Center;
Near(focusedInWindow.X,actualView.Bounds.Width/2,"focus button centers KII X",.01);
vm.SelectedScene=vm.Scenes[1];Check(!actualView.Has3DScene&&!toggle.IsVisible,"2D hides 3D toggle");
vm.SelectedScene=scene;Check(actualView.Is3DActive&&toggle.IsVisible,"returning to 3D restores controls");
Application.Current!.RequestedThemeVariant=ThemeVariant.Dark;
Check(((ISolidColorBrush)actualView.KiiBrush).Color==Color.Parse("#9B7CFF"),"3D uses violet dark theme accent");
var dark=View(scene);dark.KiiBrush=actualView.KiiBrush;dark.ObstacleBrush=actualView.ObstacleBrush;
dark.GridBrush=actualView.GridBrush;dark.TextBrush=actualView.TextBrush;dark.InfoBackground=actualView.InfoBackground;dark.HeightScaleIndex=1;
Snapshot(dark,"3d-dark.png");
dark.RotateView(80,10);Snapshot(dark,"3d-rotated.png");
window.Measure(new Size(1100,650));window.Arrange(new Rect(0,0,1100,650));
Check(actualView.Bounds.Width>200&&actualView.Bounds.Height>100,"viewer has usable area at minimum window size");

// Capture the actual interface with a normal generated scene, not just the geometric test fixture.
var settings=new JsonGenerationSettingsReader().Read(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../SceneGenerator.Console/settings.json")));
var generated=new GeneratedSceneSource(settings).GetScenes().First();
var demoVm=new MainViewModel(new StorageOutputFolderPicker(window),new WindowConfirmationService(window),new StorageSettingsFilePicker(window)){
 Scenes=Array.AsReadOnly(new[]{generated}),SelectedScene=generated,IsThreeD=true};
window.DataContext=demoVm;
actualView.HeightScaleIndex=1;
window.RequestedThemeVariant=ThemeVariant.Dark;window.Width=1280;window.Height=800;
window.Measure(new Size(1280,800));window.Arrange(new Rect(0,0,1280,800));Avalonia.Threading.Dispatcher.UIThread.RunJobs();
var content=(Control)window.Content!;
using(var fullImage=new RenderTargetBitmap(new PixelSize(1280,800),new Vector(96,96))){fullImage.Render(content);fullImage.Save(Path.Combine(artifacts,"window-3d-dark.png"),PngBitmapEncoderOptions.Default);}
actualView.FocusKii();
using(var fullImage=new RenderTargetBitmap(new PixelSize(1280,800),new Vector(96,96))){fullImage.Render(content);fullImage.Save(Path.Combine(artifacts,"window-3d-focus.png"),PngBitmapEncoderOptions.Default);}
Application.Current.RequestedThemeVariant=ThemeVariant.Light;window.RequestedThemeVariant=ThemeVariant.Light;Avalonia.Threading.Dispatcher.UIThread.RunJobs();
using(var fullImage=new RenderTargetBitmap(new PixelSize(1280,800),new Vector(96,96))){fullImage.Render(content);fullImage.Save(Path.Combine(artifacts,"window-3d-light.png"),PngBitmapEncoderOptions.Default);}
window.Close();
Console.WriteLine($"{checks} проверок пройдено: проекция, обратное преобразование, стены и крыши, дворы и вогнутые контуры, перекрытие, камера, масштаб, темы, интерфейс и неизменность JSON.");

