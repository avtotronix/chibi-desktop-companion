using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ChibiPet;

/// <summary>
/// Управление иконкой в системном трее Windows (возле часов).
/// Позволяет всегда найти и настроить чибика, даже если он скрылся за окнами.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    const int NIM_ADD = 0x00000000;
    const int NIM_MODIFY = 0x00000001;
    const int NIM_DELETE = 0x00000002;

    const int NIF_MESSAGE = 0x00000001;
    const int NIF_ICON = 0x00000002;
    const int NIF_TIP = 0x00000004;

    const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 101;

    const int WM_LBUTTONUP = 0x0202;
    const int WM_RBUTTONUP = 0x0205;
    const int WM_LBUTTONDBLCLK = 0x0203;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    readonly Window _owner;
    readonly ContextMenu _contextMenu;
    readonly Action _onLeftClick;
    IntPtr _hwnd;
    IntPtr _hIcon;
    bool _isAdded;

    public TrayIconManager(Window owner, ContextMenu contextMenu, Action onLeftClick)
    {
        _owner = owner;
        _contextMenu = contextMenu;
        _onLeftClick = onLeftClick;

        if (_owner.IsLoaded)
            Initialize();
        else
            _owner.Loaded += (_, _) => Initialize();

        _owner.Closed += (_, _) => Dispose();
    }

    void Initialize()
    {
        if (_isAdded) return;
        try
        {
            var helper = new WindowInteropHelper(_owner);
            _hwnd = helper.EnsureHandle();

            var source = HwndSource.FromHwnd(_hwnd);
            source?.AddHook(WndProc);

            CreateDefaultIcon();
            AddTrayIcon("ChibiPet");
        }
        catch { /* игнорируем ошибки инициализации */ }
    }

    void CreateDefaultIcon()
    {
        // Создаем аккуратную зеленую пиксельную иконку (стиль Creeper / Grass Block)
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(86, 153, 33)); // Трава Minecraft
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(50, 90, 20), 1);
        g.DrawRectangle(pen, 0, 0, 15, 15);

        // Пиксельная мордочка
        using var brushDark = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(30, 50, 15));
        g.FillRectangle(brushDark, 3, 4, 3, 3);
        g.FillRectangle(brushDark, 10, 4, 3, 3);
        g.FillRectangle(brushDark, 6, 8, 4, 5);

        _hIcon = bmp.GetHicon();
    }

    public void UpdateSkinFaceIcon(SkinData skin)
    {
        try
        {
            // Вырезаем лицо 8x8 из скина и создаем иконку для трея
            using var bmp = new Bitmap(16, 16);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int pxIndex = ((8 + y) * 64 + (8 + x)) * 4;
                    byte b = skin.Pixels[pxIndex];
                    byte g = skin.Pixels[pxIndex + 1];
                    byte r = skin.Pixels[pxIndex + 2];
                    byte a = skin.Pixels[pxIndex + 3];

                    // Проверяем второй слой (шапка/волосы)
                    int hatIndex = ((8 + y) * 64 + (40 + x)) * 4;
                    if (skin.Pixels[hatIndex + 3] > 10)
                    {
                        b = skin.Pixels[hatIndex];
                        g = skin.Pixels[hatIndex + 1];
                        r = skin.Pixels[hatIndex + 2];
                        a = skin.Pixels[hatIndex + 3];
                    }

                    var col = System.Drawing.Color.FromArgb(a, r, g, b);
                    // Масштабируем 8x8 в 16x16
                    bmp.SetPixel(x * 2, y * 2, col);
                    bmp.SetPixel(x * 2 + 1, y * 2, col);
                    bmp.SetPixel(x * 2, y * 2 + 1, col);
                    bmp.SetPixel(x * 2 + 1, y * 2 + 1, col);
                }
            }

            IntPtr oldIcon = _hIcon;
            _hIcon = bmp.GetHicon();
            if (oldIcon != IntPtr.Zero) DestroyIcon(oldIcon);

            if (_isAdded)
            {
                UpdateTrayTip(null);
            }
        }
        catch
        {
            // Если что-то не вышло, оставляем стандартную иконку
        }
    }

    public void UpdateTrayTip(string? tip)
    {
        if (!_isAdded || _hwnd == IntPtr.Zero) return;

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_ICON | (tip != null ? NIF_TIP : 0),
            hIcon = _hIcon,
            szTip = tip ?? "ChibiPet (Minecraft)"
        };

        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    void AddTrayIcon(string tip)
    {
        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = tip
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int mouseMsg = lParam.ToInt32();
            if (mouseMsg == WM_RBUTTONUP)
            {
                SetForegroundWindow(_hwnd);
                _contextMenu.IsOpen = true;
                handled = true;
            }
            else if (mouseMsg == WM_LBUTTONUP || mouseMsg == WM_LBUTTONDBLCLK)
            {
                _onLeftClick();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_isAdded && _hwnd != IntPtr.Zero)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _isAdded = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}
