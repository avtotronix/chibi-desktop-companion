using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ChibiPet;

public partial class LauncherWindow : Window
{
    readonly AppSettings _settings;
    readonly MainWindow _petWindow;
    readonly DispatcherTimer _spinTimer = new();
    readonly DispatcherTimer _pomodoroTimer = new();

    PlayerModel3D? _previewModel;
    SkinData? _currentSkin;
    double _previewYaw = 25;
    bool _isMouseDown;
    Point _lastMousePos;

    int _timerSecondsTotal = 25 * 60;
    int _timerSecondsRemaining = 25 * 60;
    bool _isTimerRunning;

    public LauncherWindow(AppSettings settings, MainWindow petWindow)
    {
        _settings = settings;
        _petWindow = petWindow;

        InitializeComponent();

        SetupPreviewCamera();

        Loaded += OnLoaded;
        Closing += OnClosing;

        // Вращение в 3D студии
        _spinTimer.Interval = TimeSpan.FromMilliseconds(30);
        _spinTimer.Tick += (_, _) =>
        {
            if (!_isMouseDown && _previewModel != null)
            {
                _previewYaw = (_previewYaw + 0.4) % 360;
                _previewModel.Yaw = _previewYaw;
            }
        };
        _spinTimer.Start();

        // Помодоро таймер
        _pomodoroTimer.Interval = TimeSpan.FromSeconds(1);
        _pomodoroTimer.Tick += PomodoroTimer_Tick;
    }

