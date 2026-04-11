using System.ComponentModel;
using System.Windows;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Windows.Win32.UI.Input.KeyboardAndMouse;

using WinWhisper.Common.Threading;
using WinWhisper.Infrastructure.Keyboard;
using WinWhisper.Models;

using static Windows.Win32.PInvoke;

namespace WinWhisper.Services;

public sealed class HotKeyManager : IHostedService, IDisposable, IAsyncDisposable
{
    public event EventHandler<CancelEventArgs>? OnActivate;
    public event EventHandler<CancelEventArgs>? OnDeactivate;
    public event EventHandler<CancelEventArgs>? OnCancel;
    public event EventHandler<bool>? OnHookStatusChanged;

    private readonly KeyboardHook _listener;

    private readonly Dictionary<KeyCode, string> _pressedKeyStates = [];
    private readonly HashSet<VIRTUAL_KEY> _vkRelevant;
    private readonly List<HashSet<VIRTUAL_KEY>> _keyChord;
    private volatile bool _isChordPressed;

    private bool _disposed;


    public HotKeyManager(UserSettings config, ILoggerFactory loggerFactory)
    {
        _listener = new KeyboardHook(loggerFactory.CreateLogger<KeyboardHook>());
        _keyChord = ParseKeyCombination(config.Recording.ActivationKey);
        _vkRelevant = [.. _keyChord.SelectMany(s => s)];

        _listener.OnPress += OnPress;
        _listener.OnRelease += OnRelease;
        _listener.OnStatusChanged += (isHealthy) => OnHookStatusChanged?.Invoke(this, isHealthy);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        _isChordPressed = false;
        _pressedKeyStates.Clear();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;

        DispatcherHelper.AssertMainThread();
        _disposed = true;

        _listener.Dispose();
        return ValueTask.CompletedTask;
    }


    private void OnPress(KeyCode key, bool injected)
    {
        if (key.Vk == VIRTUAL_KEY.VK_ESCAPE)
        {
            if (!TriggerEvent(OnCancel))
            {
                _pressedKeyStates[key] = "suppressed";
                _listener.SuppressEvent();
            }
        }

        if (!_vkRelevant.Contains(key.Vk))
        {
            var toRemove = _pressedKeyStates.Where(kv => kv.Value == "normal").Select(kv => kv.Key).ToList();
            foreach (var k in toRemove) _pressedKeyStates.Remove(k);
            return;
        }

        if (_isChordPressed) return;

        _pressedKeyStates[key] = "normal";
        List<KeyCode> pressed = [.. _pressedKeyStates.Where(p => p.Value == "normal").Select(p => p.Key)];

        if (_keyChord.All(orSet => orSet.Any(vk => pressed.Exists(k => k.Vk == vk))))
        {
            // Reject the chord if any foreign key is physically held.
            // Without this, holding Shift and pressing Win+` would still fire the Win+` chord,
            // even though the user is semantically typing Win+~.
            if (HasForeignKeyHeld()) return;

            _isChordPressed = true;
            foreach (var k in pressed)
                _pressedKeyStates[k] = k != key ? "chorded" : "suppressed";

            PreventWinKeyMenu();

            foreach (var k in pressed)
            {
                if (k != key)
                    KeyboardSimulator.Release(k);
            }

            TriggerEvent(OnActivate);
            _listener.SuppressEvent();
        }
    }

    private void OnRelease(KeyCode key, bool injected)
    {
        if (_pressedKeyStates.TryGetValue(key, out var state))
        {
            if (state == "normal")
            {
                _pressedKeyStates.Remove(key);
            }
            else if (state == "chorded")
            {
                _pressedKeyStates[key] = "suppressed";
            }
            else if (state == "suppressed")
            {
                _pressedKeyStates.Remove(key);
                if (_isChordPressed)
                {
                    _isChordPressed = false;
                    TriggerEvent(OnDeactivate);
                }
                _listener.SuppressEvent();
            }
        }
    }

