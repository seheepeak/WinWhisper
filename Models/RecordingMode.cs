namespace WinWhisper.Models;

/// <summary>
/// Determines how recording is started and stopped in response to the activation hotkey.
/// Serialized to JSON using the enum member name (e.g., <c>"PressToToggle"</c>).
/// </summary>
public enum RecordingMode
{
    /// <summary>Stop recording when the activation key is pressed again.</summary>
    PressToToggle,

    /// <summary>Stop recording when the activation key is released.</summary>
    HoldToRecord,

    /// <summary>Stop recording automatically after a pause in speech.</summary>
    VoiceActivityDetection,

    /// <summary>Auto-restart recording after each pause until the activation key is pressed again.</summary>
    Continuous,
}
