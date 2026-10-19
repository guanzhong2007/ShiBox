using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ShiBox;
public class State { public List<string> Paths {get;set;} = new(); public Dictionary<string,string> Originals {get;set;} = new(); public double X {get;set;}=100; public double Y {get;set;}=160; public int Index {get;set;} }
public class Box:Window {
 [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
 [DllImport("user32.dll")] static extern uint GetDpiForSystem();
 readonly string store=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ShiBox","layout.json");
 double cellX,cellY; Point? dragStart; double startLeft,startTop; bool moved;
 static readonly string collectionRoot=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"拾匣收纳");
 readonly CollectionStorage storage=new(collectionRoot,new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}.Where(p=>!string.IsNullOrWhiteSpace(p)),System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ShiBox","receipts"));
 State state=new(); Grid slot=new(); TextBlock currentName=new(); TextBox search=new(); Button expandButton=new(); Border searchAnchor=new(); Popup allPopup=new(), searchPopup=new(); StackPanel allRows=new(), searchRows=new();  bool busy; DateTime last=DateTime.MinValue;
 public Box(){
  try { if(File.Exists(store))state=JsonSerializer.Deserialize<State>(File.ReadAllText(store))??new(); }catch{}
  foreach(var receipt in storage.Recover()) {state.Paths.RemoveAll(p=>string.Equals(p,receipt.Original,StringComparison.OrdinalIgnoreCase));if(!state.Paths.Contains(receipt.Stored,StringComparer.OrdinalIgnoreCase))state.Paths.Add(receipt.Stored);state.Originals[receipt.Stored]=receipt.Original;}
  double dpi=Math.Max(96,GetDpiForSystem());Width=Math.Clamp(GetSystemMetrics(38)*96/dpi*2,136,192);Height=Math.Clamp(GetSystemMetrics(39)*96/dpi*2,174,210);Left=Math.Clamp(state.X,SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-Width);Top=Math.Clamp(state.Y,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-Height);
  Title="拾匣 · 桌面收纳";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=false;AllowDrop=true;
  cellX=GetSystemMetrics(38)*96/dpi;cellY=GetSystemMetrics(39)*96/dpi;
  var layout=new Grid();layout.RowDefinitions.Add(new(){Height=new GridLength(19)});layout.RowDefinitions.Add(new());layout.RowDefinitions.Add(new(){Height=new GridLength(29)});Content=layout;
  var head=new Grid{Margin=new Thickness(8,0,7,0)};head.ColumnDefinitions.Add(new());head.ColumnDefinitions.Add(new(){Width=new GridLength(23)});
  currentName=Text("文件收纳",11,"#FFFFFF");currentName.VerticalAlignment=VerticalAlignment.Center;currentName.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=3,ShadowDepth=1,Opacity=.85};currentName.Visibility=Visibility.Collapsed;
  var expand=MakeButton("▤",()=>ShowList());expandButton=expand;expand.FontSize=13;expand.ToolTip="展开全部收纳";Grid.SetColumn(expand,1);head.Children.Add(expand);layout.Children.Add(head);
  slot.Margin=new Thickness(10,3,10,5);Grid.SetRow(slot,1);layout.Children.Add(slot);
  var searchBorder=new Border{Margin=new Thickness(8,2,8,1),Padding=new Thickness(7,3,7,3),CornerRadius=new CornerRadius(7),Background=Brush("#E623303C"),BorderBrush=Brush("#667B8998"),BorderThickness=new Thickness(1)};
  search=new TextBox{FontSize=10,Foreground=Brush("#E3EAF0"),Background=Brushes.Transparent,BorderThickness=new Thickness(0),ToolTip="实时模糊搜索收纳名称；回车选中第一项；Esc 关闭",VerticalContentAlignment=VerticalAlignment.Center};
  var searchContent=new Grid();var placeholder=Text("⌕ 搜索收纳",10,"#8FA4B5");placeholder.IsHitTestVisible=false;placeholder.VerticalAlignment=VerticalAlignment.Center;searchContent.Children.Add(search);searchContent.Children.Add(placeholder);search.TextChanged+=(_,_)=>placeholder.Visibility=string.IsNullOrEmpty(search.Text)?Visibility.Visible:Visibility.Collapsed;searchBorder.Child=searchContent;Grid.SetRow(searchBorder,2);layout.Children.Add(searchBorder);
  search.TextChanged+=(_,_)=>RefreshSearch();search.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){var match=Matching(search.Text).FirstOrDefault();if(match!=null){SelectPath(match);searchPopup.IsOpen=false;}e.Handled=true;}else if(e.Key==Key.Escape){search.Clear();searchPopup.IsOpen=false;e.Handled=true;}else if(e.Key==Key.Down){searchPopup.Child?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));e.Handled=true;}};
  searchBorder.ToolTip="输入即显示名称匹配结果";searchAnchor=searchBorder;
  allPopup=MakePopup(expandButton,PlacementMode.Left);searchPopup=MakePopup(searchBorder,PlacementMode.Bottom);
  search.GotKeyboardFocus+=(_,_)=>RefreshSearch();
  MouseLeftButtonDown+=(_,e)=>{if(IsControl(e.OriginalSource as DependencyObject))return;if(e.ClickCount==2){dragStart=null;ReleaseMouseCapture();Open();e.Handled=true;return;}allPopup.IsOpen=false;searchPopup.IsOpen=false;dragStart=CursorPoint();startLeft=Left;startTop=Top;moved=false;CaptureMouse();e.Handled=true;};
  MouseMove+=(_,e)=>{if(dragStart is not Point origin||e.LeftButton!=MouseButtonState.Pressed)return;var now=CursorPoint();var factor=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;var delta=factor.Transform(now-origin);if(!moved&&Math.Abs(delta.X)<5&&Math.Abs(delta.Y)<5)return;moved=true;Cursor=Cursors.SizeAll;Snap(startLeft+delta.X,startTop+delta.Y);};
  MouseLeftButtonUp+=(_,e)=>{if(dragStart is Point origin){var now=CursorPoint();var factor=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;var delta=factor.Transform(now-origin);dragStart=null;ReleaseMouseCapture();Cursor=Cursors.Arrow;Snap(moved?startLeft+delta.X:Left,moved?startTop+delta.Y:Top);Save();e.Handled=true;}};
  LostMouseCapture+=(_,_)=>{dragStart=null;Cursor=Cursors.Arrow;};
  MouseWheel+=(_,e)=>{e.Handled=true;if((DateTime.UtcNow-last).TotalMilliseconds<420)return;last=DateTime.UtcNow;Step(e.Delta<0?1:-1);};
  DragOver+=(_,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;};
  Drop+=(_,e)=>{if(busy){e.Handled=true;return;}if(e.Data.GetData(DataFormats.FileDrop) is string[] paths)Import(paths);e.Handled=true;};
  var menu=new ContextMenu();AddMenu(menu,"打开当前项目",Open);AddMenu(menu,"查看全部 / 搜索",ShowList);AddMenu(menu,"收纳已添加的桌面原件",()=>Import(state.Paths.ToArray()));AddMenu(menu,"放回原处并移出收纳（引用仅移除）",RemoveCurrent);AddMenu(menu,"打开收纳存储目录",()=>{Directory.CreateDirectory(collectionRoot);Process.Start(new ProcessStartInfo(collectionRoot){UseShellExecute=true});});AddMenu(menu,"退出拾匣",Close);ContextMenu=menu;
  Closing+=(_,_)=>{allPopup.IsOpen=false;searchPopup.IsOpen=false;Save();};Loaded+=(_,_)=>{Snap(Left,Top);Save();};Render();
 }
 static SolidColorBrush Brush(string s)=>(SolidColorBrush)new BrushConverter().ConvertFromString(s)!;
 static TextBlock Text(string s,double size,string color)=>new(){Text=s,FontSize=size,Foreground=Brush(color),FontFamily=new FontFamily("Microsoft YaHei UI"),TextTrimming=TextTrimming.CharacterEllipsis};
 void AddMenu(ContextMenu m,string title,Action action){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();m.Items.Add(item);}
 void Save(){state.X=Left;state.Y=Top;try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(store)!);File.WriteAllText(store+".tmp",JsonSerializer.Serialize(state));File.Move(store+".tmp",store,true);}catch(Exception ex){MessageBox.Show("配置未保存："+ex.Message,"拾匣");}}
 static bool IsControl(DependencyObject? d){while(d!=null){if(d is TextBox||d is Button||d is ScrollBar)return true;d=d is Visual?VisualTreeHelper.GetParent(d):null;}return false;}
 string DisplayName(string p)=>System.IO.Path.GetFileNameWithoutExtension(ItemName(p)).Length>0&&System.IO.Path.GetExtension(p).Equals(".lnk",StringComparison.OrdinalIgnoreCase)?System.IO.Path.GetFileNameWithoutExtension(ItemName(p)):ItemName(p);
 string ItemName(string p)=>System.IO.Path.GetFileName(p.TrimEnd('\\','/')) is {Length:>0} n?n:p;
 Grid Card(){
  var g=new Grid{RenderTransformOrigin=new Point(.5,1)};
  var tab=new Border{Width=49,Height=18,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,3,0,0),CornerRadius=new CornerRadius(8,12,0,0),Background=Brush("#E5AB42")};g.Children.Add(tab);
  g.Children.Add(new Border{Margin=new Thickness(0,12,0,0),CornerRadius=new CornerRadius(5,10,12,12),Background=Brush("#D79D37")});
  for(int i=0;i<2;i++)g.Children.Add(new Border{Margin=new Thickness(7+i*2,16-i*3,7+i*2,8),CornerRadius=new CornerRadius(5),Background=Brush(i==0?"#D8D8CB":"#F0EEE3"),RenderTransform=new RotateTransform(i==0?-3:2)});
  var cover=new Border{Margin=new Thickness(-1,29,-1,0),CornerRadius=new CornerRadius(8,8,12,12),BorderBrush=Brush("#FFE5A3"),BorderThickness=new Thickness(1),Background=new LinearGradientBrush(Color.FromRgb(255,216,117),Color.FromRgb(226,166,58),60)};
  var content=new Grid{Margin=new Thickness(10)};
  var caption=Text(state.Paths.Count>0?DisplayName(state.Paths[state.Index]):"拖入文件夹",13,"#614516");caption.TextAlignment=TextAlignment.Center;caption.TextWrapping=TextWrapping.Wrap;caption.MaxHeight=50;caption.VerticalAlignment=VerticalAlignment.Center;caption.FontWeight=FontWeights.SemiBold;content.Children.Add(caption);
  cover.Tag="cover";cover.RenderTransformOrigin=new Point(.5,1);cover.Child=content;g.Children.Add(cover);
  g.ToolTip=state.Paths.Count>0?state.Paths[state.Index]:"拖入文件夹或快捷方式 · 滚轮切换 · 双击打开 · 拖动自动吸附";
  return g;
 }
 void Snap(double x,double y){
  var area=SystemParameters.WorkArea;
  double gx=Math.Max(1,cellX),gy=Math.Max(1,cellY);
  var col=Math.Round((x-area.Left)/gx);var row=Math.Round((y-area.Top)/gy);
  col=Math.Clamp(col,0,Math.Max(0,Math.Floor((area.Width-Width)/gx)));row=Math.Clamp(row,0,Math.Max(0,Math.Floor((area.Height-Height)/gy)));
  double targetX=area.Left+col*gx,targetY=area.Top+row*gy;
  // No animated HWND chasing: keep the captured cursor anchored and move only on cell changes.
  if(Math.Abs(Left-targetX)>.1)Left=targetX;if(Math.Abs(Top-targetY)>.1)Top=targetY;
 }
 void Render(){slot.Children.Clear();state.Index=state.Paths.Count==0?0:Math.Clamp(state.Index,0,state.Paths.Count-1);currentName.Text=state.Paths.Count==0?"拖入文件夹":DisplayName(state.Paths[state.Index]);currentName.ToolTip=state.Paths.Count==0?"拖入桌面项目收纳":state.Paths[state.Index];slot.Children.Add(Card());}
 void Step(int delta){
  if(busy||state.Paths.Count<2)return;busy=true;
  var old=(Grid)slot.Children[0];state.Index=(state.Index+delta+state.Paths.Count)%state.Paths.Count;Render();var incoming=(Grid)slot.Children[0];slot.Children.Add(old);old.IsHitTestVisible=false;
  var ease=new CubicEase{EasingMode=EasingMode.EaseInOut};var duration=TimeSpan.FromMilliseconds(380);
  var motion=new TransformGroup();var tilt=new RotateTransform();var shift=new TranslateTransform();motion.Children.Add(tilt);motion.Children.Add(shift);old.RenderTransform=motion;old.RenderTransformOrigin=new Point(.5,.85);
  // Keep the folder silhouette intact; only the cover opens, then the next folder slides in.
  var cover=old.Children.OfType<Border>().FirstOrDefault(b=>Equals(b.Tag,"cover"));
  if(cover!=null){var hinge=new TransformGroup();var skew=new SkewTransform();var scale=new ScaleTransform();hinge.Children.Add(skew);hinge.Children.Add(scale);cover.RenderTransform=hinge;skew.BeginAnimation(SkewTransform.AngleXProperty,new DoubleAnimation(0,-delta*7,TimeSpan.FromMilliseconds(210)){EasingFunction=ease});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(1,.87,TimeSpan.FromMilliseconds(230)){EasingFunction=ease});}
  tilt.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,-delta*7,duration){EasingFunction=ease});shift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,-delta*17,duration){EasingFunction=ease});
  var moveIn=new TranslateTransform(0,delta*13);incoming.RenderTransform=moveIn;incoming.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,duration){EasingFunction=ease});moveIn.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(delta*13,0,duration){EasingFunction=ease});
  var fade=new DoubleAnimationUsingKeyFrames();fade.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.Zero)));fade.KeyFrames.Add(new LinearDoubleKeyFrame(.9,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130))));fade.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromTimeSpan(duration),ease));fade.Completed+=(_,_)=>{slot.Children.Remove(old);busy=false;Save();};old.BeginAnimation(OpacityProperty,fade);
 }
 void Import(IEnumerable<string> paths){
  var errors=new List<string>();int references=0;
  foreach(var source in paths){try{
   if(!File.Exists(source)&&!Directory.Exists(source))continue;
   var already=state.Paths.FindIndex(p=>string.Equals(p,source,StringComparison.OrdinalIgnoreCase));
   if(state.Originals.ContainsKey(source))continue;
   if(storage.IsDesktopItem(source)){
    var receipt=storage.Collect(source);state.Originals[receipt.Stored]=receipt.Original;
    if(already>=0)state.Paths[already]=receipt.Stored;else state.Paths.Add(receipt.Stored);
   }else if(already<0){state.Paths.Add(source);references++;}
  }catch(Exception ex){errors.Add(ItemName(source)+"："+ex.Message);}}
  state.Index=Math.Max(0,state.Paths.Count-1);Save();Render();
  if(errors.Count>0)MessageBox.Show(string.Join("\n",errors),"部分项目未收纳");
  if(references>0)MessageBox.Show("非桌面项目只添加引用，原文件保留原位。", "拾匣");
 }
 void RemoveCurrent(){if(busy||state.Paths.Count==0)return;var path=state.Paths[state.Index];try{if(state.Originals.TryGetValue(path,out var original)){storage.Restore(new Receipt(original,path));state.Originals.Remove(path);}state.Paths.RemoveAt(state.Index);state.Index=Math.Max(0,Math.Min(state.Index,state.Paths.Count-1));Save();Render();if(allPopup.IsOpen)ShowListContent();if(searchPopup.IsOpen)RefreshSearch();}catch(Exception ex){MessageBox.Show(ex.Message,"未移出收纳");}}
 void Open(){if(state.Paths.Count==0)return;OpenPath(state.Paths[state.Index]);}
 void OpenPath(string path){
  if(Directory.Exists(path)){
   allPopup.IsOpen=false;searchPopup.IsOpen=false;
   try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message,"无法打开文件夹");}return;
  }
  if(!File.Exists(path)){MessageBox.Show("项目当前不可用，原位置或快捷方式目标可能已改变。可先使用‘放回原处’恢复。\n"+path,"拾匣");return;}
  try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true,WorkingDirectory=System.IO.Path.GetDirectoryName(path)!});}catch(Exception ex){MessageBox.Show(ex.Message,"无法打开");}
 }
 [StructLayout(LayoutKind.Sequential)] struct CursorPosition {public int X;public int Y;}
 [DllImport("user32.dll")]static extern bool GetCursorPos(out CursorPosition point);
 static Point CursorPoint(){GetCursorPos(out var p);return new Point(p.X,p.Y);}
 Button MakeButton(string title,Action action){var button=new Button{Content=title,Foreground=Brush("#E7EEF4"),Background=Brush("#304451"),BorderThickness=new Thickness(0),Padding=new Thickness(7,4,7,4),Cursor=Cursors.Hand,FontSize=11};
  var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Button.BackgroundProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Button.PaddingProperty));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);template.VisualTree=border;button.Template=template;
  button.MouseEnter+=(_,_)=>button.Background=Brush("#486575");button.MouseLeave+=(_,_)=>button.Background=Brush("#304451");button.Click+=(_,e)=>{action();e.Handled=true;};return button;
 }
 Popup MakePopup(UIElement anchor,PlacementMode placement)=>new(){PlacementTarget=anchor,Placement=placement,AllowsTransparency=true,StaysOpen=false,PopupAnimation=PopupAnimation.Fade,HorizontalOffset=placement==PlacementMode.Left?-9:0,VerticalOffset=placement==PlacementMode.Bottom?6:0};
 Border Panel(string title,StackPanel rows,string subtitle,Action? back=null){
  var body=new DockPanel{Margin=new Thickness(14)};
  var header=new Grid{Margin=new Thickness(0,0,0,10)};header.ColumnDefinitions.Add(new());header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var heading=Text(title,14,"#F3D99A");heading.FontWeight=FontWeights.SemiBold;header.Children.Add(heading);var close=MakeButton("×",()=>{allPopup.IsOpen=false;searchPopup.IsOpen=false;});Grid.SetColumn(close,1);header.Children.Add(close);DockPanel.SetDock(header,Dock.Top);body.Children.Add(header);
  var note=Text(subtitle,10,"#9DB1C1");note.Margin=new Thickness(0,0,0,10);DockPanel.SetDock(note,Dock.Top);body.Children.Add(note);
  if(back!=null){var up=MakeButton("‹ 返回收纳",back);up.Margin=new Thickness(0,0,0,8);DockPanel.SetDock(up,Dock.Top);body.Children.Add(up);}
  body.Children.Add(new ScrollViewer{Style=(Style)Application.Current.FindResource("CollectionScroll"),Content=rows,MaxHeight=270,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
  return new Border{Width=286,CornerRadius=new CornerRadius(13),Background=new LinearGradientBrush(Color.FromRgb(35,50,61),Color.FromRgb(23,35,45),90),BorderBrush=Brush("#536D7B"),BorderThickness=new Thickness(1),Child=body};
 }
 IEnumerable<string> Matching(string query){var q=query.Trim().Replace(" ","");return state.Paths.Where(p=>Fuzzy(DisplayName(p),q));}
 static bool Fuzzy(string value,string query){if(query.Length==0)return true;int j=0;foreach(char c in value){if(char.ToUpperInvariant(c)==char.ToUpperInvariant(query[j])){j++;if(j==query.Length)return true;}}return false;}
 void SelectPath(string path){if(busy)return;var index=state.Paths.IndexOf(path);if(index<0)return;state.Index=index;Render();Save();}
 void AddRow(StackPanel rows,string path,bool collection){
  var row=new Grid{Margin=new Thickness(0,0,0,7)};row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
  var name=MakeButton((Directory.Exists(path)?"▱  ":"·  ")+DisplayName(path),()=>{if(collection)SelectPath(path);OpenPath(path);searchPopup.IsOpen=false;});name.HorizontalContentAlignment=HorizontalAlignment.Left;name.ToolTip=path;name.Content=new TextBlock{Text=(Directory.Exists(path)?"▱  ":"·  ")+DisplayName(path),TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=165,HorizontalAlignment=HorizontalAlignment.Left};row.Children.Add(name);
  if(collection){var owned=state.Originals.ContainsKey(path);var restore=MakeButton(owned?"放回":"移出",()=>{SelectPath(path);RemoveCurrent();});restore.ToolTip=owned?"恢复到原桌面位置并移出收纳":"仅移除引用，原文件不受影响";restore.Margin=new Thickness(6,0,0,0);Grid.SetColumn(restore,1);row.Children.Add(restore);}rows.Children.Add(row);
 }
 void RefreshSearch(){
  var matches=Matching(search.Text).ToList();searchRows=new StackPanel();foreach(var path in matches)AddRow(searchRows,path,true);
  if(matches.Count==0)searchRows.Children.Add(new TextBlock{Text="没有匹配项\n试试名称中的几个字",Foreground=Brush("#A8BAC7"),FontSize=12,Margin=new Thickness(2,8,2,12)});
  searchPopup.Child=Panel("搜索结果",searchRows,$"找到 {matches.Count} 项 · 输入即筛选");
  if(search.IsKeyboardFocusWithin||searchPopup.IsOpen){allPopup.IsOpen=false;searchPopup.IsOpen=true;}
 }
 void ShowListContent(){allRows=new StackPanel();foreach(var path in state.Paths)AddRow(allRows,path,true);if(state.Paths.Count==0)allRows.Children.Add(Text("拖入桌面项目开始收纳",12,"#A8BAC7"));allPopup.Child=Panel("全部收纳",allRows,$"{state.Paths.Count} 项 · 点击打开，放回恢复桌面");}
 void ShowList(){if(busy)return;searchPopup.IsOpen=false;if(allPopup.IsOpen){allPopup.IsOpen=false;return;}ShowListContent();allPopup.IsOpen=true;}
 [STAThread] public static void Main(string[] args){if(args.Length==2&&args[0]=="--storage-check"){StorageCheck.Run(args[1]);return;}using var instanceMutex=new System.Threading.Mutex(true,"Local\\ShiBox.Desktop.Instance",out var created);if(!created)return;var app=new Application();app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/ShiBox;component/ScrollTheme.xaml",UriKind.Relative)});var box=new Box();if(args.Contains("--review"))box.ShowInTaskbar=true;app.Run(box);}
}
