using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace ChibiPet;

/// <summary>
/// Фоновый детектор запущенных игр.
/// Когда открыта любая игра, кроме Minecraft, чибик засыпает / прячется,
/// чтобы не мешать игроку и не тратить ресурсы ПК.
/// </summary>
public sealed class GameDetector
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);

    const int SM_CXSCREEN = 0;
    const int SM_CYSCREEN = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    readonly DispatcherTimer _timer = new();
    bool _isGameRunning;
    string? _currentGameName;

    public bool IsGameRunning => _isGameRunning;
    public string? CurrentGameName => _currentGameName;

    public event Action<bool, string?>? GameStateChanged;

    public GameDetector()
    {
        _timer.Interval = TimeSpan.FromSeconds(1.5);
        _timer.Tick += (_, _) => CheckForegroundWindow();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    void CheckForegroundWindow()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return;

        string procName = "";
        string procPath = "";
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            procName = proc.ProcessName.ToLowerInvariant();
            try { procPath = proc.MainModule?.FileName?.ToLowerInvariant() ?? ""; } catch { }
        }
        catch
        {
            return;
        }

        var sbTitle = new StringBuilder(256);
        GetWindowText(hwnd, sbTitle, 256);
        string title = sbTitle.ToString();

        var sbClass = new StringBuilder(256);
        GetClassName(hwnd, sbClass, 256);
        string className = sbClass.ToString();

        // 1. Проверяем, Minecraft ли это.
        // Игры Minecraft НЕ блокируют чибика!
        if (IsMinecraft(procName, procPath, title))
        {
            SetGameState(false, null);
            return;
        }

        // 2. Исключаем стандартные программы рабочего стола и системные окна
        if (IsIgnoredSystemApp(procName, className))
        {
            SetGameState(false, null);
            return;
        }

        // 3. Проверяем, игра ли это (по списку процессов, путям или полноэкранному режиму)
        bool isGame = IsKnownGame(procName, procPath) || IsFullscreenGame(hwnd, procName);

        if (isGame)
        {
            string displayName = !string.IsNullOrWhiteSpace(title) ? title : procName;
            SetGameState(true, displayName);
        }
        else
        {
            SetGameState(false, null);
        }
    }

    void SetGameState(bool isGame, string? gameName)
    {
        if (_isGameRunning != isGame || _currentGameName != gameName)
        {
            _isGameRunning = isGame;
            _currentGameName = isGame ? gameName : null;
            GameStateChanged?.Invoke(_isGameRunning, _currentGameName);
        }
    }

    static bool IsMinecraft(string procName, string procPath, string title)
    {
        string tLow = title.ToLowerInvariant();
        if (tLow.Contains("minecraft") || tLow.Contains("badlion") || tLow.Contains("lunar client") || tLow.Contains("feather client"))
            return true;

        if (procName == "minecraft.windows" || procName == "minecraft" || procName == "minecraftlauncher")
            return true;

        if (procName == "javaw" || procName == "java")
        {
            if (procPath.Contains("minecraft") || procPath.Contains(".minecraft") || 
                procPath.Contains("prism") || procPath.Contains("curseforge") || procPath.Contains("modrinth"))
                return true;

            if (tLow.Contains("1.") || tLow.Contains("lwjgl") || tLow.Contains("glfw"))
                return true;
        }

        return false;
    }

    static bool IsIgnoredSystemApp(string procName, string className)
    {
        // Системный рабочий стол, панель задач
        if (className == "Progman" || className == "WorkerW" || 
            className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd")
            return true;

        // Популярные десктопные программы (браузеры, мессенджеры, редакторы кода)
        string[] ignored = {
            "explorer", "devenv", "code", "rider64", "notepad", "notepad++",
            "chrome", "firefox", "msedge", "opera", "brave",
            "discord", "telegram", "spotify", "slack", "taskmgr",
            "cmd", "powershell", "windowsterminal", "chibipet"
        };

        foreach (var name in ignored)
        {
            if (procName == name) return true;
        }

        return false;
    }

    static bool IsKnownGame(string procName, string procPath)
    {
        // Проверяем пути к каталогам установки игр
        if (!string.IsNullOrEmpty(procPath))
        {
            if (procPath.Contains(@"\steamapps\common\") ||
                procPath.Contains(@"\epic games\") ||
                procPath.Contains(@"\riot games\") ||
                procPath.Contains(@"\gog games\") ||
                procPath.Contains(@"\ubisoft game launcher\") ||
                procPath.Contains(@"\games\"))
            {
                // Исключаем лаунчеры внутри этих папок
                if (!procName.Contains("launcher") && !procName.Contains("update") && !procName.Contains("crash"))
                    return true;
            }
        }

        // Популярные исполняемые файлы игр
        string[] games = {
            "cs2", "dota2", "valorant", "leagueclient", "genshinimpact",
            "honkaistarrail", "zenlesszonezero", "robloxplayerbeta", "gta5",
            "r5apex", "fortniteclient-win64-shipping", "overwatch", "pubg",
            "rainbowsix", "eldenring", "cyberpunk2077", "witcher3", "rustclient"
        };

        foreach (var g in games)
        {
            if (procName == g || procName.StartsWith(g + "_")) return true;
        }

        return false;
    }

    static bool IsFullscreenGame(IntPtr hwnd, string procName)
    {
        if (GetWindowRect(hwnd, out RECT rect))
        {
            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);

            // Окно занимает весь монитор от (0,0)
            if (rect.Left <= 0 && rect.Top <= 0 && rect.Right >= screenW && rect.Bottom >= screenH)
            {
                // Если это не плеер или обычный софт в полный экран, считаем игрой
                if (procName != "vlc" && procName != "mpc-hc" && procName != "wmplayer")
                    return true;
            }
        }
        return false;
    }
}
