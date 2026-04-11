using Windows.Win32.UI.Input.KeyboardAndMouse;

using static Windows.Win32.PInvoke;

namespace WinWhisper.Infrastructure.Keyboard;

public record KeyCode
{
    public VIRTUAL_KEY Vk { get; init; }
    public bool IsExtended { get; init; }

    public KeyCode(VIRTUAL_KEY vk, bool isExtended)
    {
        Vk = vk;
        IsExtended = isExtended;
    }


    public KeyCode(VIRTUAL_KEY vk)
    {
        Vk = vk;
        IsExtended = ExtendedKeys.Contains(vk);
    }


    public KEYBDINPUT GetParameters(bool isPress)
    {
        VIRTUAL_KEY vk = Vk;
        ushort scan = (ushort)MapVirtualKeyEx((uint)vk, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC, HKL.Null);
        KEYBD_EVENT_FLAGS flags = IsExtended ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0;
        if (!isPress)
            flags |= KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        return new KEYBDINPUT
        {
            dwFlags = flags,
            wVk = vk,
            wScan = scan,
        };
    }

    public static readonly HashSet<VIRTUAL_KEY> ExtendedKeys = CollectExtendedKeys();

    private static HashSet<VIRTUAL_KEY> CollectExtendedKeys()
    {
        HashSet<VIRTUAL_KEY> extendedKeys = [];
        foreach (var vk in Enum.GetValues<VIRTUAL_KEY>())
        {
            uint scanCode = MapVirtualKeyEx((uint)vk, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX, HKL.Null);
            byte highByte = (byte)(scanCode >> 8);
            if (highByte == 0xE0 || highByte == 0xE1)
            {
                extendedKeys.Add(vk);
            }
        }
        return extendedKeys;
    }

}
