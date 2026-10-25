using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using RtlTerminal;
using Microsoft.Win32;

internal static partial class Program
{
    private static void CheckLivePaneSessions()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var historyRegistry = @"Software\RtlTerminal.Tests\PaneHistory-" + Guid.NewGuid().ToString("N");
        var window = new MainWindow(new DirectoryHistoryStore(historyRegistry));
        var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
        object? Call(string name, params object?[] args) => typeof(MainWindow).GetMethods(flags | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(window,args);
        object? Get(object o,string p) => o.GetType().GetProperty(p)!.GetValue(o);
        void Set(object o,string p,object? v) => o.GetType().GetProperty(p)!.SetValue(o,v);
        var tabType = typeof(MainWindow).GetNestedType("TerminalTab",BindingFlags.NonPublic)!;
        var profileType = typeof(MainWindow).GetNestedType("TerminalProfile",BindingFlags.NonPublic)!;
        var sessions = new List<ConPtySession>();
        var readers = new List<Task>();
        var root = (FrameworkElement)window.Content;
        void Pump()
        {
            root.Measure(new Size(980,620)); root.Arrange(new Rect(0,0,980,620)); root.UpdateLayout();
            root.Dispatcher.Invoke(() => {},DispatcherPriority.ContextIdle);
        }
        try
        {
            var panes = new List<object>();
            for(var i=0;i<2;i++)
            {
                var profile=Enum.ToObject(profileType,i); // CMD, then PowerShell.
                var pane=Call("CreatePane",profile)!;
                var tab=Activator.CreateInstance(tabType,pane)!; tabs.Add(tab); panes.Add(pane);
                Call("ShowTab",tab); Pump();
                var session=new ConPtySession(80,20); sessions.Add(session);
                Set(pane,"Session",session); Set(pane,"Buffer",new TerminalBuffer(80,20));
                Set(pane,"CancellationTokenSource",new CancellationTokenSource());
                // Launch directly, including from this test host with redirected
                // stdio. The ConPTY connection must supply its own standard handles.
                session.Start((string)Call("GetProfileCommand",profile,false,null,null)!,Environment.CurrentDirectory);
                readers.Add(Task.Run(()=>Call("ReadOutputLoop",pane)));
            }
            string Text(object pane) => string.Concat(((TerminalBuffer)Get(pane,"Buffer")!).CaptureSnapshot().Lines.SelectMany(l=>l.Runs).Select(r=>r.Text));
            void WaitFor(Func<bool> condition)
            {
                var watch=Stopwatch.StartNew();
                while(!condition())
                {
                    Pump(); Thread.Sleep(15);
                    if(watch.Elapsed>TimeSpan.FromSeconds(10))throw new Exception("Live pane output timed out");
                }
                Pump();
            }
            sessions[0].Write("echo ALPHA_BEFORE_JOIN\r"); sessions[1].Write("echo BETA_BEFORE_JOIN\r");
            WaitFor(()=>Text(panes[0]).Contains("ALPHA_BEFORE_JOIN")&&Text(panes[1]).Contains("BETA_BEFORE_JOIN"));
            var recorder = new MacroRecorder(MacroMode.Interactive);
            typeof(MainWindow).GetField("_macroRecorder", flags)!.SetValue(window, recorder);
            typeof(MainWindow).GetField("_macroPane", flags)!.SetValue(window, panes[0]);
            Call("SendUserInput", panes[0], "echo RECORDED_PANE_ONLY\r");
            Call("SendUserInput", panes[1], "echo NOT_RECORDED_OTHER_PANE\r");
            WaitFor(()=>Text(panes[0]).Contains("RECORDED_PANE_ONLY")&&Text(panes[1]).Contains("NOT_RECORDED_OTHER_PANE"));
            if (recorder.Macro.Steps.Count != 1 || recorder.Macro.Steps[0].Input != "echo RECORDED_PANE_ONLY\r")
                throw new Exception("Macro recorded input from another pane.");
            typeof(MainWindow).GetField("_macroRecorder", flags)!.SetValue(window, null);
            using (var cancellation = new CancellationTokenSource())
            {
                typeof(MainWindow).GetField("_macroCancellation", flags)!.SetValue(window, cancellation);
                Call("SendUserInput", panes[0], "echo MUST_NOT_INTERLEAVE\r");
                Thread.Sleep(100); Pump();
                if (Text(panes[0]).Contains("MUST_NOT_INTERLEAVE")) throw new Exception("Manual input interleaved with macro playback.");
                typeof(MainWindow).GetField("_macroCancellation", flags)!.SetValue(window, null);
                typeof(MainWindow).GetField("_macroPane", flags)!.SetValue(window, null);
            }
            if(!(bool)Call("TryJoinTab",tabs[0],false)!)throw new Exception("Live panes could not be joined");
            sessions[0].Write("echo ALPHA_AFTER_JOIN\r"); sessions[1].Write("echo BETA_AFTER_JOIN\r");
            WaitFor(()=>Text(panes[0]).Contains("ALPHA_AFTER_JOIN")&&Text(panes[1]).Contains("BETA_AFTER_JOIN"));
            if(Text(panes[0]).Contains("BETA_AFTER_JOIN")||Text(panes[1]).Contains("ALPHA_AFTER_JOIN") ||
               !ReferenceEquals(Get(panes[0],"Session"),sessions[0]) || !ReferenceEquals(Get(panes[1],"Session"),sessions[1]))
                throw new Exception("Joining crossed shell input/output or replaced a running session");
            var targetView=(TerminalView)Get(panes[0],"View")!;
            if(!ReferenceEquals(Call("FindDropPane",targetView),panes[0]))throw new Exception("Drop hit testing selected the wrong pane");
            var drop=new DataObject(DataFormats.UnicodeText,"echo DROP_TARGET_ONLY\r");
            ((Task)Call("DropIntoPaneAsync",panes[0],drop)!).GetAwaiter().GetResult();
            WaitFor(()=>Text(panes[0]).Contains("DROP_TARGET_ONLY"));
            if(Text(panes[1]).Contains("DROP_TARGET_ONLY"))throw new Exception("A drop was sent to the active pane instead of its target");
            var path=Path.Combine(Environment.CurrentDirectory,"test & file %RTL_DROP_TOKEN%!.png");
            sessions[0].Write("echo ");
            ((Task)Call("DropIntoPaneAsync",panes[0],new DataObject(DataFormats.FileDrop,new[]{path}))!).GetAwaiter().GetResult();
            sessions[0].Write("\r");WaitFor(()=>Text(panes[0]).Contains("%RTL_DROP_TOKEN%!"));
            var image=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[]{0,0,255,255},4);
            sessions[0].Write("echo ");
            ((Task)Call("DropIntoPaneAsync",panes[0],new DataObject(DataFormats.Bitmap,image))!).GetAwaiter().GetResult();
            sessions[0].Write("\r");WaitFor(()=>Text(panes[0]).Contains("clipboard-"));
            var images=(List<string>)typeof(MainWindow).GetField("_temporaryClipboardFiles",flags)!.GetValue(window)!;
            if(images.Count!=1 || !File.Exists(images[0]))throw new Exception("Dropped image was not saved as a temporary PNG");
            for (var i = 0; i < panes.Count; i++)
            {
                Call("ShowTab", tabs[0]);
                Call("ActivatePane", panes[i]);
                Call("ApplyFontSettings", new TerminalFontSettings("Consolas", 14, false, false));
                Pump();
                var pane = panes[i];
                WaitFor(() => ((TerminalBuffer)Get(pane, "Buffer")!).CaptureSnapshot().Columns > 0);
                var prefix = i == 0 ? "RTL_ZOOM_CMD_" : "RTL_ZOOM_PS_";
                var tail = new string('x', 90);
                sessions[i].Write(i == 0
                    ? $"for /L %i in (1,1,12) do @echo {prefix}%i_{tail}\r"
                    : $"1..12 | ForEach-Object {{ Write-Output ('{prefix}'+$_+'_{tail}') }}\r");
                WaitFor(() => Text(pane).Contains(prefix + "12_"));
                var before = ((TerminalBuffer)Get(pane, "Buffer")!).CaptureSnapshot().Columns;
                Call("ZoomFontSize", 3); Call("ZoomFontSize", 3); Pump();
                Call("ApplyViewportResize");
                if (((TerminalBuffer)Get(pane, "Buffer")!).CaptureSnapshot().Columns != before)
                    throw new Exception("Zoom resized the buffer before the debounce interval elapsed");
                WaitFor(() => ((TerminalBuffer)Get(pane, "Buffer")!).CaptureSnapshot().Columns < before &&
                    Text(pane).Contains(prefix + "12_"));
                var retained = Text(pane);
                for (var line = 1; line <= 12; line++)
                    if (!retained.Contains(prefix + line + "_")) throw new Exception($"Zoom lost {prefix}{line} history");
            }
            Console.WriteLine("PASS live CMD/PowerShell zoom debounce and retained output after narrower/wrapped grids");
            Console.WriteLine("PASS two live shells, target-pane text/file/image drops, literal paths and temporary image creation");
        }
        finally
        {
            // Keep readers draining while each native host shuts down.
            foreach(var pane in tabs.Cast<object>().SelectMany(t=>((IList)Get(t,"Panes")!).Cast<object>())) Set(pane,"Session",null);
            Task.WaitAll(sessions.Select(s=>Task.Run(s.Dispose)).ToArray(),TimeSpan.FromSeconds(10));
            window.Close();
            Task.WaitAll(readers.ToArray(),TimeSpan.FromSeconds(10));
            Registry.CurrentUser.DeleteSubKeyTree(historyRegistry, false);
        }
    }
}
