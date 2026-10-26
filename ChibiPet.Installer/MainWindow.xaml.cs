namespace ChibiPet.Installer;

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

public partial class MainWindow : Window
{
    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    const int SHCNE_ASSOCCHANGED = 0x08000000;
    const uint SHCNF_IDLIST = 0;

    bool _isFinished = false;

    public MainWindow()
    {
        InitializeComponent();

        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "ChibiPet");

        TxtInstallPath.Text = defaultPath;
    }

    void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Выберите папку для установки ChibiPet",
            InitialDirectory = TxtInstallPath.Text
        };

        if (dlg.ShowDialog() == true)
        {
            TxtInstallPath.Text = dlg.FolderName;
        }
    }

    async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_isFinished)
        {
            Close();
            return;
        }

        string targetDir = TxtInstallPath.Text.Trim();
        if (string.IsNullOrEmpty(targetDir))
        {
            MessageBox.Show("Пожалуйста, укажите папку для установки.", "ChibiPet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnInstall.IsEnabled = false;
        BtnCancel.IsEnabled = false;
        TxtInstallPath.IsEnabled = false;

        try
        {
            // 1. Остановка уже запущенных процессов
            TxtStatus.Text = "Проверка запущенных процессов...";
            PrgProgress.Value = 10;
            await Task.Run(() =>
            {
                foreach (var p in Process.GetProcessesByName("ChibiPet"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }
            });

            // 2. Создание каталога установки
            TxtStatus.Text = "Подготовка директории...";
            PrgProgress.Value = 25;
            Directory.CreateDirectory(targetDir);

            // 3. Распаковка файлов
            TxtStatus.Text = "Распаковка компонентов ChibiPet...";
            PrgProgress.Value = 40;

            await Task.Run(() =>
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "ChibiPet.Installer.Payload.app.zip";

                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                    archive.ExtractToDirectory(targetDir, overwriteFiles: true);
                }
                else
                {
                    // Запасной вариант — копирование из соседней папки
                    string fallbackZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.zip");
                    if (File.Exists(fallbackZip))
                    {
                        ZipFile.ExtractToDirectory(fallbackZip, targetDir, overwriteFiles: true);
                    }
                }
            });

            string exePath = Path.Combine(targetDir, "ChibiPet.exe");

            // 4. Создание ярлыков
            TxtStatus.Text = "Создание ярлыков и регистрация...";
            PrgProgress.Value = 70;

            string iconPath = Path.Combine(targetDir, "ChibiPet_Avatar.ico");
            if (!File.Exists(iconPath))
            {
                iconPath = exePath;
            }

            if (ChkDesktopShortcut.IsChecked == true)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateShortcut(Path.Combine(desktop, "ChibiPet.lnk"), exePath, targetDir, iconPath);
            }

            if (ChkStartMenu.IsChecked == true)
            {
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ChibiPet");
                Directory.CreateDirectory(startMenu);
                CreateShortcut(Path.Combine(startMenu, "ChibiPet.lnk"), exePath, targetDir, iconPath);
            }

            // 5. Автозагрузка
            if (ChkAutoStart.IsChecked == true)
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.SetValue("ChibiPet", $"\"{exePath}\"");
            }

            // 6. Запись деинсталлятора в Windows Settings
            RegisterUninstaller(targetDir, exePath);

            // 7. Обновление проводника
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

            PrgProgress.Value = 100;
            TxtStatus.Text = "Установка успешно завершена! 🎉";
            TxtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;

            // 8. Запуск если выбрано
            if (ChkLaunchNow.IsChecked == true && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = targetDir });
            }

            _isFinished = true;
            BtnInstall.IsEnabled = true;
            BtnInstall.Content = "Готово";
            BtnCancel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при установке:\n{ex.Message}", "ChibiPet Installer", MessageBoxButton.OK, MessageBoxImage.Error);
            BtnInstall.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            TxtStatus.Text = "Ошибка установки.";
        }
    }

    static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.IconLocation = $"{iconPath},0";
            shortcut.Description = "ChibiPet — 3D Minecraft Desktop Companion";
            shortcut.Save();
        }
        catch { }
    }

    static void RegisterUninstaller(string installDir, string exePath)
    {
        try
        {
            string uninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ChibiPet";
            using var key = Registry.CurrentUser.CreateSubKey(uninstallKeyPath);
            if (key == null) return;

            key.SetValue("DisplayName", "ChibiPet (3D Minecraft Desktop Companion)");
            key.SetValue("DisplayVersion", "1.0.0");
            key.SetValue("Publisher", "AvtoTronix");
            key.SetValue("DisplayIcon", exePath);
            key.SetValue("InstallLocation", installDir);

            // Создаем скрипт удаления
            string uninstallerScript = Path.Combine(installDir, "Uninstall.cmd");
            File.WriteAllText(uninstallerScript, 
                $"@echo off\r\n" +
                $"taskkill /F /IM ChibiPet.exe >nul 2>&1\r\n" +
                $"timeout /t 1 >nul\r\n" +
                $"del /f /q \"%USERPROFILE%\\Desktop\\ChibiPet.lnk\" >nul 2>&1\r\n" +
                $"rd /s /q \"%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\ChibiPet\" >nul 2>&1\r\n" +
                $"reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v ChibiPet /f >nul 2>&1\r\n" +
                $"reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ChibiPet\" /f >nul 2>&1\r\n" +
                $"start /b \"\" cmd /c rd /s /q \"{installDir}\" & exit\r\n");

            key.SetValue("UninstallString", $"cmd.exe /c \"{uninstallerScript}\"");
        }
        catch { }
    }
}
