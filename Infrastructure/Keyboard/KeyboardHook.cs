using System.Runtime.InteropServices;
using System.Windows;

using Microsoft.Extensions.Logging;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

using static Windows.Win32.PInvoke;

namespace WinWhisper.Infrastructure.Keyboard;

public sealed class KeyboardHook : IDisposable
{
    // Constants and enums
    private enum State
    {
        Created,
        Running,
        Stopped,
    }

    private const uint WM_REINSTALL = WM_APP + 10;

    private readonly ILogger<KeyboardHook> _logger;

    // State management
    private State _state = State.Created;
    private bool _disposed;

    // Hook related
    private UnhookWindowsHookExSafeHandle _hookId = new();
    private Thread? _messageThread;
    private volatile uint _messageThreadId;
    private readonly ManualResetEventSlim _messageThreadStarted = new(initialState: false);
    private readonly HOOKPROC _hookProc; // Keep the delegate alive to avoid GC
    private volatile bool _suppress; // suppress "current" event if set inside handler

    // Alive check & synchronization
    private readonly VIRTUAL_KEY _alivePingKey;
    private readonly AutoResetEvent _alivePongEvent = new(false);
    private readonly CancellationTokenSource _aliveMonitorCts = new();
    private Task? _aliveTask;

    // Events
    public event Action<KeyCode, bool>? OnPress;
    public event Action<KeyCode, bool>? OnRelease;
    public event Action<bool>? OnStatusChanged;

    // Constructor
    public KeyboardHook(ILogger<KeyboardHook> logger)
    {
        _logger = logger;

        // capture delegate once
        _hookProc = HookCallback;

        _alivePingKey = VIRTUAL_KEY.VK_OEM_CLEAR;
    }

    // Public methods
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_state == State.Stopped)
        {
            throw new InvalidOperationException("KeyboardHook is one-shot and cannot be restarted.");
        }

        if (_state == State.Created)
        {
            _messageThread = new Thread(MessageLoop) { IsBackground = true, Name = "KeyboardHookThread" };
            _messageThread.Start();

            // Wait for the worker to create its message queue & report thread id
            if (!_messageThreadStarted.Wait(10_000))
                throw new TimeoutException("KeyboardHook thread failed to start in time.");

            _logger.LogInformation("KeyboardHookThread started: tid={ThreadId}", _messageThreadId);

            _aliveTask = Task.Run(AliveMonitorAsync, _aliveMonitorCts.Token);

            _state = State.Running;
        }
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state == State.Running)
        {
            if (_messageThreadId != 0)
            {
                PostThreadMessage(_messageThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
            }

            _messageThread?.Join(10000);
            _state = State.Stopped;
        }
    }

    public void PostHookReinstall()
    {
        if (_state == State.Running && _messageThreadId != 0)
        {
            PostThreadMessage(_messageThreadId, WM_REINSTALL, UIntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <summary>
    /// Suppresses the CURRENT event if called inside OnPress/OnRelease before the hook returns.
    /// </summary>
    public void SuppressEvent() => _suppress = true;

    public void Dispose()
    {
        if (_disposed)
            return;

        _aliveMonitorCts.Cancel();
        _alivePongEvent.Set();
        try { _aliveTask?.Wait(10_000); } catch { }
        try { Stop(); } catch { }

        _aliveMonitorCts.Dispose();
        _alivePongEvent.Dispose();
        _hookId.Dispose();
        _messageThreadStarted.Dispose();
        _disposed = true;
    }

    // Private methods
    private void MessageLoop()
    {
        _messageThreadId = GetCurrentThreadId();
        _ = PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);

        _hookId = SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _hookProc, null, 0);
        if (_hookId.IsInvalid)
        {
            _messageThreadStarted.Set();

            // Show error and shutdown on UI thread
            var err = Marshal.GetLastPInvokeError();
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    $"Failed to install the keyboard hook.\n\n"
                        + $"Possible causes:\n"
                        + $"- Administrator privileges may be required\n"
                        + $"- Security software may be blocking it\n"
                        + $"- Conflict with another program\n\n"
                        + $"Error Code: {err}",
                    "WinWhisper - Initialization failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Application.Current.Shutdown();
            });

            return;
        }

        // Signal that message loop thread starts
        _messageThreadStarted.Set();
        _logger.LogInformation("Keyboard hook installed.");

        // Standard message pump
        for (; ; )
        {
            var ret = GetMessage(out MSG msg, HWND.Null, 0, 0);
            if (ret.Value == 0) // WM_QUIT
                break;

            if (ret.Value == -1) // error
            {
                var err = Marshal.GetLastPInvokeError();
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"An error occurred while processing the message loop.\n"
                            + $"Please restart the program.\n\n"
                            + $"Error Code: {err}\n"
                            + $"If the problem persists, try rebooting the system.",
                        "WinWhisper - Runtime error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    Application.Current.Shutdown();
                });
                break;
            }

            // Process thread message
            if (msg.message == WM_REINSTALL)
            {
                _hookId.Dispose();
                _hookId = SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _hookProc, null, 0);
                if (_hookId.IsInvalid)
                {
                    var err = Marshal.GetLastPInvokeError();
                    _logger.LogError("Failed to re-install keyboard hook. GetLastError={ErrorCode}", err);
                }
                else
                {
                    _logger.LogInformation("Keyboard hook re-installed.");
                }
            }
            else
            {
                TranslateMessage(msg);
                DispatchMessage(msg);
            }
        }
    }

    private LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        _suppress = false;

        if (nCode == HC_ACTION)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (hookStruct.vkCode != (uint)VIRTUAL_KEY.VK_PACKET)
            {
                var message = (uint)wParam.Value;
                bool isExtended = (hookStruct.flags & KBDLLHOOKSTRUCT_FLAGS.LLKHF_EXTENDED) != 0;
                bool injected =
                    (hookStruct.flags & (KBDLLHOOKSTRUCT_FLAGS.LLKHF_INJECTED | KBDLLHOOKSTRUCT_FLAGS.LLKHF_LOWER_IL_INJECTED)) != 0;
                var key = new KeyCode((VIRTUAL_KEY)hookStruct.vkCode, isExtended);
                if (
                    (message == WM_KEYDOWN || message == WM_SYSKEYDOWN || message == WM_KEYUP || message == WM_SYSKEYUP)
                    && key.Vk == _alivePingKey
                    && injected
                )
                {
                    _alivePongEvent.Set();
                    SuppressEvent();
                }
                else if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    OnPress?.Invoke(key, injected);
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    OnRelease?.Invoke(key, injected);
                }
            }
        }

        return _suppress ? (LRESULT)1 : CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private async Task AliveMonitorAsync()
    {
        var pongFailCount = 0;

        try
        {
            await Task.Delay(10_000, _aliveMonitorCts.Token);

            while (true)
            {
                KeyboardSimulator.Press(new KeyCode(_alivePingKey));
                await Task.Delay(100, _aliveMonitorCts.Token);
                KeyboardSimulator.Release(new KeyCode(_alivePingKey));

                await Task.Delay(10_000, _aliveMonitorCts.Token);
                if (_alivePongEvent.WaitOne(TimeSpan.FromSeconds(1)))
                {
                    if (pongFailCount > 0)
                    {
                        OnStatusChanged?.Invoke(true);
                    }
                    pongFailCount = 0;
                    continue;
                }

                if (++pongFailCount > 0)
                {
                    OnStatusChanged?.Invoke(false);
                }

                // Retry re-installing independently of fail count (once we miss a pong, we try to recover)
                PostHookReinstall();
                await Task.Delay(10_000, _aliveMonitorCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }
}
