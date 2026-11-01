using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RtlTerminal.Proxy;

public enum ProxyRowStatus { Pending, Running, Success, Failed, Skipped }
public sealed class ProxyOperationRow(string provider) : INotifyPropertyChanged
{
    public string Provider { get; } = provider;
    public string DisplayName => Provider switch
    {
        "Environment (User)" => "Environment Proxy",
        "Windows system (User)" => "Windows System Proxy",
        "WinHTTP (effective User)" => "WinHTTP Proxy",
        _ => Provider.EndsWith(" user", StringComparison.Ordinal) ? Provider[..^5] : Provider
    };
    public ProxyRowStatus Status { get; private set; }
    public string Detail { get; private set; } = "Waiting";
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Update(ProxyRowStatus status, string detail)
    {
        if (Status == status && Detail == ProxyService.Redact(detail)) return;
        Status = status; Detail = ProxyService.Redact(detail);
        PropertyChanged?.Invoke(this, new(nameof(Status)));
    }
}

// UI-independent state. Success is assigned only from a verified provider result.
public sealed class ProxyOperationState : INotifyPropertyChanged
{
    public IReadOnlyList<ProxyOperationRow> Rows { get; }
    public bool IsComplete { get; private set; }
    public string? Error { get; private set; }
    public string Phase { get; private set; } = "Preparing operation…";
    public int Completed => Rows.Count(r => r.Status is ProxyRowStatus.Success or ProxyRowStatus.Failed or ProxyRowStatus.Skipped);
    public int Succeeded => Rows.Count(r => r.Status == ProxyRowStatus.Success);
    public int Failed => Rows.Count(r => r.Status == ProxyRowStatus.Failed);
    public int Skipped => Rows.Count(r => r.Status == ProxyRowStatus.Skipped);
    public bool AllSucceeded => IsComplete && Error == null && Rows.Count > 0 && Succeeded == Rows.Count;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ProxyOperationState(IEnumerable<string> providers)
    {
        Rows = providers.Select(p => new ProxyOperationRow(p)).ToArray();
        if (Rows.Count == 0 || Rows.Select(r => r.Provider).Distinct().Count() != Rows.Count) throw new ArgumentException("Operation must contain unique providers.");
    }
    public void Report(ProxyOperationUpdate update)
    {
        if (IsComplete) return;
        if (update.Phase != null) Phase = update.Phase;
        if (update.Provider is { } name && update.Diagnostic == null)
        {
            var row = Rows.Single(r => r.Provider == name);
            if (update.Result is { } result)
                row.Update(result.Success ? ProxyRowStatus.Success : result.Outcome == ProxyOutcome.Skipped ? ProxyRowStatus.Skipped : ProxyRowStatus.Failed, result.Message);
            else row.Update(ProxyRowStatus.Running, "Processing and verifying…");
        }
        Changed();
    }
    public void Complete(IReadOnlyList<ProxyResult> results, string? error = null)
    {
        if (IsComplete) return;
        foreach (var result in results) Report(new(result.Provider, result));
        Error = error == null ? null : ProxyService.Redact(error);
        foreach (var row in Rows.Where(r => r.Status is ProxyRowStatus.Pending or ProxyRowStatus.Running))
            row.Update(row.Status == ProxyRowStatus.Running ? ProxyRowStatus.Failed : ProxyRowStatus.Skipped,
                row.Status == ProxyRowStatus.Running ? "Operation interrupted; configuration state requires inspection in History." : "Not started: " + (Error ?? "the operation ended before this provider ran."));
        IsComplete = true; Changed();
    }
    private void Changed() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
