using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckProfileChrome()
    {
        var main = new MainWindow();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
            var paneType = typeof(MainWindow).GetNestedType("TerminalPane", BindingFlags.NonPublic)!;
            var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
            var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(main)!;
            foreach (var profile in Enum.GetValues<ProfileIconKind>())
            {
                var image = ProfileIcons.Get(profile);
                if (!image.IsFrozen || !ReferenceEquals(image, ProfileIcons.Get(profile)) || image.Width <= 0)
                    throw new Exception("Profile icon must be frozen, cached and nonempty.");
                var pane = Activator.CreateInstance(paneType, tabs.Count + 1, Enum.ToObject(profileType, (int)profile), profile.ToString())!;
                tabs.Add(Activator.CreateInstance(tabType, pane)!);
            }
            typeof(MainWindow).GetField("_activeTab", flags)!.SetValue(main, tabs[0]);
            typeof(MainWindow).GetMethod("RebuildTabStrip", flags)!.Invoke(main, null);
            var strip = (StackPanel)main.FindName("TabStrip");
            if (strip.Children.OfType<Grid>().Any(tab => tab.Height != 32)) throw new Exception("Tabs did not use the compact height.");
            for (var i = 0; i < strip.Children.Count; i++)
            {
                var images = Descendants(strip.Children[i]).OfType<Image>().ToArray();
                if (images.Length != 1 || !ReferenceEquals(images[0].Source, ProfileIcons.Get((ProfileIconKind)i)))
                    throw new Exception("Tab icon does not match its profile.");
            }
            var add = (Button)main.FindName("NewTabButton"); var profiles = (Button)main.FindName("ProfileMenuButton");
            if (!ReferenceEquals(add.Parent, profiles.Parent) || ((FrameworkElement)add.Parent).Parent != main.FindName("NewTerminalControls") ||
                add.Content is not System.Windows.Shapes.Path || profiles.Content is not System.Windows.Shapes.Path)
                throw new Exception("New terminal actions must be vector icons in one shared control.");
            if (!add.Focusable || !profiles.Focusable) throw new Exception("New terminal actions must remain keyboard accessible.");
            var menu = (ContextMenu)typeof(MainWindow).GetMethod("CreateProfileMenu", flags)!.Invoke(main, null)!;
            if (menu.Items.OfType<MenuItem>().Any(item => item.Icon is not Image)) throw new Exception("Profile menu is missing profile icons.");
            var root = (FrameworkElement)main.Content;
            tabType.GetProperty("BackgroundColor")!.SetValue(tabs[0], Color.FromRgb(111,75,32));
            tabType.GetProperty("BackgroundColor")!.SetValue(tabs[1], Colors.Yellow);
            typeof(MainWindow).GetField("_activeTab", flags)!.SetValue(main, tabs[1]);
            var autoHide = typeof(MainWindow).GetMethod("ApplyAutoHideMenu", flags)!;
            autoHide.Invoke(main, [false, false]);
            typeof(MainWindow).GetMethod("RebuildTabStrip", flags)!.Invoke(main, null);
            root.Measure(new Size(980,620)); root.Arrange(new Rect(0,0,980,620)); root.UpdateLayout();
            for (var i = 0; i < strip.Children.Count; i++)
            {
                var tab = (Grid)strip.Children[i];
                var background = tab.Children.OfType<System.Windows.Shapes.Path>().Single();
                if (background.Stroke is not null || background.StrokeThickness != 0)
                    throw new Exception("Tabs must have no outline.");
                if (background.Data.FillContains(new Point(4.1,.1)) || !background.Data.FillContains(new Point(12,1)))
                    throw new Exception("Every tab must keep its rounded top corners.");
                if (background.Data.Bounds.X != (i == 1 ? 0 : 4) || Panel.GetZIndex(tab) != (i == 1 ? 10 : 0))
                    throw new Exception("Only the selected tab should have overlapping bottom corners.");
                if (i == 0 && ((SolidColorBrush)background.Fill).Color == Color.FromRgb(111,75,32) ||
                    i == 1 && ((SolidColorBrush)background.Fill).Color != Colors.Yellow)
                    throw new Exception("Inactive colors should be muted while the selected color remains unchanged.");
                if (tab.Margin.Left != -4 || tab.Margin.Right != -4) throw new Exception("Tab overlap spacing changed.");
            }
            SaveChrome(root, "tab-overlap.png", 980, 80);
            var group = (Border)main.FindName("NewTerminalControls");
            var mouseOverKey = (DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            group.SetValue(mouseOverKey, true);
            foreach (var hovered in new[] { add, profiles })
            {
                hovered.SetValue(mouseOverKey, true); hovered.ApplyTemplate();
                var sibling = ReferenceEquals(hovered, add) ? profiles : add; sibling.ApplyTemplate();
                var hoveredBackground = (Border)hovered.Template.FindName("Segment", hovered);
                var siblingBackground = (Border)sibling.Template.FindName("Segment", sibling);
                if (hoveredBackground.BorderThickness != new Thickness(0) || hovered.FocusVisualStyle is not null)
                    throw new Exception("Terminal actions must not draw a click/focus border.");
                if (((SolidColorBrush)group.Background).Color.A != 0 || ((SolidColorBrush)siblingBackground.Background).Color.A != 0 ||
                    ((SolidColorBrush)hoveredBackground.Background).Color.A == 0)
                    throw new Exception("Hover changed both halves of the terminal actions.");
                root.UpdateLayout();
                SaveChrome(root, ReferenceEquals(hovered, add) ? "tab-hover-add.png" : "tab-hover-profiles.png", 980,80);
                hovered.SetValue(mouseOverKey, false);
            }
            group.SetValue(mouseOverKey, false);
            var close = Descendants(strip.Children[1]).OfType<Button>().Single(b => b.Name == "CloseTabButton");
            close.ApplyTemplate();
            var closeBackground = (Border)close.Template.FindName("CloseBackground", close);
            if (closeBackground.CornerRadius != new CornerRadius(3) || close.Width != close.Height ||
                close.Content is not System.Windows.Shapes.Path { Width: 8, Height: 8 })
                throw new Exception("Tab close must use a small vector cross with a rounded-square hover surface.");
            close.SetValue(mouseOverKey, true); root.UpdateLayout();
            SaveChrome(root, "tab-hover-close.png", 980, 80);
            close.SetValue(mouseOverKey, false);
            autoHide.Invoke(main, [true, false]);
            var selectedShape = ((Grid)strip.Children[1]).Children.OfType<System.Windows.Shapes.Path>().Single();
            if (((SolidColorBrush)selectedShape.Fill).Color != ((SolidColorBrush)main.FindResource("TerminalBackgroundBrush")).Color)
                throw new Exception("The selected tab must match the terminal background with auto-hide enabled.");
            autoHide.Invoke(main, [false, false]);
            selectedShape = ((Grid)strip.Children[1]).Children.OfType<System.Windows.Shapes.Path>().Single();
            if (((SolidColorBrush)selectedShape.Fill).Color != Colors.Yellow)
                throw new Exception("Turning off auto-hide must restore the selected tab's custom color.");
            Console.WriteLine("PASS native cached profile icons, compact tabs, grouped vector actions and accessible profile menu");
        }
        finally { main.Close(); }
    }
}
