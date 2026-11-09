namespace RtlTerminal;

public enum DirectoryOpenMode { Smart, AlwaysNewTab, CurrentTab }
internal enum DirectoryOpenAction { Replace, NewTab, Confirm, Cancel }
internal static class DirectoryOpenPolicy
{
    internal static DirectoryOpenAction Decide(DirectoryOpenMode mode, bool hasInput) => mode switch
    {
        DirectoryOpenMode.AlwaysNewTab => DirectoryOpenAction.NewTab,
        DirectoryOpenMode.CurrentTab when hasInput => DirectoryOpenAction.Confirm,
        DirectoryOpenMode.CurrentTab => DirectoryOpenAction.Replace,
        _ => hasInput ? DirectoryOpenAction.NewTab : DirectoryOpenAction.Replace
    };
}
