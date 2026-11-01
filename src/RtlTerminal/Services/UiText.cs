using Microsoft.Win32;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace RtlTerminal;

public enum UiLanguage { English, Persian, Arabic }

// Locale changes only string values; no culture, alignment or FlowDirection is set.
public sealed partial class UiText : MarkupExtension
{
    private const string SettingsPath = @"Software\RtlTerminal";
    private static readonly Dictionary<string, LocalizedString> Bindings = new(StringComparer.Ordinal);
    public static UiLanguage Current { get; private set; } = Load();
    public static event Action? Changed;
    public string Key { get; set; } = "";
    public UiText() { }
    public UiText(string key) => Key = key;
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (!Bindings.TryGetValue(Key, out var source)) Bindings[Key] = source = new(Key);
        return new Binding(nameof(LocalizedString.Value)) { Source = source, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
    }
    private sealed class LocalizedString(string key) : INotifyPropertyChanged
    {
        public string Value => Get(key);
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Refresh() => PropertyChanged?.Invoke(this, new(nameof(Value)));
    }
    public static UiLanguage Load()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsPath);
        return Enum.TryParse<UiLanguage>(key?.GetValue("UiLanguage") as string, out var language) && Enum.IsDefined(language) ? language : UiLanguage.English;
    }
    public static void Select(UiLanguage language, bool persist = true)
    {
        if (!Enum.IsDefined(language)) throw new ArgumentOutOfRangeException(nameof(language));
        if (persist)
        {
            using var key = Registry.CurrentUser.CreateSubKey(SettingsPath);
            key.SetValue("UiLanguage", language.ToString());
        }
        Current = language;
        foreach (var value in Bindings.Values.ToArray()) value.Refresh();
        Changed?.Invoke();
    }
    public static string Get(string source)
    {
        if (Current == UiLanguage.English) return source;
        if (Catalog.TryGetValue(source, out var pair)) return Current == UiLanguage.Persian ? pair.Fa : pair.Ar;
        foreach (var item in Templates)
        {
            var match = item.Pattern.Match(source);
            if (match.Success)
                return string.Format(CultureInfo.InvariantCulture, Current == UiLanguage.Persian ? item.Fa : item.Ar,
                    match.Groups.Cast<Group>().Skip(1).Select(g => (object)g.Value).ToArray());
        }
        return source; // Product names, paths, errors from the OS and user content stay literal.
    }
    public static string Format(string key, params object?[] args) => string.Format(CultureInfo.InvariantCulture, Get(key), args);
    public static string Paragraphs(string source) => string.Join("\n\n", source.Split("\n\n").Select(Get));
    private static readonly List<(Regex Pattern, string Fa, string Ar)> Templates = [];
    private static readonly Dictionary<string, (string Fa, string Ar)> Catalog = BuildCatalog();
    private static Dictionary<string, (string Fa, string Ar)> BuildCatalog()
    {
        var result = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        var entries = TranslationData.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => (Line: line, Proxy: false))
            .Concat(ProxyTranslationData.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => (Line: line, Proxy: true)));
        foreach (var entry in entries)
        {
            var line = entry.Line;
            var fields = line.TrimEnd('\r').Split('|');
            if (fields.Length != 3) throw new InvalidOperationException("Invalid UI translation row: " + fields[0]);
            var key = fields[0].Replace("\\n", "\n");
            if (entry.Proxy && result.ContainsKey(key)) continue;
            result.Add(key, (fields[1].Replace("\\n", "\n"), fields[2].Replace("\\n", "\n")));
            if (fields[0].Contains("{0}"))
            {
                var pattern = Regex.Escape(fields[0]);
                for (var i = 0; i < 8; i++) pattern = pattern.Replace(Regex.Escape("{" + i + "}"), "(.*?)");
                Templates.Add((new Regex("\\A" + pattern + "\\z", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)), fields[1], fields[2]));
            }
        }
        return result;
    }
    public static MessageBoxResult Show(Window owner, string text, string caption = "Rtl Terminal", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        => MessageBox.Show(owner, Get(text), Get(caption), button, image);
    public static MessageBoxResult Show(string text, string caption = "Rtl Terminal", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        => MessageBox.Show(Get(text), Get(caption), button, image);
}
