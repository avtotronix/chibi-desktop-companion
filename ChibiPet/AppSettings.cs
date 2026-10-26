using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace ChibiPet;

public enum ModelStyle
{
    ClassicMinecraft = 0,   // Аутентичный 1:1 Стив/Алекс
    Chibi = 1              // Милый чибик (в стиле бэби-мобов)
}

/// <summary>
/// Настройки приложения (%AppData%\ChibiPet\settings.json)
/// </summary>
public sealed class AppSettings
{
    public string? SkinPath { get; set; }
    public double Scale { get; set; } = 5.5;                  // Масштаб (размер чибика)
    public double WalkSpeed { get; set; } = 50.0;             // Скорость ходьбы
    public ModelStyle Style { get; set; } = ModelStyle.ClassicMinecraft; // Стиль модельки
    public bool SleepInGames { get; set; } = true;           // Отключаться во всех играх, кроме Minecraft
    public bool Topmost { get; set; } = true;                // Всегда поверх окон
    public bool AutoRun { get; set; } = false;               // Автозапуск с Windows
    public bool PetActive { get; set; } = true;              // Отображается ли чибик на экране
    public bool AutoIdleEmotions { get; set; } = true;       // Скучает через 5 мин, засыпает через 10 мин
    public bool WaterReminders { get; set; } = true;         // Напоминания попить воды / размяться
    public bool NightSleep { get; set; } = true;             // Авто-сон ночью после 23:00
    public bool BatteryReactions { get; set; } = true;       // Реакция на заряд и батарею

    // Настройки женской модельки (в стиле мода Wildfire's Female Gender)
    public bool FemaleGenderEnabled { get; set; } = false;   // Включить женскую модельку (грудь)
    public double BreastSize { get; set; } = 0.65;           // Размер груди (0.1 .. 1.2)
    public double BreastYOffset { get; set; } = 0.0;         // Смещение по высоте (-1.5 .. +1.5)
    public double BreastDepth { get; set; } = 1.0;           // Выступ вперёд (0.5 .. 2.0)
    public bool BreastPhysics { get; set; } = true;          // Физика покачивания
    public double BounceIntensity { get; set; } = 1.0;       // Сила покачивания (0.0 .. 2.0)
    public double Cleavage { get; set; } = 0.25;             // Расстояние / разделение (0.0 .. 0.8)

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChibiPet", "settings.json");

    const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppRegistryName = "ChibiPet";

    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            if (File.Exists(FilePath))
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            else
                settings = new AppSettings();
        }
        catch
        {
            settings = new AppSettings();
        }

        settings.AutoRun = IsAutoStartEnabled();
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            SetAutoStart(AutoRun);
        }
        catch { /* не критично */ }
    }

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            return key?.GetValue(AppRegistryName) != null;
        }
        catch { return false; }
    }

    public static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return;
            if (enable)
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(AppRegistryName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppRegistryName, false);
            }
        }
        catch { /* права доступа */ }
    }
}
