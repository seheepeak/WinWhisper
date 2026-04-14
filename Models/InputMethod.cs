namespace WinWhisper.Models;

/// <summary>
/// Determines how transcribed text is delivered to the focused application.
/// Serialized to JSON using the enum member name (e.g., <c>"Keyboard"</c>).
/// </summary>
public enum InputMethod
{
    /// <summary>Simulate keyboard input character-by-character.</summary>
    Keyboard,

    /// <summary>Copy to clipboard and send Ctrl+V to paste.</summary>
    Paste,
}
