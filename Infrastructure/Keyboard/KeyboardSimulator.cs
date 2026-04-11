using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using Windows.Win32.UI.Input.KeyboardAndMouse;

using static Windows.Win32.PInvoke;

namespace WinWhisper.Infrastructure.Keyboard;

public static class KeyboardSimulator
{
    public static void Press(KeyCode key)
    {
        Handle(key, true);
    }

    public static void Release(KeyCode key)
    {
        Handle(key, false);
    }

    public static void TypeUnicode(string text)
    {
        // This code and related structures are based on 
        // http://stackoverflow.com/a/11910555/252218        
        try
        {
            // Encode the string to UTF-16LE
            var surrogates = Encoding.Unicode.GetBytes(text);
            var presses = new List<INPUT>();
            var releases = new List<INPUT>();

            // Process 2 bytes at a time (UTF-16 code units)
            for (int i = 0; i < surrogates.Length; i += 2)
            {
                byte lower = surrogates[i];
                byte higher = surrogates[i + 1];
                ushort scanCode = (ushort)((higher << 8) | lower);

                // Create key press event
                var pressInput = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
                pressInput.Anonymous.ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanCode,
                    dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE
                };
                presses.Add(pressInput);

                // Create key release event
                var releaseInput = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
                releaseInput.Anonymous.ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanCode,
                    dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP
                };
                releases.Add(releaseInput);
            }

            // Combine all presses followed by all releases
            var allInputs = new List<INPUT>();
            allInputs.AddRange(presses);
            allInputs.AddRange(releases);

            // Send all inputs at once
            SendInput(allInputs.ToArray(), Marshal.SizeOf<INPUT>());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to type unicode text: {text}, Error: {ex.Message}");
        }
    }

    private static void Handle(KeyCode key, bool isPress)
    {
        try
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_TYPE.INPUT_KEYBOARD;
            inputs[0].Anonymous.ki = key.GetParameters(isPress);
            SendInput(inputs, Marshal.SizeOf<INPUT>());
        }
        catch (Exception)
        {
            var action = isPress ? "press" : "release";
            Debug.WriteLine($"Failed to {action} key: {key}");
        }
    }
}