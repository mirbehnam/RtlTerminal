using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckSchemesAcrossPanes()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var window = new MainWindow();
        var tabs=(IList)typeof(MainWindow).GetField("_tabs",flags)!.GetValue(window)!;
        var tabType=typeof(MainWindow).GetNestedType("TerminalTab",BindingFlags.NonPublic)!;
        var profile=typeof(MainWindow).GetNestedType("TerminalProfile",BindingFlags.NonPublic)!;
        var direction=typeof(MainWindow).GetNestedType("PaneSplitDirection",BindingFlags.NonPublic)!;
        object? Call(string name,params object?[] args)=>typeof(MainWindow).GetMethods(flags).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(window,args);
        object Get(object o,string p)=>o.GetType().GetProperty(p)!.GetValue(o)!;
        void Set(object o,string p,object? v)=>o.GetType().GetProperty(p)!.SetValue(o,v);
        object NewTab()
        {
            var pane=Call("CreatePane",Enum.ToObject(profile,0))!;
            var tab=Activator.CreateInstance(tabType,pane)!;tabs.Add(tab);return tab;
        }
        var root=(FrameworkElement)window.Content;
        void Layout()
        {
            root.Measure(new Size(980,740));root.Arrange(new Rect(0,0,980,740));root.UpdateLayout();
            root.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
        }
        try
        {
            var target=NewTab();Call("ShowTab",target);Layout();
            var first=Get(target,"ActivePane");
            var right=Call("AddSplitPane",target,first,Enum.Parse(direction,"SideBySide"))!;Layout();
            var bottom=Call("AddSplitPane",target,right,Enum.Parse(direction,"Stacked"))!;Layout();
            var hiddenTab=NewTab();var hidden=Get(hiddenTab,"ActivePane");
            var panes=new[]{first,right,bottom,hidden};
            const string output="\x1b[?25lفارسی / English\r\n\x1b[31mANSI red \x1b[38;2;197;15;31mexact RGB\x1b[0m";
            foreach(var p in panes)
            {
                var buffer=new TerminalBuffer(60,16);Set(p,"Buffer",buffer);Call("Render",p,buffer.Process(output));
            }
            Layout();
            var selected=(TerminalView)Get(right,"View");selected.SelectAll();var selection=selected.GetSelectedText();
            var tabColor=Colors.Purple;Call("SetTabColor",target,tabColor);
            foreach(var scheme in ColorScheme.All)
            {
                // Ensure hidden tabs also have cached layout state to invalidate.
                foreach(var p in panes) typeof(TerminalView).GetMethod("Layout",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(Get(p,"View"),new object[]{0});
                AppTheme.Apply(scheme.Theme,scheme);Call("RefreshColorViews");
                foreach(var p in panes)
                {
                    var view=(TerminalView)Get(p,"View");
                    var layouts=(IDictionary)typeof(TerminalView).GetField("_layouts",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(view)!;
                    if(layouts.Count!=0)throw new Exception("Scheme left stale layout in a visible or hidden pane");
                    var bg=((SolidColorBrush)view.Background).Color;
                    if(bg!=Color.FromRgb(scheme.DefaultBackground.Red,scheme.DefaultBackground.Green,scheme.DefaultBackground.Blue))
                        throw new Exception($"Pane {Array.IndexOf(panes,p)} background {bg} ignored scheme {scheme.Name} / {scheme.DefaultBackground}");
                    var html=view.CreateHtmlPreview(HtmlPreviewScope.EntireHistory);
                    var expected=$"#{scheme.DefaultBackground.Red:X2}{scheme.DefaultBackground.Green:X2}{scheme.DefaultBackground.Blue:X2}";
                    var red=scheme.Text(1);
                    var ansi=$"#{red.Red:X2}{red.Green:X2}{red.Blue:X2}";
                    if(!html.Contains("background:"+expected)||!html.Contains("#C50F1F")||!html.Contains(ansi))throw new Exception("HTML scheme or exact RGB color was lost");
                }
                string OscColor(TerminalColor c) => $"{c.Red:X2}{c.Red:X2}/{c.Green:X2}{c.Green:X2}/{c.Blue:X2}{c.Blue:X2}".ToLowerInvariant();
                var replies=((TerminalBuffer)Get(hidden,"Buffer")).Process("\x1b]10;?\a\x1b]11;?\a").Responses;
                if(!replies.Contains("\x1b]10;rgb:"+OscColor(scheme.DefaultForeground)+"\x1b\\") ||
                   !replies.Contains("\x1b]11;rgb:"+OscColor(scheme.DefaultBackground)+"\x1b\\"))
                    throw new Exception("Terminal queries did not report the selected scheme defaults");
                if(!ReferenceEquals(Get(target,"ActivePane"),bottom)||selected.GetSelectedText()!=selection||(Color)Get(target,"BackgroundColor")!=tabColor)
                    throw new Exception("Changing scheme altered the active pane, selection or custom tab color");
                if (AppTheme.Current != scheme.Theme)
                    throw new Exception("Applied theme mode is out of sync");
                Layout();
                if(scheme.Name is "Dracula" or "One Half Light")
                {
                    var bitmap=new RenderTargetBitmap(980,740,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
                    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream=File.Create("artifacts/render-checks/scheme-panes-"+scheme.Name.Replace(' ','-')+".png");png.Save(stream);
                }
            }
            Call("ShowTab",hiddenTab);Layout();
            if(!((TerminalView)Get(hidden,"View")).CreateHtmlPreview(HtmlPreviewScope.EntireHistory).Contains("#EFF1F5"))
                throw new Exception("Hidden tab restored the previous color scheme");
            AppTheme.Apply(AppThemeKind.Dark);Call("RefreshColorViews");
            if(AppTheme.IsLight || AppTheme.Scheme is not null)
                throw new Exception("Default color reset failed");
            Console.WriteLine("PASS all schemes across horizontal/vertical/hidden panes, cache invalidation, HTML, selection, tab colors and menu state");
        }
        finally { window.Close();AppTheme.Apply(AppThemeKind.Dark); }
    }
}
