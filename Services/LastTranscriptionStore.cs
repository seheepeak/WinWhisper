namespace WinWhisper.Services;

public sealed class LastTranscriptionStore
{
    private readonly object _lock = new();
    private string? _text;

    public string? Text
    {
        get { lock (_lock) return _text; }
    }

    public void Set(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        lock (_lock) _text = text;
    }
}
