# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WinWhisper is a Windows-only WPF system tray app that does real-time speech-to-text via Whisper. The user holds a global hotkey (configurable, default `Win+\``), speaks, and the transcribed text is auto-pasted into the foreground window.

**Tech stack:** .NET 9.0 (WPF + WinForms hybrid), Whisper.net (local) + OpenAI API (cloud), NAudio, WebRtcVadSharp, Microsoft.Windows.CsWin32, Serilog, Microsoft.Extensions.Hosting.

The TFM is `net9.0-windows10.0.17763.0` because `Microsoft.Toolkit.Uwp.Notifications` (toast) requires the WinRT projections from Windows 10 1809+.

## Build & Install

```bash
dotnet restore
dotnet build -c Release
dotnet publish -c Release          # output: ./publish/

# Personal install: publish directly into %LocalAppData%\Programs\WinWhisper
# + drop a .lnk in the user's Startup folder so it auto-starts on login.
powershell -ExecutionPolicy Bypass -File install.ps1
```

Release builds are self-contained `win-x64`. There's no test project.

## Architecture

### Hosting and DI

The app uses `Microsoft.Extensions.Hosting`. All registrations live in `App.OnStartup` (App.xaml.cs). Service lifetimes are not arbitrary — pay attention:

- **Singletons**: `SettingsManager` (also `IOptionsMonitor<UserSettings>`), `LocalWhisperProvider`, `OpenAIWhisperProvider`, `TranscriptionProviderFactory`, `HotKeyManager`, `SoundEffectService`, `IModelService`, `LastTranscriptionStore`.
- **Hosted services** (singleton + lifecycle): `LocalWhisperProvider`, `HotKeyManager`, `SoundEffectService`. These need `StartAsync`/`StopAsync` for setup/teardown of native resources or background loops.
- **Transient**: `TranscriptionService` (one per recording session), `StatusWindow` (one per activation), `SettingsWindow`, `SettingsViewModel`.

`HotKeyManager` is registered both as a singleton AND as a hosted service so other code can pull it directly while still getting lifecycle hooks.

### Transcription pipeline

`TranscriptionService.RunAsync` (Services/Transcription/TranscriptionService.cs) orchestrates one session:

1. Pick recording device from settings (or default).
2. Detect input language from the foreground window's keyboard layout (`GetForegroundWindow` → `GetKeyboardLayout(threadId)` → `LanguageMappings`). Re-detected periodically during recording.
3. Record 16 kHz mono PCM with `NAudio.WaveInEvent` into a `MemoryStream`.
4. Run WebRTC VAD on 30 ms frames at `VeryAggressive` mode. Recording-stop trigger depends on `RecordingMode`:
   - `manual` — stops when hotkey released.
   - `voice_activity_detection` — stops after N consecutive silent frames.
   - `continuous` — loops the whole session until hotkey re-pressed.
5. Send WAV to the provider returned by `TranscriptionProviderFactory.GetProvider()`.
6. Cache result in `LastTranscriptionStore`, then fire `TranscriptionEvent(TranscribingCompleted, text)`.
7. `StatusWindow.OnTranscribingCompleted` performs the actual auto-paste — either character-by-character `KeyboardSimulator.TypeUnicode` or `Clipboard.SetText` + simulated `Ctrl+V`, depending on `InputMethod` setting.

### Provider selection

`TranscriptionProviderFactory.GetProvider()` returns `OpenAIWhisperProvider` iff `Model.Api.Enabled && !string.IsNullOrEmpty(ApiKey)`, otherwise `LocalWhisperProvider`. Both implement `ITranscriptionProvider.TranscribeAsync(Stream, langCode, ct)`.

### Hotkey chord engine

`HotKeyManager` (Services/HotKeyManager.cs) consumes raw key events from `KeyboardHook` and runs a small state machine. **Read this code carefully before changing it** — there are several non-obvious invariants:

