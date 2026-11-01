using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace RtlTerminal.Proxy;

public static class ProxyDiagnostics
{
    public static string Sanitize(string text)
    {
        text = ProxyService.Redact(text);
        return Regex.Replace(text, @"(?im)((?:password|passwd|token|secret|authorization|api[-_]?key|_authToken)\s*[=:]\s*)[^\r\n]+", "$1[redacted]");
    }
    public static string Describe(Exception exception)
    {
        var text = new StringBuilder(exception.ToString());
        var seen = new HashSet<Exception>();
        void Native(Exception ex)
        {
            if (!seen.Add(ex)) return;
            text.Append("\nException: ").Append(ex.GetType().FullName).Append("; HRESULT=0x").Append(ex.HResult.ToString("X8"));
            if (ex is Win32Exception native)
                text.Append("; NativeErrorCode=").Append(native.NativeErrorCode).Append(" (0x").Append(native.NativeErrorCode.ToString("X8")).Append("): ").Append(new Win32Exception(native.NativeErrorCode).Message);
            if (ex is AggregateException aggregate) foreach (var inner in aggregate.InnerExceptions) Native(inner);
            else if (ex.InnerException != null) Native(ex.InnerException);
        }
        Native(exception); return Sanitize(text.ToString());
    }
}
