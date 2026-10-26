using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ChibiPet;

public partial class App : Application
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Проверка на уже запущенный процесс ChibiPet:
        // Если приложение уже запущено, второй чибик НЕ появляется!
        var current = System.Diagnostics.Process.GetCurrentProcess();
        var running = System.Diagnostics.Process.GetProcessesByName(current.ProcessName);
        foreach (var p in running)
        {
            if (p.Id != current.Id)
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(p.MainWindowHandle);
                }
                Shutdown();
                return;
            }
        }

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            File.WriteAllText("crash.log", args.ExceptionObject.ToString());
        };

        DispatcherUnhandledException += (s, args) =>
        {
            File.WriteAllText("crash.log", args.Exception.ToString());
            args.Handled = true;
        };

        try
        {
            var settings = AppSettings.Load();
            settings.PetActive = true;

            var petWindow = new MainWindow(settings);
            var launcherWindow = new LauncherWindow(settings, petWindow);
            petWindow.SetLauncherWindow(launcherWindow);

            petWindow.Show();
            launcherWindow.Show();
        }
        catch (Exception ex)
        {
            File.WriteAllText("crash.log", ex.ToString());
            MessageBox.Show(ex.ToString(), "ChibiPet Error");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}