    private bool HasForeignKeyHeld()
    {
        // Skip mouse buttons (0x01~0x06) — clicking while hitting a hotkey is normal.
        // Skip VK_PACKET (0xE7) — it's a synthetic VK for Unicode injection, not a real key.
        for (int vk = 0x07; vk <= 0xFE; vk++)
        {
            if (vk == (int)VIRTUAL_KEY.VK_PACKET) continue;
            if ((GetAsyncKeyState(vk) & 0x8000) == 0) continue;
            if (_vkRelevant.Contains((VIRTUAL_KEY)vk)) continue;
            return true;
        }
        return false;
    }

    private void PreventWinKeyMenu()
    {
        var winKeys = new HashSet<VIRTUAL_KEY> { VIRTUAL_KEY.VK_LWIN, VIRTUAL_KEY.VK_RWIN };
        if (!_vkRelevant.Overlaps(winKeys)) return;

        var modifiers = new[]
        {
            VIRTUAL_KEY.VK_MENU, VIRTUAL_KEY.VK_LMENU, VIRTUAL_KEY.VK_RMENU,
            VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_LCONTROL, VIRTUAL_KEY.VK_RCONTROL,
            VIRTUAL_KEY.VK_SHIFT, VIRTUAL_KEY.VK_LSHIFT, VIRTUAL_KEY.VK_RSHIFT,
        };
        if (_vkRelevant.Overlaps(modifiers)) return;

        KeyboardSimulator.Press(new KeyCode(VIRTUAL_KEY.VK_LCONTROL));
        KeyboardSimulator.Release(new KeyCode(VIRTUAL_KEY.VK_LCONTROL));
    }

    private bool TriggerEvent(EventHandler<CancelEventArgs>? eventHandler)
    {
        if (eventHandler == null) return true;
        var args = new CancelEventArgs();
        foreach (var handler in eventHandler.GetInvocationList().Cast<EventHandler<CancelEventArgs>>())
        {
            try { handler(this, args); } catch { }
        }
        return !args.Cancel;
    }

    public static List<HashSet<VIRTUAL_KEY>> ParseKeyCombination(string combinationString)
    {
        var chord = new List<HashSet<VIRTUAL_KEY>>();
        var keyMap = new Dictionary<string, (VIRTUAL_KEY, VIRTUAL_KEY)>(StringComparer.OrdinalIgnoreCase)
        {
            { "ctrl", (VIRTUAL_KEY.VK_LCONTROL, VIRTUAL_KEY.VK_RCONTROL) },
            { "shift", (VIRTUAL_KEY.VK_LSHIFT, VIRTUAL_KEY.VK_RSHIFT) },
            { "alt", (VIRTUAL_KEY.VK_LMENU, VIRTUAL_KEY.VK_RMENU) },
            { "win", (VIRTUAL_KEY.VK_LWIN, VIRTUAL_KEY.VK_RWIN) },
        };

        foreach (var s in combinationString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (s.Length == 1)
            {
                var ch = char.ToLowerInvariant(s[0]);
                var result = VkKeyScan(ch);
                if (result != -1)
                {
                    var vk = (VIRTUAL_KEY)(result & 0xFF);
                    chord.Add([vk]);
                    continue;
                }
            }

            if (keyMap.TryGetValue(s.ToLowerInvariant(), out var pair))
            {
                chord.Add([pair.Item1, pair.Item2]);
                continue;
            }

            var vkStr = $"VK_{s.ToUpperInvariant()}";
            if (Enum.TryParse<VIRTUAL_KEY>(vkStr, true, out var vkEnum))
            {
                chord.Add([vkEnum]);
                continue;
            }

            // Unknown key
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show($"Unknown key in activation key combination: '{s}'", "WinWhisper", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            });
            break;
        }

        if (chord.Count == 0)
        {
            chord.Add([VIRTUAL_KEY.VK_LWIN, VIRTUAL_KEY.VK_RWIN]);
            chord.Add([VIRTUAL_KEY.VK_OEM_3]);
        }
        return chord;
    }
}