- **Thread invariant**: `KeyboardHook` installs a low-level hook on its own dedicated `KeyboardHookThread` with a message pump. WH_KEYBOARD_LL callbacks fire on that thread, which means **all `OnPress`/`OnRelease` handlers run single-threaded on the hook thread**. `_pressedKeyStates` is therefore not protected by a lock — don't add cross-thread access.
- **State machine** per key: `normal` → `chorded` → `suppressed`. When the chord completes, all already-pressed modifier keys are synthetically `Release`d via `KeyboardSimulator` so they don't get stuck in other apps after the chord.
- **`PreventWinKeyMenu`**: when the chord includes only `Win` (no other modifier), an `LCtrl` press/release is injected to defeat the Start menu popup that fires on Win-key release.
- **Foreign key rejection**: `HasForeignKeyHeld()` scans VK 0x07–0xFE via `GetAsyncKeyState` at chord-completion time. Skips mouse buttons and `VK_PACKET`. If any key outside the chord is physically held, the chord is rejected. This prevents `Win+\`` from firing when, say, `Shift` is also held (which is semantically `Win+~`, a different intent).
- **Chord parsing**: `ParseKeyCombination("win+\`")` → `List<HashSet<VIRTUAL_KEY>>`. Outer list is AND-of-slots, each slot is OR-of-keys (e.g. `{LWin, RWin}`). Single-character segments use `VkKeyScan` to map to VK; named modifiers use a small lookup table.
- **LL hook timeout**: Windows unhooks low-level hooks if a callback takes longer than `LowLevelHooksTimeout` (~300 ms). `KeyboardHook.AliveMonitorAsync` injects a `VK_OEM_CLEAR` ping every ~10 s and re-installs the hook via `WM_REINSTALL` if the pong doesn't come back. Subscribers must keep handlers cheap and marshal heavy work to a thread pool.

### LastTranscriptionStore

Lock-guarded singleton holding the most recent successful transcription. Producer: `TranscriptionService.RunAsync` (just before firing `TranscribingCompleted`). Consumer: tray menu's "Copy last transcription" item in `App.xaml.cs`. Empty strings are rejected so the cache only ever holds meaningful text.

### Settings (MVVM + persistence gate)

`SettingsViewModel` clones the persisted config from `SettingsManager`, edits a draft, and only writes back on Apply. `TryClose()` is the gate shared by Cancel button and `Window.Closing` (X / Alt+F4): it validates the **persisted** config (not the draft), so closing without Apply never leaves the app in a broken state. If the persisted config is invalid (e.g. first-run with no backend), the user is prompted to exit the application.

`ModelSelectViewModel` is embedded inside the Settings UI. `ModelItem.GgmlType` is the `Whisper.net.Ggml.GgmlType` enum (not a string) — don't reintroduce string-keyed model identifiers, the previous codebase had `Enum.TryParse` calls scattered across three sites and we deliberately removed them.

### Native library resolver

`App.OnStartup` registers `NativeLibrary.SetDllImportResolver` for the WebRtcVad assembly. Required because `webrtcvad.dll` ships under `runtimes/win-x64/native/` and the default resolver can't find it from the EXE directory at runtime.

## Key Implementation Details

- **Win32 via CsWin32**: Native function and type names are listed in `NativeMethods.txt`. CsWin32 generates the corresponding signatures at compile time. To use a new Win32 function, add its name to that file and rebuild.
- **VAD configuration**: 30 ms frames, 16 kHz, `VeryAggressive` mode. The first ~150 ms of frames are skipped to avoid the keypress click.
- **Logging**: Serilog. Debug builds log to console only. Release builds also write rolling daily files to `%LocalAppData%\WinWhisper\logs\`.
- **Settings file**: `%LocalAppData%\WinWhisper\settings.json`. `SettingsManager` implements `IOptionsMonitor<UserSettings>` for reactive updates.
- **First run**: `LocalWhisperProvider` downloads the chosen GGML model (~1.5 GB for large-v3) on first use through `IModelService`. Tracked in the Settings → Model UI with progress + cancel.
- **Toast notifications**: `App.ShowTrayNotification(title, message)` wraps `ToastContentBuilder`. WinRT toast APIs are thread-safe — no `Dispatcher` marshaling needed.

## Conventions

- **All comments in English.** Same for user-facing strings (MessageBox / toast / window text).
- **MessageBox vs Toast**: keep MessageBox when (a) the app is about to shut down, (b) the user is inside a modal dialog reacting to a button they just pressed, or (c) a YesNo prompt is needed. Use Toast for long-running async failures (e.g. model download) where the user has likely switched away.
- **Don't add defensive checks for cases the type system already prevents.** Past examples removed: `Enum.TryParse<GgmlType>` on a programmatically-set enum, validation of pack-resource-loaded icons.