    void SetupPreviewCamera()
    {
        PreviewViewport.Camera = new PerspectiveCamera(
            new Point3D(0, 16, 52),
            new Vector3D(0, 0, -1),
            new Vector3D(0, 1, 0),
            45);
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Синхронизация UI настроек
        SliderScale.Value = _settings.Scale;
        LblScale.Text = $"{(int)(_settings.Scale / 5.5 * 100)}%";

        SliderSpeed.Value = _settings.WalkSpeed;
        LblSpeed.Text = $"{(int)_settings.WalkSpeed} px/s";

        RadioStyleClassic.IsChecked = _settings.Style == ModelStyle.ClassicMinecraft;
        RadioStyleChibi.IsChecked = _settings.Style == ModelStyle.Chibi;

        ChkSleepGames.IsChecked = _settings.SleepInGames;
        ChkTopmost.IsChecked = _settings.Topmost;
        ChkAutoRun.IsChecked = _settings.AutoRun;

        ChkAutoIdle.IsChecked = _settings.AutoIdleEmotions;
        ChkAutoSleep.IsChecked = _settings.AutoIdleEmotions;
        ChkNightSleep.IsChecked = _settings.NightSleep;
        ChkBattery.IsChecked = _settings.BatteryReactions;
        ChkWaterPeriodic.IsChecked = _settings.WaterReminders;

        ChkFemaleGender.IsChecked = _settings.FemaleGenderEnabled;
        PanelFemaleSettings.Visibility = _settings.FemaleGenderEnabled ? Visibility.Visible : Visibility.Collapsed;
        SliderBreastSize.Value = _settings.BreastSize;
        LblBreastSize.Text = $"{(int)(_settings.BreastSize * 100)}%";
        SliderBreastDepth.Value = _settings.BreastDepth;
        LblBreastDepth.Text = $"{(int)(_settings.BreastDepth * 100)}%";
        SliderBreastYOffset.Value = _settings.BreastYOffset;
        LblBreastYOffset.Text = $"{_settings.BreastYOffset:F1}";
        SliderCleavage.Value = _settings.Cleavage;
        LblCleavage.Text = $"{_settings.Cleavage:F2}";
        ChkBreastPhysics.IsChecked = _settings.BreastPhysics;
        SliderBounceIntensity.Value = _settings.BounceIntensity;
        LblBounceIntensity.Text = $"{(int)(_settings.BounceIntensity * 100)}%";

        UpdatePetStatusUI();
        UpdateActiveSkin(_petWindow.CurrentSkin ?? SkinLoader.CreateDefaultSteveSkin(), _settings.SkinPath);
    }

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // При закрытии окна настроек чибик остаётся на экране, окно просто скрывается
        e.Cancel = true;
        Hide();
    }

    // ---------- Навигация по вкладкам ----------

    void TabSettings_Click(object sender, RoutedEventArgs e)
    {
        GridTabSettings.Visibility = Visibility.Visible;
        GridTabEmotions.Visibility = Visibility.Collapsed;
        GridTabAssistant.Visibility = Visibility.Collapsed;
    }

    void TabEmotions_Click(object sender, RoutedEventArgs e)
    {
        GridTabSettings.Visibility = Visibility.Collapsed;
        GridTabEmotions.Visibility = Visibility.Visible;
        GridTabAssistant.Visibility = Visibility.Collapsed;
    }

    void TabAssistant_Click(object sender, RoutedEventArgs e)
    {
        GridTabSettings.Visibility = Visibility.Collapsed;
        GridTabEmotions.Visibility = Visibility.Collapsed;
        GridTabAssistant.Visibility = Visibility.Visible;
    }

    // ---------- Эмоции и действия чибика ----------

    void EmotionJoy_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerJoy();
    void EmotionCry_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerCry();
    void EmotionAngry_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerAngry();
    void EmotionScared_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerScared();
    void EmotionBlush_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerBlush();
    void EmotionBored_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerBored();
    void EmotionSleep_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerSleep();
    void EmotionWake_Click(object sender, RoutedEventArgs e) => _petWindow.WakeUp();

    void ActionCatchCursor_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerCatchCursor();
    void ActionClimbWall_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerClimbWall();
    void ActionDance_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerDance();
    void ActionMining_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerMining();
    void ActionFishing_Click(object sender, RoutedEventArgs e) => _petWindow.TriggerFishing();

    void ChkAutoOptions_Click(object sender, RoutedEventArgs e)
    {
        bool autoIdle = ChkAutoIdle.IsChecked == true || ChkAutoSleep.IsChecked == true;
        bool night = ChkNightSleep.IsChecked == true;
        bool battery = ChkBattery.IsChecked == true;
        _petWindow.UpdateAutoOptions(autoIdle, _settings.WaterReminders, night, battery);
    }

    // ---------- Напоминания и Помощник ----------

    void ReminderWater_Click(object sender, RoutedEventArgs e)
    {
        _petWindow.TriggerJoy();
        _petWindow.ShowSpeechBubble("Пора выпить стакан чистой воды! 💧", "💧", 5.0);
    }

    void ReminderEyes_Click(object sender, RoutedEventArgs e)
    {
        _petWindow.TriggerBlush();
        _petWindow.ShowSpeechBubble("Посмотри в окно 20 секунд и поморгай! 👀", "👀", 5.0);
    }

    void ReminderStretch_Click(object sender, RoutedEventArgs e)
    {
        _petWindow.TriggerBored();
        _petWindow.ShowSpeechBubble("Встань из-за стола и разомни плечи! 🧘", "🧘", 5.0);
    }

    void ReminderGoodDay_Click(object sender, RoutedEventArgs e)
    {
        _petWindow.TriggerJoy();
        _petWindow.ShowSpeechBubble("Хорошего и продуктивного тебе дня! ✨", "☀️", 5.0);
    }

    void BtnSendCustomNote_Click(object sender, RoutedEventArgs e)
    {
        string note = TxtCustomNote.Text.Trim();
        if (!string.IsNullOrEmpty(note))
        {
            _petWindow.TriggerJoy();
            _petWindow.ShowSpeechBubble(note, "📌", 6.0);
        }
    }

    void ChkWaterPeriodic_Click(object sender, RoutedEventArgs e)
    {
        _settings.WaterReminders = ChkWaterPeriodic.IsChecked == true;
        _petWindow.UpdateAutoOptions(_settings.AutoIdleEmotions, _settings.WaterReminders, _settings.NightSleep, _settings.BatteryReactions);
    }

    // ---------- Помодоро Таймер ----------

    void SetTimerMinutes(int minutes)
    {
        _isTimerRunning = false;
        _pomodoroTimer.Stop();
        _timerSecondsTotal = minutes * 60;
        _timerSecondsRemaining = _timerSecondsTotal;
        UpdateTimerDisplay();
        BtnTimerStart.Content = "▶ Запустить";
        BtnTimerStart.Background = (SolidColorBrush)FindResource("AccentGreen");
        TxtTimerStatus.Text = $"Таймер на {minutes} минут готов";
    }

    void TimerPreset_25(object sender, RoutedEventArgs e) => SetTimerMinutes(25);
    void TimerPreset_5(object sender, RoutedEventArgs e) => SetTimerMinutes(5);
    void TimerPreset_15(object sender, RoutedEventArgs e) => SetTimerMinutes(15);
    void TimerPreset_45(object sender, RoutedEventArgs e) => SetTimerMinutes(45);

    void BtnTimerStart_Click(object sender, RoutedEventArgs e)
    {
        if (_isTimerRunning)
        {
            _isTimerRunning = false;
            _pomodoroTimer.Stop();
            BtnTimerStart.Content = "▶ Продолжить";
            BtnTimerStart.Background = (SolidColorBrush)FindResource("AccentGreen");
            TxtTimerStatus.Text = "На паузе";
        }
        else
        {
            _isTimerRunning = true;
            _pomodoroTimer.Start();
            BtnTimerStart.Content = "⏸ Пауза";
            BtnTimerStart.Background = new SolidColorBrush(Color.FromRgb(220, 140, 40));
            TxtTimerStatus.Text = "Идёт отсчёт...";
            _petWindow.ShowSpeechBubble("Помодоро запущен! Удачи в работе! ⏱️", "⏳", 4.0);
        }
    }

    void BtnTimerReset_Click(object sender, RoutedEventArgs e)
    {
        _isTimerRunning = false;
        _pomodoroTimer.Stop();
        _timerSecondsRemaining = _timerSecondsTotal;
        UpdateTimerDisplay();
        BtnTimerStart.Content = "▶ Запустить";
        BtnTimerStart.Background = (SolidColorBrush)FindResource("AccentGreen");
        TxtTimerStatus.Text = "Сброшен";
    }

    void PomodoroTimer_Tick(object? sender, EventArgs e)
    {
        if (_timerSecondsRemaining > 0)
        {
            _timerSecondsRemaining--;
            UpdateTimerDisplay();
        }
        else
        {
            _isTimerRunning = false;
            _pomodoroTimer.Stop();
            BtnTimerStart.Content = "▶ Запустить";
            BtnTimerStart.Background = (SolidColorBrush)FindResource("AccentGreen");
            TxtTimerStatus.Text = "Время вышло! Отдых!";
            _petWindow.TriggerPomodoroFinished();
        }
    }

    void UpdateTimerDisplay()
    {
        int min = _timerSecondsRemaining / 60;
        int sec = _timerSecondsRemaining % 60;
        TxtTimerDisplay.Text = $"{min:D2}:{sec:D2}";
    }

    // ---------- 3D Превью и скины ----------

    public void UpdateActiveSkin(SkinData skin, string? skinPath)
    {
        _currentSkin = skin;
        _previewModel = new PlayerModel3D(skin, _settings.Style, GenderConfig.FromSettings(_settings));
        _previewModel.Yaw = _previewYaw;
        PreviewCharacter.Content = _previewModel.Root;

        string name = string.IsNullOrEmpty(skinPath) ? "Встроенный скин" : Path.GetFileName(skinPath);
        TxtSkinName.Text = $"Скин: {name}";
        string modelType = skin.Slim ? "Алекс (Slim 3px)" : "Стив (4px)";
        TxtSkinType.Text = $"Тип: 64×64 • {modelType}";
    }

    void UpdatePetStatusUI()
    {
        if (_settings.PetActive)
        {
            StatusDot.Fill = (SolidColorBrush)FindResource("AccentGreen");
            TxtStatus.Text = "Чибик гуляет по экрану";
            BtnTogglePet.Content = "Остановить";
            if (BtnHidePet != null)
            {
                BtnHidePet.Content = "▼";
                BtnHidePet.ToolTip = "Скрыть чибика с экрана (стрелочка вниз)";
                BtnHidePet.Foreground = (SolidColorBrush)FindResource("AccentBlue");
            }
        }
        else
        {
            StatusDot.Fill = (SolidColorBrush)FindResource("TextMuted");
            TxtStatus.Text = "Чибик скрыт с экрана";
            BtnTogglePet.Content = "Запустить";
            if (BtnHidePet != null)
            {
                BtnHidePet.Content = "▲";
                BtnHidePet.ToolTip = "Вернуть чибика на экран (стрелочка вверх)";
                BtnHidePet.Foreground = (SolidColorBrush)FindResource("AccentGreen");
            }
        }
    }

    void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isMouseDown = true;
        _lastMousePos = e.GetPosition(this);
        Mouse.Capture(sender as IInputElement);
    }

    void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isMouseDown = false;
        Mouse.Capture(null);
    }

    void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isMouseDown && _previewModel != null)
        {
            var pos = e.GetPosition(this);
            double dx = pos.X - _lastMousePos.X;
            _previewYaw = (_previewYaw + dx * 0.8) % 360;
            _previewModel.Yaw = _previewYaw;
            _lastMousePos = pos;
        }
    }

    void BtnLoadSkin_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите скин Minecraft (PNG 64x64 или 64x32)",
            Filter = "Скин Minecraft (*.png)|*.png|Все файлы (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            ApplyNewSkin(dlg.FileName);
        }
    }

    void PresetSteve_Click(object sender, RoutedEventArgs e)
    {
        string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skin.png");
        if (!File.Exists(p)) p = "skin.png";
        if (File.Exists(p))
        {
            ApplyNewSkin(p);
        }
        else
        {
            var skin = SkinLoader.CreateDefaultSteveSkin();
            _settings.SkinPath = null;
            _settings.Save();
            _petWindow.SetExplicitSkin(skin, null);
            UpdateActiveSkin(skin, "Классический Стив");
        }
    }

    void PresetAlex_Click(object sender, RoutedEventArgs e)
    {
        var skin = SkinLoader.CreateDefaultAlexSkin();
        _settings.SkinPath = null;
        _settings.Save();
        _petWindow.SetExplicitSkin(skin, null);
        UpdateActiveSkin(skin, "Классическая Алекс");
    }

    void PresetGanyu_Click(object sender, RoutedEventArgs e)
    {
        string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ganyu.png");
        if (!File.Exists(p)) p = "ganyu.png";
        if (File.Exists(p))
        {
            ApplyNewSkin(p);
        }
    }

    void ApplyNewSkin(string path)
    {
        try
        {
            var skin = SkinLoader.Load(path);
            _settings.SkinPath = Path.GetFullPath(path);
            _settings.Save();

            _petWindow.SetExplicitSkin(skin, _settings.SkinPath);
            UpdateActiveSkin(skin, _settings.SkinPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка скина", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void SliderScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblScale == null) return;
        _settings.Scale = e.NewValue;
        LblScale.Text = $"{(int)(e.NewValue / 5.5 * 100)}%";
        _petWindow?.UpdateScale(e.NewValue);
    }

    void SliderSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblSpeed == null) return;
        _settings.WalkSpeed = e.NewValue;
        LblSpeed.Text = $"{(int)e.NewValue} px/s";
        _petWindow?.UpdateWalkSpeed(e.NewValue);
    }

    void RadioStyle_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var style = RadioStyleChibi.IsChecked == true ? ModelStyle.Chibi : ModelStyle.ClassicMinecraft;
        _settings.Style = style;
        _petWindow?.UpdateStyle(style);
        if (_currentSkin != null) UpdateActiveSkin(_currentSkin, _settings.SkinPath);
    }

    void ChkFemaleGender_Click(object sender, RoutedEventArgs e)
    {
        _settings.FemaleGenderEnabled = ChkFemaleGender.IsChecked == true;
        PanelFemaleSettings.Visibility = _settings.FemaleGenderEnabled ? Visibility.Visible : Visibility.Collapsed;
        _settings.Save();
        _petWindow?.UpdateGenderSettings();
        if (_currentSkin != null) UpdateActiveSkin(_currentSkin, _settings.SkinPath);
    }

    void SliderBreast_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || SliderBreastSize == null || SliderBreastDepth == null || SliderBreastYOffset == null || SliderCleavage == null || SliderBounceIntensity == null) return;

        _settings.BreastSize = SliderBreastSize.Value;
        _settings.BreastDepth = SliderBreastDepth.Value;
        _settings.BreastYOffset = SliderBreastYOffset.Value;
        _settings.Cleavage = SliderCleavage.Value;
        _settings.BounceIntensity = SliderBounceIntensity.Value;

        LblBreastSize.Text = $"{(int)(_settings.BreastSize * 100)}%";
        LblBreastDepth.Text = $"{(int)(_settings.BreastDepth * 100)}%";
        LblBreastYOffset.Text = $"{_settings.BreastYOffset:F1}";
        LblCleavage.Text = $"{_settings.Cleavage:F2}";
        LblBounceIntensity.Text = $"{(int)(_settings.BounceIntensity * 100)}%";

        _settings.Save();
        _petWindow?.UpdateGenderSettings();
        if (_currentSkin != null) UpdateActiveSkin(_currentSkin, _settings.SkinPath);
    }

    void ChkBreastPhysics_Click(object sender, RoutedEventArgs e)
    {
        _settings.BreastPhysics = ChkBreastPhysics.IsChecked == true;
        _settings.Save();
    }

    void ChkSleepGames_Click(object sender, RoutedEventArgs e) =>
        _petWindow?.UpdateOptions(ChkSleepGames.IsChecked == true, _settings.Topmost);

    void ChkTopmost_Click(object sender, RoutedEventArgs e) =>
        _petWindow?.UpdateOptions(_settings.SleepInGames, ChkTopmost.IsChecked == true);

    void ChkAutoRun_Click(object sender, RoutedEventArgs e)
    {
        _settings.AutoRun = ChkAutoRun.IsChecked == true;
        _settings.Save();
    }

    void BtnTogglePet_Click(object sender, RoutedEventArgs e)
    {
        _settings.PetActive = !_settings.PetActive;
        _petWindow?.SetPetActive(_settings.PetActive);
        UpdatePetStatusUI();
    }

    void BtnHidePet_Click(object sender, RoutedEventArgs e)
    {
        _settings.PetActive = !_settings.PetActive;
        _petWindow?.SetPetActive(_settings.PetActive);
        UpdatePetStatusUI();
    }

    void BtnCloseLauncher_Click(object sender, RoutedEventArgs e) => Hide();
    void BtnMinimizeToTray_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void BtnExitAll_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void BtnCloseToTray_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
