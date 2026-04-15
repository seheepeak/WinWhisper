# WinWhisper

Hotkey-triggered speech-to-text for Windows. Tap the hotkey to start dictating, tap again to stop — the transcription is auto-pasted into the focused window.

<img src="demo.gif" alt="demo" width="480">

Inspired by [whisper-writer](https://github.com/savbell/whisper-writer), but built native for Windows for two reasons:

- A UI with a bit more personality.
- Windows' built-in dictation handles Korean poorly — especially when Korean speech is mixed with English loanwords (which is most real-world Korean).
- Most existing tools can't use the Windows key as part of the activation hotkey. WinWhisper can.

Powered by [OpenAI Whisper](https://github.com/openai/whisper) via [Whisper.net](https://github.com/sandrohanea/whisper.net) (local) or the OpenAI API (cloud).

## Install

Requires Windows 10 build 17763 (1809) or later, x64, and the .NET 9 SDK.

```powershell
git clone https://github.com/seheepeak/WinWhisper.git
cd WinWhisper
powershell -ExecutionPolicy Bypass -File install.ps1
```

`install.ps1` publishes a Release build directly into `%LocalAppData%\Programs\WinWhisper` and drops a shortcut in the Startup folder so it auto-starts on next login. Re-run any time to update.

## Use

1. Press the hotkey (default `Win+~`) to start recording, then speak.
2. Press it again to stop. The transcription is auto-pasted into the focused window.
3. `Esc` while recording to cancel.
4. Right-click the tray icon for Settings, "Copy last transcription", or Exit.

Other recording modes (hold-to-record, voice-activity, continuous) are available in `settings.json` under `recording.recordingMode`.

First launch opens Settings — pick a local Whisper model to download or paste an OpenAI API key.

To change the hotkey, edit `recording.activationKey` in `%LocalAppData%\WinWhisper\settings.json` and restart. Examples: `"win+~"`, `"ctrl+shift+v"`, `"alt+space"`.

## Architecture

See [CLAUDE.md](CLAUDE.md).

## License

MIT.
