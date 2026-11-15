using System.Runtime.InteropServices;
using SyncForge.Models;

namespace SyncForge.Services;

/// <summary>
/// Symbol im Infobereich (Win32 Shell_NotifyIcon) mit Kontextmenü. Das unsichtbare Hilfsfenster
/// empfängt außerdem WM_DEVICECHANGE, um neu angeschlossene Laufwerke zu melden.
/// Muss auf dem UI-Thread erzeugt werden (dessen Nachrichtenschleife bedient das Fenster).
/// </summary>
public sealed partial class TrayIcon : IDisposable
{
    public enum MenuCommand { Open = 1, SyncAll = 2, Exit = 3 }

    private const uint WM_APP_TRAY = 0x8001;
    private const uint WM_DEVICECHANGE = 0x0219;
    private const uint WM_CONTEXTMENU = 0x007B;
    private const uint WM_NULL = 0x0000;
    private const uint NIN_SELECT = 0x0400;
    private const uint NIN_KEYSELECT = 0x0401;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVTYP_VOLUME = 0x0002;
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_SHOWTIP = 0x80;
    private const uint NOTIFYICON_VERSION_4 = 4;
    private const uint MF_STRING = 0x0, MF_SEPARATOR = 0x800, MF_DEFAULT = 0x1000;
    private const uint TPM_RIGHTBUTTON = 0x2, TPM_RETURNCMD = 0x100, TPM_BOTTOMALIGN = 0x20;

    private readonly WndProcDelegate _wndProc; // Referenz halten, sonst sammelt der GC den Delegaten ein
    private readonly nint _hwnd;
    private readonly nint _icon;
    private readonly uint _taskbarCreated;
    private string _toolTip = "SyncForge";
    private bool _added;

    public event EventHandler<MenuCommand>? Command;
    public event EventHandler<char>? DriveArrived;

    /// <summary>Menütexte (lokalisiert) – vom Aufrufer gesetzt.</summary>
    public Func<MenuCommand, string> MenuText { get; set; } = c => c.ToString();

    public TrayIcon(string iconPath)
    {
        _wndProc = WndProc;
        var hInstance = GetModuleHandle(null);
        var className = "SyncForgeTrayWindow";
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = className
        };
        RegisterClassEx(ref wc);
        // Normales (unsichtbares) Top-Level-Fenster: Nur solche erhalten Broadcasts wie WM_DEVICECHANGE.
        _hwnd = CreateWindowEx(0, className, "SyncForge", 0, 0, 0, 0, 0, 0, 0, hInstance, 0);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _icon = LoadImage(0, iconPath, 1 /* IMAGE_ICON */, GetSystemMetrics(49 /* SM_CXSMICON */), GetSystemMetrics(50), 0x10 /* LR_LOADFROMFILE */);
        Add();
    }

    /// <summary>Aktueller Tooltip-Text.</summary>
    public string ToolTip => _toolTip;

    public void SetToolTip(string text)
    {
        if (text == _toolTip) return;
        _toolTip = text.Length > 127 ? text[..127] : text;
        if (!_added) return;
        var data = CreateData(NIF_TIP | NIF_SHOWTIP);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    /// <summary>Kontextmenü im passenden Design (nutzt die von Windows für den Explorer verwendete Einstellung).</summary>
    public static void ApplyMenuTheme(AppTheme theme)
    {
        try
        {
            SetPreferredAppMode(theme switch { AppTheme.Dark => 2, AppTheme.Light => 3, _ => 1 });
            FlushMenuThemes();
        }
        catch
        {
            // Undokumentierte API – ohne sie bleibt das Menü im Standard-Design.
        }
    }

    private void Add()
    {
        var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    private NOTIFYICONDATA CreateData(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _icon,
        szTip = _toolTip,
        szInfo = string.Empty,
        szInfoTitle = string.Empty
    };

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_APP_TRAY)
        {
            var evt = (uint)(lParam.ToInt64() & 0xFFFF);
            if (evt is NIN_SELECT or NIN_KEYSELECT) Command?.Invoke(this, MenuCommand.Open);
            else if (evt == WM_CONTEXTMENU) ShowMenu();
            return 0;
        }
        if (msg == WM_DEVICECHANGE && wParam.ToInt64() == DBT_DEVICEARRIVAL && lParam != 0)
        {
            if (Marshal.ReadInt32(lParam, 4) == DBT_DEVTYP_VOLUME)
            {
                var mask = (uint)Marshal.ReadInt32(lParam, 12);
                for (var i = 0; i < 26; i++)
                    if ((mask & (1u << i)) != 0) DriveArrived?.Invoke(this, (char)('A' + i));
            }
            return 1;
        }
        if (msg == _taskbarCreated && _taskbarCreated != 0)
        {
            Add(); // Explorer wurde neu gestartet
            return 0;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING | MF_DEFAULT, (nuint)MenuCommand.Open, MenuText(MenuCommand.Open));
        AppendMenu(menu, MF_STRING, (nuint)MenuCommand.SyncAll, MenuText(MenuCommand.SyncAll));
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        AppendMenu(menu, MF_STRING, (nuint)MenuCommand.Exit, MenuText(MenuCommand.Exit));

        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd); // sonst schließt das Menü nicht beim Klick daneben
        var cmd = TrackPopupMenuEx(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_BOTTOMALIGN, pt.X, pt.Y, _hwnd, 0);
        PostMessage(_hwnd, WM_NULL, 0, 0);
        DestroyMenu(menu);
        if (cmd > 0) Command?.Invoke(this, (MenuCommand)cmd);
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = CreateData(0);
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != 0) DestroyIcon(_icon);
        if (_hwnd != 0) DestroyWindow(_hwnd);
    }

    // --- Win32 ---

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadImage(nint hInst, string name, uint type, int cx, int cy, uint load);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint tpm);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")] private static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA data);
    [DllImport("uxtheme.dll", EntryPoint = "#135")] private static extern int SetPreferredAppMode(int mode);
    [DllImport("uxtheme.dll", EntryPoint = "#136")] private static extern void FlushMenuThemes();
}
