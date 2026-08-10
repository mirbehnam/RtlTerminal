using System.Windows;



namespace RtlTerminal;



public partial class App : Application
{
    private const int MinimumWindowsBuild = 17763;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!OperatingSystem.IsWindowsVersionAtLeast(
                major: 10,
                minor: 0,
                build: MinimumWindowsBuild))
        {
            var version = Environment.OSVersion.Version;
            MessageBox.Show(
                "RtlTerminal requires Windows 10 version 1809 " +
                "(build 17763) or Windows Server 2019 or later." +
                Environment.NewLine +
                Environment.NewLine +
                $"Detected Windows version: {version}",
                "Unsupported Windows version",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
