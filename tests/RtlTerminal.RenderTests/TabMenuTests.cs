using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckTabMenusAndColors()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var window = new MainWindow();
        typeof(MainWindow).GetMethod("ApplyAutoHideMenu", flags)!.Invoke(window, [false, false]);
        var tabs = (IList)typeof(MainWindow).GetField("_tabs",flags)!.GetValue(window)!;
        var tabType = typeof(MainWindow).GetNestedType("TerminalTab",BindingFlags.NonPublic)!;
        var profile = typeof(MainWindow).GetNestedType("TerminalProfile",BindingFlags.NonPublic)!;
        object? Call(string name,params object?[] args) => typeof(MainWindow).GetMethods(flags)
            .Single(m=>m.Name==name && m.GetParameters().Length==args.Length).Invoke(window,args);
        object NewTab()
        {
            var pane=Call("CreatePane",Enum.ToObject(profile,0))!;
            var tab=Activator.CreateInstance(tabType,pane)!; tabs.Add(tab); return tab;
        }
        var first=NewTab(); Call("ShowTab",first);
        var second=NewTab(); Call("ShowTab",second);
        var root=(FrameworkElement)window.Content;
        void Layout()
        {
            root.Measure(new Size(980,620)); root.Arrange(new Rect(0,0,980,620)); root.UpdateLayout();
            root.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
        }
        MenuItem Item(ContextMenu menu,string header) => menu.Items.OfType<MenuItem>().Single(m=>Equals(m.Header,header));
        ContextMenu Menu(object tab)=>(ContextMenu)Call("CreateTabContextMenu",tab)!;
        Color? ColorOf(object tab)=>(Color?)tabType.GetProperty("BackgroundColor")!.GetValue(tab);
        var menu=Menu(first);
        if(window.FindName("SplitPaneButton") is not null || !ReferenceEquals(Item(menu,"Split tab").Tag,first))
            throw new Exception("Split was not moved to the target tab context menu");
        var colors=Item(menu,"Color...");
        var swatches=(UniformGrid)((MenuItem)colors.Items[0]).Header;
        var yellow=swatches.Children.OfType<Button>().Single(b=>Equals(b.ToolTip,"Yellow"));
        var color=(Color)yellow.Tag;
        yellow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout();
        if(ColorOf(first)!=color || ColorOf(second) is not null || !ReferenceEquals(typeof(MainWindow).GetField("_activeTab",flags)!.GetValue(window),second))
            throw new Exception("Coloring an inactive tab changed the wrong tab or selection");
        var strip=(StackPanel)window.FindName("TabStrip");
        var firstGrid=(Grid)strip.Children[0];
        var path=firstGrid.Children.OfType<System.Windows.Shapes.Path>().Single();
        if(((SolidColorBrush)path.Fill).Color==color || ((SolidColorBrush)path.Fill).Color.A != 255 || firstGrid.ContextMenu is null)
            throw new Exception("Inactive tab must retain a muted opaque color and its context menu");
        var panel=firstGrid.Children.OfType<DockPanel>().Single();
        var label=panel.Children.OfType<Button>().Single(b=>b.Content is DockPanel);
        var close=panel.Children.OfType<Button>().Single(b=>b.Name=="CloseTabButton");
        if(((SolidColorBrush)label.Foreground).Color!=Colors.Black || ((SolidColorBrush)close.Foreground).Color!=Colors.Black)
            throw new Exception("Bright tab colors have unreadable text or close control");
        AppTheme.Apply(AppThemeKind.Light); Call("RebuildTabStrip"); Layout();
        if(ColorOf(first)!=color) throw new Exception("Theme switching lost custom tab colors");
        Call("SelectTab",first); Layout();
        if(ColorOf(first)!=color) throw new Exception("Selecting a tab lost its color");
        var selectedPath=((Grid)strip.Children[0]).Children.OfType<System.Windows.Shapes.Path>().Single();
        if(((SolidColorBrush)selectedPath.Fill).Color!=color || selectedPath.Stroke is not null)
            throw new Exception("Selected tab must use its original color without an outline");
        menu=Menu(second); colors=Item(menu,"Color...");
        swatches=(UniformGrid)((MenuItem)colors.Items[0]).Header;
        swatches.Children.OfType<Button>().Single(b=>Equals(b.ToolTip,"Blue")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        AppTheme.Apply(AppThemeKind.Dark); Call("RebuildTabStrip"); Layout();
        var bitmap=new RenderTargetBitmap(980,80,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var stream=File.Create("artifacts/render-checks/tab-colors.png")) encoder.Save(stream);
        menu=Menu(first); colors=Item(menu,"Color...");
        var reset=colors.Items.OfType<MenuItem>().Single(m=>Equals(m.Header,"Reset color"));
        if(!reset.IsEnabled)throw new Exception("Custom tab color cannot be reset");
        reset.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Layout();
        if(ColorOf(first) is not null || ColorOf(second) is null)throw new Exception("Reset changed another tab's color");
        typeof(MainWindow).GetField("_paneShortcuts",flags)!.SetValue(window,new PaneShortcutSettings(Split:false));
        if(Item(Menu(second),"Split tab").InputGestureText!="")throw new Exception("Disabled shortcut still advertised in tab menu");
        window.Close();
        Console.WriteLine("PASS tab context split targeting, independent colors, readable labels, theme/selection retention and color reset");
    }
}
