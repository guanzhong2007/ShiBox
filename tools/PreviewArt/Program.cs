using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Draw a documentation illustration, never capture the user's desktop or data.
class PreviewArt
{
    static DrawingContext dc = null!;
    static SolidColorBrush C(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    static void Box(double x,double y,double w,double h,string color,double radius=0,string? stroke=null) =>
        dc.DrawRoundedRectangle(C(color),stroke==null?null:new Pen(C(stroke),1),new Rect(x,y,w,h),radius,radius);
    static void Text(string text,double x,double y,double size,string color,bool bold=false)
    {
        var f = new FormattedText(text,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,C(color),1);
        dc.DrawText(f,new Point(x,y));
    }
    static void Folder(double x,double y)
    {
        Box(x+8,y+18,224,190,"#08131D",17);
        Box(x,y,82,34,"#E5AB42",10);
        Box(x,y+18,222,171,"#D79D37",12);
        Box(x+13,y+19,195,157,"#D8D8CB",6);
        Box(x+8,y+24,207,155,"#F0EEE3",6);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(255,216,117),Color.FromRgb(226,166,58),65),new Pen(C("#FFE5A3"),1),new Rect(x-1,y+46,224,145),12,12);
        Text("开发项目",x+49,y+99,25,"#614516",true);
        Box(x+197,y-28,28,25,"#304451",6);Text("▤",x+204,y-29,18,"#E7EEF4");
        Box(x+1,y+204,222,38,"#23303C",8,"#526B7A");Text("⌕",x+13,y+208,20,"#91A9BB");Text("搜索收纳",x+43,y+212,14,"#8FA4B5");
    }
    static void Panel(double x,double y,bool search)
    {
        Box(x+5,y+10,354,332,"#0A1520",18);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(35,50,61),Color.FromRgb(23,35,45),90),new Pen(C("#536D7B"),1),new Rect(x,y,354,332),16,16);
        Text(search?"搜索结果":"全部收纳",x+20,y+17,19,"#F3D99A",true);
        Box(x+308,y+15,27,27,"#304451",6);Text("×",x+315,y+14,19,"#D2E0E9");
        Text(search?"找到 3 项 · 输入即筛选":"6 项 · 点击打开，放回恢复桌面",x+20,y+54,12,"#9DB1C1");
        string[] names=search?["开发项目","设计素材","常用文档"]:["开发项目","设计素材","常用文档","游戏快捷方式","灵感收藏"];
        for(int i=0;i<names.Length;i++) {var py=y+91+i*43;Box(x+20,py,237,34,i==1?"#486575":"#304451",7);Text("▱",x+31,py+5,17,"#B9CDD7");Text(names[i],x+58,py+7,13,"#E7EEF4");Box(x+265,py,57,34,"#304451",7);Text("放回",x+280,py+8,12,"#D1DFE7");}
        if(!search){Box(x+334,y+94,5,215,"#182B36",3);Box(x+334,y+95,5,105,"#617E8D",3);}
        else Text("仅搜索已收纳的名称",x+20,y+254,12,"#6E8A9B");
    }
    [STAThread] static void Main(string[] args)
    {
        var visual=new DrawingVisual();using(var context=visual.RenderOpen()){
            dc=context;
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(17,28,39),Color.FromRgb(32,52,65),35),null,new Rect(0,0,1600,1000));
            for(int x=40;x<1600;x+=48)for(int y=40;y<1000;y+=48)dc.DrawEllipse(C("#29404E"),null,new Point(x,y),.8,.8);
            Box(90,68,179,31,"#293F4D",15);Text("WINDOWS  /  WPF",105,74,12,"#A4BECC");
            Text("拾匣",86,127,63,"#F2F5F4",true);Text("ShiBox",247,144,40,"#9FB6C6");
            Text("让常用文件夹，留在手边。",90,218,27,"#B9CBD5");
            Box(1115,83,395,42,"#243C49",21);Text("滚轮切换  /  名称搜索  /  系统打开",1138,94,14,"#B7CDD8");
            Box(90,300,1420,462,"#172936",23,"#375160");
            Text("01  桌面收纳",124,328,15,"#9AB3C2");Text("02  就地展开",611,328,15,"#9AB3C2");Text("03  实时搜索",1074,328,15,"#9AB3C2");
            Folder(215,416);Panel(587,390,false);Panel(1073,390,true);
            Text("滚动翻阅 · 双击打开当前项",174,704,14,"#829FAD");
            dc.DrawLine(new Pen(C("#415B68"),1),new Point(524,396),new Point(524,718));
            dc.DrawLine(new Pen(C("#415B68"),1),new Point(1009,396),new Point(1009,718));
            string[] title=["拖入收纳","轻量切换","随时放回"];
            string[] sub=["桌面项目集中管理，目录保持平铺","格子吸附定位，记住上次的位置","保留原位置记录，冲突时不覆盖"];
            for(int i=0;i<3;i++){var x=94+i*480;Text($"0{i+1}",x,814,17,"#D9B96F");Text(title[i],x+45,810,22,"#E2EBEF",true);Text(sub[i],x+45,852,14,"#91ACBB");}
            Text("MIT 开源  ·  C# / WPF  ·  Windows x64",92,945,13,"#718E9E");
            Text("功能示意图 · 虚构示例数据 · 非桌面实拍",1118,945,12,"#718E9E");
        }
        var bitmap=new RenderTargetBitmap(1600,1000,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);using(var file=File.Create(output))encoder.Save(file);
        Console.WriteLine(output);
    }
}
