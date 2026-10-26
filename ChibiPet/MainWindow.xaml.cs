using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ChibiPet;

public partial class MainWindow : Window
{
    const double WinW = 320;
    const double WinH = 270;
    const double FeetMargin = 16;

    readonly AppSettings _settings;
    readonly PetBehavior _pet = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly GameDetector _gameDetector = new();
    readonly DispatcherTimer _topmostTimer = new();
    readonly DispatcherTimer _systemCheckTimer = new();
    readonly Random _rng = new();

    LauncherWindow? _launcher;
    TrayIconManager? _tray;
    PlayerModel3D? _model;
    SkinData? _currentSkin;
    double _lastT, _phase, _yaw;
    Vector _dragOffset;
    Point _mouseDownPos;
    DateTime _mouseDownTime;
    bool _isPausedByGame;
    double _particleTimer;
    bool _warnedBattery;
    bool _wasOnBattery;
    DateTime _lastWaterReminder = DateTime.Now;
    double _autonomousActionTimer = 22.0;

    bool _isMining;
    bool _isMiningWood;
    TreeType _currentTree;
    OreType _currentOre;
    bool _isCurrentDeepslate;
    int _lastCrackStage;
    double _minedDropTimer;
    double _minedDropPhase;
    double _miningTimer;
    double _miningPhase;
    double _lastSparkTime;
    string? _minedResourceName;
    bool _isFishing;
    bool _isFishingBite;
    double _fishingTimer;
    double _fishingPhase;
    double _lootDisplayTimer;

    double _breastSpringY;
    double _breastVelocityY;
    double _breastSpringPitch;
    double _breastVelocityPitch;

    public SkinData? CurrentSkin => _currentSkin;
    public PetBehavior Pet => _pet;

    [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint p);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("kernel32.dll")]
    static extern bool GetSystemPowerStatus(out SystemPowerStatus sps);

    const int GWL_EXSTYLE = -20;
    const int WS_EX_TOOLWINDOW = 0x00000080;

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemPowerStatus
    {
        public byte ACLineStatus; // 0 = Offline, 1 = Online, 255 = Unknown
        public byte BatteryFlag;
        public byte BatteryLifePercent; // 0..100, 255 = Unknown
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    public MainWindow(AppSettings settings)
    {
        _settings = settings;

        InitializeComponent();
        Width = WinW;
        Height = WinH;

        UpdateCamera();

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Скрываем прозрачное окно чибика из Alt+Tab и панели задач Windows
        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        }
    }

    public void SetLauncherWindow(LauncherWindow launcher)
    {
        _launcher = launcher;
    }

    public void UpdateCamera()
    {
        double s = _settings.Scale;
        View.Camera = new OrthographicCamera(
            new Point3D(0, (WinH / 2.0 - FeetMargin) / s, 100),
            new Vector3D(0, 0, -1),
            new Vector3D(0, 1, 0),
            WinW / s);
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        _pet.Bounds = area;
        _pet.X = area.Left + area.Width / 2;
        _pet.Y = area.Bottom;
        _pet.WalkSpeed = _settings.WalkSpeed;
        _pet.AutoIdleEmotions = _settings.AutoIdleEmotions;
        _pet.AutoNightSleep = _settings.NightSleep;
        PlaceWindow();

        LoadInitialSkin();

        _tray = new TrayIconManager(this, PetMenu, () => OpenLauncher());
        if (_currentSkin != null)
        {
            _tray.UpdateSkinFaceIcon(_currentSkin);
            DesktopIconHelper.UpdateDesktopIcon(_currentSkin);
        }

        MenuSleepGames.IsChecked = _settings.SleepInGames;
        MenuTopmost.IsChecked = _settings.Topmost;
        MenuAutoRun.IsChecked = _settings.AutoRun;
        Topmost = _settings.Topmost;

        _gameDetector.GameStateChanged += OnGameStateChanged;
        _gameDetector.Start();

        _topmostTimer.Interval = TimeSpan.FromSeconds(2);
        _topmostTimer.Tick += (_, _) => EnforceTopmost();
        _topmostTimer.Start();

        _systemCheckTimer.Interval = TimeSpan.FromSeconds(30);
        _systemCheckTimer.Tick += (_, _) => CheckSystemConditions();
        _systemCheckTimer.Start();

        CompositionTarget.Rendering += OnRender;
        _pet.TriggerClickJoy();
    }

    void OnClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRender;
        _gameDetector.Stop();
        _topmostTimer.Stop();
        _systemCheckTimer.Stop();
        _tray?.Dispose();
    }

    public void OpenLauncher()
    {
        if (_launcher == null)
        {
            _launcher = new LauncherWindow(_settings, this);
        }
        _launcher.Show();
        _launcher.WindowState = WindowState.Normal;
        _launcher.Activate();
    }

    void OpenLauncher_Click(object sender, RoutedEventArgs e) => OpenLauncher();

    void EnforceTopmost()
    {
        if (!_settings.Topmost || _isPausedByGame) return;
        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            SetWindowPos(helper.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    void OnGameStateChanged(bool isGameRunning, string? gameName)
    {
        if (!_settings.SleepInGames) return;

        if (isGameRunning)
        {
            _isPausedByGame = true;
            // Идет спать в правый угол экрана
            _pet.GoToCornerAndSleep();
            PlaceWindow();
            _tray?.UpdateTrayTip($"ChibiPet (Спит в углу — {gameName})");
        }
        else
        {
            _isPausedByGame = false;
            _pet.WakeUp();
            _pet.TriggerClickJoy();
            _tray?.UpdateTrayTip("ChibiPet (Minecraft)");
            EnforceTopmost();
        }
    }

    void CheckSystemConditions()
    {
        // 1. Проверка батареи
        if (_settings.BatteryReactions && GetSystemPowerStatus(out var sps))
        {
            if (sps.ACLineStatus == 0 && sps.BatteryLifePercent < 20 && !_warnedBattery)
            {
                _warnedBattery = true;
                _wasOnBattery = true;
                _pet.TriggerBored();
                ShowSpeechBubble($"Батарея почти села ({sps.BatteryLifePercent}%)! Нужна зарядка ⚡", "🪫", 6.0);
            }
            else if (sps.ACLineStatus == 1 && _wasOnBattery)
            {
                _wasOnBattery = false;
                _warnedBattery = false;
                _pet.TriggerClickJoy();
                ShowSpeechBubble("Ура, зарядка подключена! ⚡", "🔋", 5.0);
            }
            else if (sps.BatteryLifePercent >= 25)
            {
                _warnedBattery = false;
            }
        }

        // 2. Напоминания попить воды
        if (_settings.WaterReminders)
        {
            if ((DateTime.Now - _lastWaterReminder).TotalMinutes >= 45)
            {
                _lastWaterReminder = DateTime.Now;
                _pet.TriggerClickJoy();
                string[] waterTips = {
                    "Пора выпить стакан воды! 💧",
                    "Отдохни от экрана и поморгай 👀",
                    "Встань и немного разомнись! 🧘"
                };
                ShowSpeechBubble(waterTips[_rng.Next(waterTips.Length)], "💧", 6.0);
            }
        }

        // 3. Авто-сон ночью
        if (_settings.NightSleep)
        {
            int hour = DateTime.Now.Hour;
            if ((hour >= 23 || hour < 7) && _pet.State != PetState.Sleeping && !_pet.IsSleepingManually)
            {
                _pet.TriggerSleep();
                ShowSpeechBubble("Уже поздняя ночь... Пора спать 🌙", "💤", 6.0);
            }
        }
    }

    public void SetPetActive(bool active)
    {
        _settings.PetActive = active;
        if (active)
        {
            Visibility = Visibility.Visible;
            CompositionTarget.Rendering += OnRender;
            _pet.TriggerClickJoy();
            EnforceTopmost();
        }
        else
        {
            CompositionTarget.Rendering -= OnRender;
            Visibility = Visibility.Collapsed;
        }
    }

    public void UpdateScale(double scale)
    {
        _settings.Scale = scale;
        _settings.Save();
        UpdateCamera();
    }

    public void UpdateWalkSpeed(double speed)
    {
        _settings.WalkSpeed = speed;
        _settings.Save();
        _pet.WalkSpeed = speed;
    }

    public void UpdateStyle(ModelStyle style)
    {
        _settings.Style = style;
        _settings.Save();
        if (_currentSkin != null) ApplySkinModel(_currentSkin);
    }

    public void UpdateOptions(bool sleepInGames, bool topmost)
    {
        _settings.SleepInGames = sleepInGames;
        _settings.Topmost = topmost;
        Topmost = topmost;
        _settings.Save();
        EnforceTopmost();
    }

    public void UpdateAutoOptions(bool autoIdle, bool water, bool night, bool battery)
    {
        _settings.AutoIdleEmotions = autoIdle;
        _settings.WaterReminders = water;
        _settings.NightSleep = night;
        _settings.BatteryReactions = battery;
        _pet.AutoIdleEmotions = autoIdle;
        _pet.AutoNightSleep = night;
        _settings.Save();
    }

    public void SetExplicitSkin(SkinData skin, string? path)
    {
        _currentSkin = skin;
        ApplySkinModel(skin);
        _tray?.UpdateSkinFaceIcon(skin);
        DesktopIconHelper.UpdateDesktopIcon(skin);
    }

    // ---------- Эмоции и публичные команды ----------

    public void TriggerJoy() { _pet.TriggerClickJoy(); SpawnHeartParticles(); }
    public void TriggerCry() { _pet.TriggerCry(); SpawnTears(); }
    public void TriggerAngry() { _pet.TriggerAngry(); SpawnAnger(); }
    public void TriggerScared() { _pet.TriggerScared(); SpawnSweat(); }
    public void TriggerBlush() { _pet.TriggerBlush(); SpawnHeartParticles(); }
    public void TriggerBored() { _pet.TriggerBored(); SpawnSweat(); }
    public void TriggerSleep() { _pet.TriggerSleep(); }
    public void TriggerDance() { _pet.TriggerDance(); SpawnMusic(); }

    public void TriggerMining()
    {
        _miningTimer = 3.8;
        _pet.TriggerMining(_miningTimer + 0.5);
        _isMining = true;
        _miningPhase = 0;
        _lastCrackStage = 0;
        _minedDropTimer = 0;

        bool isTree = _rng.NextDouble() < 0.5;
        _isMiningWood = isTree;
        if (isTree)
        {
            _currentTree = (TreeType)_rng.Next(11);
            if (_model != null)
            {
                _model.HeldTool = HeldToolType.NetheriteAxe;
                _model.SetWorldBlock(MinecraftVoxelItems.CreateTreeBlock(_currentTree, 0));
                _model.SetWorldBlockPosition(_pet.Facing > 0 ? 8.2 : -8.2, 0, 4.5);
            }
            _minedResourceName = MinecraftVoxelItems.GetTreeName(_currentTree);
            ShowSpeechBubble($"Рубим {_minedResourceName} незеритовым топором! 🪓", "🪓", 3.0);
        }
        else
        {
            _currentOre = (OreType)_rng.Next(11);
            _isCurrentDeepslate = _rng.NextDouble() < 0.4 && _currentOre != OreType.NetherQuartz && _currentOre != OreType.NetherGold && _currentOre != OreType.AncientDebris;
            if (_model != null)
            {
                _model.HeldTool = HeldToolType.NetheritePickaxe;
                _model.SetWorldBlock(MinecraftVoxelItems.CreateOreBlock(_currentOre, _isCurrentDeepslate, 0));
                _model.SetWorldBlockPosition(_pet.Facing > 0 ? 8.2 : -8.2, 0, 4.5);
            }
            _minedResourceName = MinecraftVoxelItems.GetOreName(_currentOre) + (_isCurrentDeepslate ? " (глубинный сланец)" : "");
            ShowSpeechBubble($"Добываем {_minedResourceName} незеритовой киркой! ⛏️", "⛏️", 3.0);
        }
    }

    public void TriggerFishing()
    {
        _fishingTimer = _rng.Next(7, 18);
        _pet.TriggerFishing(_fishingTimer + 6.0);
        _isFishing = true;
        _isFishingBite = false;
        _fishingPhase = 0;
        _lootDisplayTimer = 0;

        if (_model != null)
        {
            _model.HeldTool = HeldToolType.FishingRod;
            _model.SetWorldBlock(MinecraftVoxelItems.CreateFishingSpot(0, false));
            _model.SetWorldBlockPosition(_pet.Facing > 0 ? 8 : -8, 0, 11);
        }

        ShowSpeechBubble("Закинули удочку... Ждём поклёвки 🌊", "🎣", 3.5);
    }

    public void TriggerCatchCursor()
    {
        _pet.TriggerCatchCursor();
        ShowSpeechBubble("Попробуй поймать меня курсором! 🎯", "🐾", 3.5);
    }

    public void TriggerClimbWall()
    {
        _pet.TriggerClimbWall();
        ShowSpeechBubble("Лезем по стене наверх! 🧗", "🧗", 3.0);
    }

    void ResolveFishingCatch()
    {
        var loot = (FishingLootType)_rng.Next(7);
        if (_model != null)
        {
            _model.HeldTool = HeldToolType.None;
            _model.SetWorldBlock(MinecraftVoxelItems.CreateFishingLoot(loot));
            _model.SetWorldBlockPosition(0, 9.5, 4.0);
        }
        _lootDisplayTimer = 4.5;

        switch (loot)
        {
            case FishingLootType.Cod:
                TriggerJoy();
                ShowSpeechBubble("Поймана сырая треска! 🐟 Вкусный обед!", "🐟", 5.0);
                break;
            case FishingLootType.Salmon:
                TriggerJoy();
                ShowSpeechBubble("Ого, свежий лосось! 🍣 Рыбалка удалась!", "🍣", 5.0);
                break;
            case FishingLootType.TropicalFish:
                TriggerBlush();
                ShowSpeechBubble("Редкая тропическая рыбка-клоун! 🐠 Красотища!", "🐠", 5.0);
                break;
            case FishingLootType.Pufferfish:
                TriggerScared();
                ShowSpeechBubble("ОЙ! Надулся ядовитый иглобрюх! 🐡 Ай-ай!", "🐡", 5.0);
                break;
            case FishingLootType.EnchantedBook:
                TriggerJoy();
                ShowSpeechBubble("ЛЕГЕНДАРНЫЙ ДРОП! Зачарованная книга «Починка»! 📖✨", "✨", 6.0);
                break;
            case FishingLootType.TNT:
                TriggerAngry();
                ShowSpeechBubble("СТОП, ЭТО ДИНАМИТ?! П-Ш-Ш-Ш! 💣 БЕЖИИИМ!", "💥", 5.0);
                break;
            case FishingLootType.Diamond:
                TriggerDance();
                ShowSpeechBubble("ДЖЕКПОТ! Настоящий Алмаз со дна реки! 💎✨", "💎", 6.0);
                break;
        }
    }

    void TriggerRandomAutonomousBehavior()
    {
        int action = _rng.Next(8);
        switch (action)
        {
            case 0:
                TriggerMining();
                break;
            case 1:
                TriggerFishing();
                break;
            case 2:
                TriggerClimbWall();
                break;
            case 3:
                TriggerDance();
                break;
            case 4:
                TriggerCatchCursor();
                break;
            case 5:
                TriggerJoy();
                break;
            case 6:
                TriggerBlush();
                break;
            case 7:
                ShowRandomReminderTip();
                break;
        }
    }

    void ShowRandomReminderTip()
    {
        string[] tips = {
            "Пора выпить стакан чистой воды! 💧",
            "Посмотри в окно 20 секунд и поморгай! 👀",
            "Встань из-за стола и разомни плечи! 🧘",
            "Хорошего и продуктивного тебе дня! ✨",
            "Не забывай держать осанку ровно! 🪑"
        };
        string[] icons = { "💧", "👀", "🧘", "✨", "🪑" };
        int idx = _rng.Next(tips.Length);
        TriggerJoy();
        ShowSpeechBubble(tips[idx], icons[idx], 5.0);
    }

    public void WakeUp() { _pet.WakeUp(); _pet.TriggerClickJoy(); }

    public void TriggerPomodoroFinished()
    {
        _pet.TriggerClickJoy();
        SpawnHeartParticles();
        ShowSpeechBubble("Время вышло! Отличная работа! 🎉", "🔔", 8.0);
    }

    public void ToggleSleep()
    {
        if (_pet.State == PetState.Sleeping)
            _pet.WakeUp();
        else
            _pet.TriggerSleep();
    }

    void ToggleSleep_Click(object sender, RoutedEventArgs e) => ToggleSleep();

    void LoadInitialSkin()
    {
        string baseSkin = Path.Combine(AppContext.BaseDirectory, "skin.png");
        string? targetPath = null;

        if (!string.IsNullOrEmpty(_settings.SkinPath) && File.Exists(_settings.SkinPath))
            targetPath = _settings.SkinPath;
        else if (File.Exists(baseSkin))
            targetPath = baseSkin;

        if (targetPath != null && TrySetSkin(targetPath))
            return;

        _currentSkin = SkinLoader.CreateDefaultSteveSkin();
        ApplySkinModel(_currentSkin);
    }

    bool ChooseSkin()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выбери скин Minecraft (PNG 64x64 или 64x32)",
            Filter = "Скин Minecraft (*.png)|*.png|Все файлы (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true && TrySetSkin(dlg.FileName))
        {
            _launcher?.UpdateActiveSkin(_currentSkin!, dlg.FileName);
            return true;
        }
        return false;
    }

    bool TrySetSkin(string path)
    {
        try
        {
            var skin = SkinLoader.Load(path);
            _settings.SkinPath = Path.GetFullPath(path);
            _settings.Save();
            SetExplicitSkin(skin, _settings.SkinPath);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ChibiPet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    void ApplySkinModel(SkinData skin)
    {
        _currentSkin = skin;
        _model = new PlayerModel3D(skin, _settings.Style, GenderConfig.FromSettings(_settings));
        Character.Content = _model.Root;
    }

    public void UpdateGenderSettings()
    {
        if (_currentSkin != null)
        {
            ApplySkinModel(_currentSkin);
        }
    }

    // ---------- Обработка мыши (перетаскивание) ----------

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _mouseDownPos = e.GetPosition(this);
        _mouseDownTime = DateTime.UtcNow;

        if (GetCursorPos(out var pt))
        {
            _dragOffset = new Vector(Left - pt.X, Top - pt.Y);
            _pet.BeginDrag();
            CaptureMouse();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pet.State == PetState.Dragged && GetCursorPos(out var pt))
        {
            double newLeft = pt.X + _dragOffset.X;
            double newTop = pt.Y + _dragOffset.Y;

            Left = newLeft;
            Top = newTop;

            _pet.X = newLeft + WinW / 2.0;
            _pet.Y = newTop + WinH - FeetMargin;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_pet.State == PetState.Dragged)
        {
            ReleaseMouseCapture();
            _pet.EndDrag();

            var upPos = e.GetPosition(this);
            var dist = (upPos - _mouseDownPos).Length;
            var elapsed = (DateTime.UtcNow - _mouseDownTime).TotalMilliseconds;
            if (dist < 6 && elapsed < 300)
            {
                TriggerJoy();
            }
        }
    }

    void PlaceWindow()
    {
        Left = _pet.X - WinW / 2.0;
        Top = _pet.Y - WinH + FeetMargin;
    }

    // ---------- Анимационный цикл (60 FPS) ----------

    void OnRender(object? sender, EventArgs e)
    {
        double t = _clock.Elapsed.TotalSeconds;
        double dt = _lastT <= 0 ? 0.016 : Math.Clamp(t - _lastT, 0.001, 0.05);
        _lastT = t;

        _pet.Update(dt);

        if (_pet.State != PetState.Dragged)
        {
            PlaceWindow();
        }

        UpdateEmotionParticles(dt);

        if (_model == null) return;

        // Автоматическое рандомное использование эмоций и действий чибиком
        if (_settings.AutoIdleEmotions && (_pet.State == PetState.Idle || _pet.State == PetState.Walk) && !_isMining && !_isFishing && !_pet.IsSleepingManually && !_isPausedByGame)
        {
            _autonomousActionTimer -= dt;
            if (_autonomousActionTimer <= 0)
            {
                _autonomousActionTimer = _rng.Next(20, 45);
                TriggerRandomAutonomousBehavior();
            }
        }

        if (_isMining)
        {
            _miningTimer -= dt;

            // Динамическое растрескивание блока от ударов кирки/топора (стадии 0..3)
            double progress = Math.Clamp(1.0 - (_miningTimer / 3.8), 0.0, 1.0);
            int stage = Math.Clamp((int)(progress * 4.0), 0, 3);
            if (stage != _lastCrackStage)
            {
                _lastCrackStage = stage;
                _model.SetWorldBlock(_isMiningWood
                    ? MinecraftVoxelItems.CreateTreeBlock(_currentTree, stage)
                    : MinecraftVoxelItems.CreateOreBlock(_currentOre, _isCurrentDeepslate, stage));
            }

            // Ритмичная отдача и сотрясение блока при каждом ударе кирки/топора
            double cycle = _miningPhase % 1.0;
            double shake = (cycle >= 0.50 && cycle <= 0.68) ? Math.Sin((cycle - 0.50) * Math.PI * 4.0) * 0.45 : 0;
            _model.SetWorldBlockPosition(_pet.Facing > 0 ? 8.2 + shake : -8.2 - shake, 0, 4.5);

            if (cycle >= 0.50 && cycle <= 0.62)
            {
                SpawnMiningSparks(_isMiningWood);
            }

            if (_miningTimer <= 0)
            {
                _isMining = false;
                _model.HeldTool = HeldToolType.None; // кирка/топор убираются из руки

                // Взрыв осколков блока при разрушении
                SpawnBlockBreakDebris(_isMiningWood);

                // Появление выпавшего ресурса
                _model.SetWorldBlock(MinecraftVoxelItems.CreateMiniDrop(_isMiningWood, _currentTree, _currentOre));
                _minedDropTimer = 2.4;
                _minedDropPhase = 0;

                ShowSpeechBubble($"Успешно добыто: {_minedResourceName}! 🎉", "📦", 4.5);
                TriggerJoy();
            }
        }

        if (_minedDropTimer > 0)
        {
            _minedDropTimer -= dt;
            _minedDropPhase += dt * 5.0;

            // Анимация подпрыгивания, вращения и плавного притягивания дропа к руке Стива
            double dropT = 1.0 - (_minedDropTimer / 2.4);
            double hopY = Math.Sin(dropT * Math.PI) * 4.5;
            double startX = _pet.Facing > 0 ? 8.2 : -8.2;
            double currentX = startX * (1.0 - dropT * 0.85);
            double currentZ = 4.5 * (1.0 - dropT * 0.5);

            _model.SetWorldBlockTransform(currentX, 2.0 + hopY, currentZ, yaw: _minedDropPhase * 45.0, pitch: 0, scale: Math.Max(0.1, 1.0 - dropT * 0.3));

            if (_minedDropTimer <= 0)
            {
                _model.SetWorldBlock(null);
            }
        }

        if (_isFishing)
        {
            _fishingTimer -= dt;

            // Поклёвка наступает за 1.8 секунды до вытягивания рыбы
            if (_fishingTimer <= 1.8 && !_isFishingBite)
            {
                _isFishingBite = true;
                ShowSpeechBubble("КЛЮЁТ! ТЯНИ! 🎣💥", "💥", 2.2);
                SpawnWaterSplashes();
            }

            // Покачивание или резкое погружение поплавка под воду при поклёвке
            double dip;
            if (_isFishingBite)
            {
                dip = -1.2 + Math.Sin(_fishingPhase * 12.0) * 0.35;
                if (_rng.NextDouble() < 0.22) SpawnWaterSplashes();
            }
            else
            {
                dip = Math.Sin(_fishingPhase * 3.0) * 0.28;
            }

            _model.SetWorldBlock(MinecraftVoxelItems.CreateFishingSpot(dip, _isFishingBite));
            _model.SetWorldBlockPosition(_pet.Facing > 0 ? 8 : -8, 0, 11);

            if (_fishingTimer <= 0)
            {
                _isFishing = false;
                _isFishingBite = false;
                ResolveFishingCatch();
            }
        }

        if (_lootDisplayTimer > 0)
        {
            _lootDisplayTimer -= dt;

            // Анимация полета улова по дуге из воды наверх к Стиву и вращения в воздухе
            double lootT = Math.Clamp((4.5 - _lootDisplayTimer) / 1.0, 0.0, 1.0);
            double flyY;
            double flyX;
            double flyZ;
            if (lootT < 1.0)
            {
                double waterX = _pet.Facing > 0 ? 8.0 : -8.0;
                flyX = waterX * (1.0 - lootT);
                flyZ = 11.0 * (1.0 - lootT) + 4.0 * lootT;
                flyY = Math.Sin(lootT * Math.PI) * 14.0 + 9.5 * lootT;
            }
            else
            {
                flyX = 0;
                flyZ = 4.0;
                flyY = 9.5 + Math.Sin((4.5 - _lootDisplayTimer) * 3.5) * 0.7;
            }
            double spin = (4.5 - _lootDisplayTimer) * 120.0;
            _model.SetWorldBlockTransform(flyX, flyY, flyZ, yaw: spin, pitch: 0, scale: 1.0);

            if (_lootDisplayTimer <= 0)
            {
                _model.SetWorldBlock(null);
            }
        }

        if (!_isMining && !_isFishing && _lootDisplayTimer <= 0 && _minedDropTimer <= 0 && _pet.State != PetState.Mining && _pet.State != PetState.Fishing && _pet.State != PetState.Joy)
        {
            if (_model.HeldTool != HeldToolType.None)
            {
                _model.HeldTool = HeldToolType.None;
            }
            if (_lootDisplayTimer <= 0 && _minedDropTimer <= 0)
            {
                _model.SetWorldBlock(null);
            }
        }

        if (_pet.State != PetState.Sleeping)
        {
            _model.ShowShadow = true;
        }

        if (_settings.FemaleGenderEnabled && _settings.BreastPhysics)
        {
            double intensity = _settings.BounceIntensity * (_settings.BreastSize / 0.65);
            double driveY = 0;
            double drivePitch = 0;

            if (_pet.State == PetState.Walk)
            {
                // Естественное вертикальное покачивание при ходьбе в такт шагам
                driveY = Math.Sin(_phase * 2.0) * 0.08;
                drivePitch = Math.Sin(_phase * 2.0 - 0.2) * 0.6;
            }
            else if (_pet.State == PetState.CatchCursor)
            {
                // При быстром беге чуть активнее
                driveY = Math.Sin(_phase * 2.0) * 0.13;
                drivePitch = Math.Sin(_phase * 2.0 - 0.2) * 1.0;
            }
            else if (_pet.State == PetState.Joy || _pet.State == PetState.Dance)
            {
                // При прыжках радости
                driveY = Math.Sin(t * 12.0) * 0.15;
                drivePitch = Math.Sin(t * 12.0) * 1.2;
            }
            else if (_pet.State == PetState.Fall || _pet.State == PetState.WallJump)
            {
                // Инерция вертикального движения при свободном падении/прыжке
                driveY = Math.Clamp(-_pet.VY * 0.00015, -0.22, 0.22);
                drivePitch = Math.Clamp(-_pet.VY * 0.001, -1.2, 1.2);
            }
            else if (_pet.State == PetState.Climbing)
            {
                driveY = Math.Sin(_phase * 2.0) * 0.05;
                drivePitch = Math.Sin(_phase * 2.0) * 0.4;
            }
            else if (_pet.State == PetState.Sleeping)
            {
                driveY = 0;
                drivePitch = 0;
            }
            else
            {
                // Незаметное нежное дыхание в покое
                driveY = Math.Sin(t * 2.2) * 0.015;
                drivePitch = Math.Sin(t * 2.2) * 0.15;
            }

            double targetY = driveY * intensity;
            double targetPitch = drivePitch * intensity;

            const double kSpring = 150.0;
            const double dDamping = 16.0;

            double accelY = kSpring * (targetY - _breastSpringY) - dDamping * _breastVelocityY;
            _breastVelocityY += accelY * dt;
            _breastSpringY += _breastVelocityY * dt;

            double accelPitch = kSpring * (targetPitch - _breastSpringPitch) - dDamping * _breastVelocityPitch;
            _breastVelocityPitch += accelPitch * dt;
            _breastSpringPitch += _breastVelocityPitch * dt;

            _model.SetBreastBounce(_breastSpringY, _breastSpringPitch);
        }
        else
        {
            _breastSpringY = 0;
            _breastVelocityY = 0;
            _breastSpringPitch = 0;
            _breastVelocityPitch = 0;
            _model.SetBreastBounce(0, 0);
        }

        var p = new Pose();
        double targetYaw = 0;

        switch (_pet.State)
        {
            case PetState.Walk:
            {
                _phase += dt * 8.5;
                double s = Math.Sin(_phase);
                p.LegRSwing = 38 * s;
                p.LegLSwing = -38 * s;
                p.ArmRSwing = -32 * s;
                p.ArmLSwing = 32 * s;
                p.HeadYaw = 10 * Math.Sin(_phase * 0.5);
                targetYaw = _pet.Facing * 70;
                break;
            }
            case PetState.CatchCursor:
            {
                _phase += dt * 11.0;
                double s = Math.Sin(_phase);
                p.LegRSwing = 42 * s;
                p.LegLSwing = -42 * s;
                p.ArmRSwing = -38 * s;
                p.ArmLSwing = 38 * s;
                p.BodyPitch = 10;
                p.HeadPitch = 6;
                targetYaw = _pet.Facing * 70;
                break;
            }
            case PetState.Idle:
            {
                p.ArmSpread = 4 + 2 * Math.Sin(t * 2.2);
                p.HeadYaw = 22 * Math.Sin(t * 0.7);
                p.HeadPitch = 6 * Math.Sin(t * 1.1);
                p.HeadRoll = 4 * Math.Sin(t * 0.5);
                targetYaw = 0;
                break;
            }
            case PetState.Joy:
            {
                double hop = Math.Abs(Math.Sin(t * 12));
                p.YOffset = hop * 4.0;
                p.ArmSpread = 80 + 30 * hop;
                p.HeadPitch = -15;
                p.LegSpread = 10;
                targetYaw = 0;
                break;
            }
            case PetState.Cry:
            {
                _phase += dt * 10;
                double s = Math.Sin(_phase);
                p.ArmSpread = 20;
                p.ArmRSwing = -55;
                p.ArmLSwing = -55;
                p.HeadPitch = 15;
                p.HeadYaw = s * 6;
                p.YOffset = Math.Abs(s) * 1.5;
                targetYaw = 0;
                break;
            }
            case PetState.Angry:
            {
                double stomp = Math.Abs(Math.Sin(t * 14));
                p.YOffset = stomp * 2.0;
                p.LegRSwing = stomp * 35;
                p.ArmSpread = 45;
                p.ArmRSwing = -30;
                p.ArmLSwing = -30;
                p.HeadPitch = -12;
                p.HeadYaw = Math.Sin(t * 8) * 12;
                targetYaw = 0;
                break;
            }
            case PetState.Scared:
            {
                double shake = Math.Sin(t * 30) * 3;
                p.XOffset = shake;
                p.ArmSpread = 20;
                p.ArmRSwing = 50;
                p.ArmLSwing = 50;
                p.HeadPitch = 15;
                p.HeadYaw = Math.Sin(t * 10) * 15;
                p.LegSpread = 14;
                targetYaw = 0;
                break;
            }
            case PetState.Blush:
            {
                double sway = Math.Sin(t * 2);
                p.BodyRoll = sway * 5;
                p.HeadRoll = sway * 8;
                p.HeadPitch = 10;
                p.HeadYaw = 18;
                p.ArmSpread = 12;
                p.ArmRSwing = -20;
                p.ArmLSwing = -20;
                targetYaw = 15;
                break;
            }
            case PetState.Bored:
            {
                double stretch = Math.Sin(t * 2);
                p.ArmSpread = 60 + 20 * stretch;
                p.ArmRSwing = -70;
                p.ArmLSwing = -70;
                p.HeadPitch = -20;
                p.HeadYaw = 8 * Math.Sin(t);
                targetYaw = 0;
                break;
            }
            case PetState.Dance:
            {
                double beat = Math.Sin(t * 10);
                p.YOffset = Math.Abs(beat) * 3.5;
                p.BodyRoll = beat * 12;
                p.HeadYaw = beat * 22;
                p.ArmSpread = 40 + beat * 20;
                p.ArmRSwing = beat * 45;
                p.ArmLSwing = -beat * 45;
                p.LegSpread = 10;
                targetYaw = 0;
                break;
            }
            case PetState.Mining:
            {
                // Реалистичный мощный рубящий взмах (Minecraft Swing Cycle)
                _miningPhase += dt * 3.3; // 3.3 взмаха в секунду!
                double cycle = _miningPhase % 1.0;
                double armSwing;
                if (cycle < 0.35)
                {
                    double u = cycle / 0.35;
                    armSwing = -15 - u * 75; // замах вверх от -15 до -90 градусов
                }
                else if (cycle < 0.60)
                {
                    double u = (cycle - 0.35) / 0.25;
                    armSwing = -90 + u * 130; // РЕЗКИЙ РУБЯЩИЙ УДАР до +40 градусов (контакт с блоком!)
                }
                else
                {
                    double u = (cycle - 0.60) / 0.40;
                    armSwing = 40 - u * 55; // отскок от блока и возврат в исходное положение
                }

                p.ArmRSwing = armSwing;
                p.ArmLSwing = 22 - Math.Sin(_miningPhase * 2.0) * 8; // левая рука балансирует
                p.HeadPitch = (cycle >= 0.50 && cycle <= 0.68) ? 24 : 12; // голова кивает в такт ударам
                p.HeadYaw = _pet.Facing * -18; // внимательный взгляд на блок
                p.BodyPitch = (cycle >= 0.50 && cycle <= 0.68) ? 12 : 3; // тело наклоняется в удар
                p.LegRSwing = 12;
                p.LegLSwing = -12;
                targetYaw = _pet.Facing * 50; // поворот корпуса к блоку
                break;
            }
            case PetState.Fishing:
            {
                _fishingPhase += dt * 2.5;

                if (_isFishingBite)
                {
                    // ПОКЛЁВКА! Резкая подсечка удилищем вверх и назад
                    p.ArmRSwing = -92 + Math.Sin(_fishingPhase * 12.0) * 8; // удилище высоко поднято и дрожит от сопротивления рыбы!
                    p.ArmLSwing = -28; // вторая рука судорожно крутит катушку
                    p.HeadPitch = -18; // голова закинута назад
                    p.HeadYaw = _pet.Facing * 12;
                    p.BodyPitch = -12; // корпус откинут назад при натяжении лески
                    p.YOffset = 0; // вскочил на ноги!
                    p.LegRSwing = 12;
                    p.LegLSwing = -12;
                    targetYaw = _pet.Facing * 30;
                }
                else
                {
                    // Спокойное реалистичное ожидание у воды: чибик сидит у кромки воды
                    double sway = Math.Sin(_fishingPhase) * 3.5;
                    p.ArmRSwing = -38 + sway; // удилище плавно покачивается в руках
                    p.ArmLSwing = 16 - sway * 0.5; // левая рука придерживает
                    p.HeadPitch = 15 + Math.Sin(_fishingPhase * 1.5) * 3; // взгляд периодически следит за поплавком
                    p.HeadYaw = _pet.Facing * 12;
                    p.YOffset = -2.8; // сидит у воды
                    p.LegRSwing = -68; // ноги согнуты
                    p.LegLSwing = -68;
                    targetYaw = _pet.Facing * 30;
                }
                break;
            }
            case PetState.Climbing:
            {
                _phase += dt * 9;
                double s = Math.Sin(_phase);
                p.ArmSpread = 18;
                p.ArmRSwing = -60 + s * 35;
                p.ArmLSwing = -60 - s * 35;
                p.LegRSwing = s * 40;
                p.LegLSwing = -s * 40;
                p.HeadPitch = -25;
                targetYaw = _pet.Facing * 85;
                break;
            }
            case PetState.WallJump:
            {
                p.ArmSpread = 125;
                p.LegSpread = 25;
                p.HeadPitch = -15;
                p.BodyPitch = -10;
                targetYaw = _pet.Facing * 30;
                break;
            }
            case PetState.Dragged:
            {
                _phase += dt * 10;
                double s = Math.Sin(_phase);
                p.ArmSpread = 145;
                p.LegRSwing = 22 * s; p.LegLSwing = -22 * s;
                p.LegSpread = 12;
                p.HeadPitch = 15;
                targetYaw = 0;
                break;
            }
            case PetState.Fall:
            {
                p.ArmSpread = 110;
                p.LegSpread = 18;
                p.HeadPitch = -20;
                targetYaw = 0;
                break;
            }
            case PetState.Sleeping:
            {
                _model.ShowShadow = false;
                p.LegRSwing = 85;
                p.LegLSwing = 85;
                p.ArmSpread = 12;
                p.ArmRSwing = -28;
                p.ArmLSwing = -28;
                p.HeadPitch = 15 + Math.Sin(t * 1.5) * 2;
                p.HeadRoll = 18;
                p.HeadYaw = 10;
                p.YOffset = -5.5 + Math.Sin(t * 1.5) * 0.4;
                targetYaw = 15;
                break;
            }
        }

        _yaw += (targetYaw - _yaw) * Math.Min(1, dt * 9);
        _model.Yaw = _yaw;
        _model.Apply(p);
    }

    // ---------- Частицы и пузыри сообщений ----------

    void UpdateEmotionParticles(double dt)
    {
        _particleTimer -= dt;
        if (_particleTimer <= 0)
        {
            switch (_pet.Emotion)
            {
                case EmotionType.Zzz:
                    SpawnZzz();
                    _particleTimer = 1.2;
                    break;
                case EmotionType.Tears:
                    SpawnTears();
                    _particleTimer = 0.35;
                    break;
                case EmotionType.Hearts:
                    SpawnHeartParticles();
                    _particleTimer = 0.8;
                    break;
                case EmotionType.Anger:
                    SpawnAnger();
                    _particleTimer = 0.55;
                    break;
                case EmotionType.Sweat:
                    SpawnSweat();
                    _particleTimer = 0.85;
                    break;
                case EmotionType.Music:
                    SpawnMusic();
                    _particleTimer = 0.45;
                    break;
                case EmotionType.Blush:
                    SpawnHeartParticles();
                    _particleTimer = 1.0;
                    break;
            }
        }
    }

    public void SpawnMiningSparks(bool isWood)
    {
        double now = _clock.Elapsed.TotalSeconds;
        if (now - _lastSparkTime < 0.22) return;
        _lastSparkTime = now;

        string[] woodIcons = { "🪵", "🟫", "▫️" };
        string[] oreIcons = { "✨", "◽", "▫️" };
        string icon = isWood ? woodIcons[_rng.Next(woodIcons.Length)] : oreIcons[_rng.Next(oreIcons.Length)];

        var elem = new TextBlock
        {
            Text = icon,
            FontSize = 13,
            IsHitTestVisible = false
        };

        double bx = WinW / 2.0 + (_pet.Facing > 0 ? 35 : -35);
        double by = WinH / 2.0 + 15;
        Canvas.SetLeft(elem, bx + _rng.Next(-8, 8));
        Canvas.SetTop(elem, by + _rng.Next(-5, 5));
        EmotionCanvas.Children.Add(elem);

        double dx = (_pet.Facing > 0 ? -1 : 1) * _rng.Next(15, 30);
        double dy = -_rng.Next(20, 38);
        AnimateFloatingElement(elem, dx, dy, 0.6);
    }

    public void SpawnBlockBreakDebris(bool isWood)
    {
        string[] woodIcons = { "🪵", "🟫", "▫️", "▪️" };
        string[] oreIcons = { "✨", "💎", "◽", "▫️", "🪙" };
        string[] pool = isWood ? woodIcons : oreIcons;

        double bx = WinW / 2.0 + (_pet.Facing > 0 ? 30 : -30);
        double by = WinH / 2.0 + 15;

        for (int i = 0; i < 7; i++)
        {
            var elem = new TextBlock
            {
                Text = pool[_rng.Next(pool.Length)],
                FontSize = _rng.Next(11, 16),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(elem, bx + _rng.Next(-10, 10));
            Canvas.SetTop(elem, by + _rng.Next(-8, 8));
            EmotionCanvas.Children.Add(elem);

            double angle = _rng.NextDouble() * Math.PI * 2.0;
            double dist = _rng.Next(25, 55);
            double dx = Math.Cos(angle) * dist;
            double dy = Math.Sin(angle) * dist - 22; // импульс вверх
            AnimateFloatingElement(elem, dx, dy, 0.75);
        }
    }

    public void SpawnWaterSplashes()
    {
        for (int i = 0; i < 3; i++)
        {
            var drop = new TextBlock
            {
                Text = i == 0 ? "💦" : "💧",
                FontSize = 12,
                IsHitTestVisible = false
            };

            double fx = WinW / 2.0 + (_pet.Facing > 0 ? 25 : -25);
            double fy = WinH / 2.0 + 25;
            Canvas.SetLeft(drop, fx + (i - 1) * 12);
            Canvas.SetTop(drop, fy);
            EmotionCanvas.Children.Add(drop);

            AnimateFloatingElement(drop, (i - 1) * 10, -_rng.Next(20, 40), 0.7);
        }
    }

    public void SpawnHeartParticles()
    {
        for (int i = 0; i < 3; i++)
        {
            var heart = new TextBlock
            {
                Text = "❤️",
                FontSize = 16,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(heart, WinW / 2.0 - 8 + (i - 1) * 16);
            Canvas.SetTop(heart, WinH / 2.0 - 30);
            EmotionCanvas.Children.Add(heart);

            AnimateFloatingElement(heart, 0, -40, 1.2);
        }
    }

    void SpawnZzz()
    {
        var z = new TextBlock
        {
            Text = "Z",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 180, 255)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(z, WinW / 2.0 + 10);
        Canvas.SetTop(z, WinH / 2.0 - 15);
        EmotionCanvas.Children.Add(z);

        AnimateFloatingElement(z, 20, -50, 1.8);
    }

    void SpawnTears()
    {
        var tear = new TextBlock
        {
            Text = "💧",
            FontSize = 12,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(tear, WinW / 2.0 - 6 + (_yaw > 0 ? 10 : -10));
        Canvas.SetTop(tear, WinH / 2.0 - 30);
        EmotionCanvas.Children.Add(tear);

        AnimateFloatingElement(tear, 0, 30, 0.7);
    }

    void SpawnAnger()
    {
        var mark = new TextBlock
        {
            Text = "💢",
            FontSize = 16,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(mark, WinW / 2.0 + (_rng.NextDouble() * 20 - 10));
        Canvas.SetTop(mark, WinH / 2.0 - 45);
        EmotionCanvas.Children.Add(mark);
        AnimateFloatingElement(mark, 0, -25, 0.9);
    }

    void SpawnSweat()
    {
        var drop = new TextBlock
        {
            Text = "💦",
            FontSize = 14,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(drop, WinW / 2.0 + 15);
        Canvas.SetTop(drop, WinH / 2.0 - 35);
        EmotionCanvas.Children.Add(drop);
        AnimateFloatingElement(drop, 5, 20, 0.8);
    }

    void SpawnMusic()
    {
        string[] notes = { "🎵", "🎶", "🎼" };
        var note = new TextBlock
        {
            Text = notes[_rng.Next(notes.Length)],
            FontSize = 15,
            IsHitTestVisible = false
        };
        double side = _rng.NextDouble() < 0.5 ? -1 : 1;
        Canvas.SetLeft(note, WinW / 2.0 + side * 14);
        Canvas.SetTop(note, WinH / 2.0 - 35);
        EmotionCanvas.Children.Add(note);
        AnimateFloatingElement(note, side * 18, -45, 1.4);
    }

    public void ShowSpeechBubble(string text, string icon = "💬", double duration = 4.5)
    {
        Dispatcher.Invoke(() =>
        {
            for (int i = EmotionCanvas.Children.Count - 1; i >= 0; i--)
            {
                if (EmotionCanvas.Children[i] is FrameworkElement fe && fe.Tag as string == "speech_bubble")
                {
                    EmotionCanvas.Children.RemoveAt(i);
                }
            }

            var bubble = new Border
            {
                Tag = "speech_bubble",
                Background = new SolidColorBrush(Color.FromArgb(242, 20, 20, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 95, 95, 140)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 7, 14, 7),
                MaxWidth = 280,
                Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.65 }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconBlock = new TextBlock
            {
                Text = icon,
                FontSize = 14,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBlock, 0);
            grid.Children.Add(iconBlock);

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textBlock, 1);
            grid.Children.Add(textBlock);

            bubble.Child = grid;

            bubble.Measure(new Size(280, 250));
            double bw = bubble.DesiredSize.Width;
            double bh = bubble.DesiredSize.Height;

            bool isNearCeiling = _pet.Y < _pet.Bounds.Top + 220;
            Canvas.SetLeft(bubble, Math.Max(8, (WinW - bw) / 2.0));
            Canvas.SetTop(bubble, isNearCeiling ? (WinH / 2.0 + 35) : Math.Max(8, (WinH / 2.0) - 52 - bh));

            EmotionCanvas.Children.Add(bubble);
            AnimateBubble(bubble, duration);
        });
    }

    void AnimateBubble(UIElement elem, double durationSec)
    {
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
        elem.BeginAnimation(OpacityProperty, fadeIn);

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.4))
        {
            BeginTime = TimeSpan.FromSeconds(durationSec - 0.4)
        };
        fadeOut.Completed += (_, _) => EmotionCanvas.Children.Remove(elem);
        elem.BeginAnimation(OpacityProperty, fadeOut);
    }

    void AnimateFloatingElement(UIElement elem, double dx, double dy, double durationSec)
    {
        var daY = new DoubleAnimation
        {
            By = dy,
            Duration = TimeSpan.FromSeconds(durationSec),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var daOpacity = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromSeconds(durationSec)
        };

        daOpacity.Completed += (_, _) => EmotionCanvas.Children.Remove(elem);

        elem.BeginAnimation(Canvas.TopProperty, daY);
        elem.BeginAnimation(OpacityProperty, daOpacity);
    }

    public void CenterChibi()
    {
        var area = SystemParameters.WorkArea;
        _pet.X = area.Left + area.Width / 2.0;
        _pet.Y = area.Bottom;
        PlaceWindow();
        TriggerJoy();
        EnforceTopmost();
    }

    // ---------- Обработчики меню ----------

    void SetScale_Small(object sender, RoutedEventArgs e) => UpdateScale(4.0);
    void SetScale_Normal(object sender, RoutedEventArgs e) => UpdateScale(5.5);
    void SetScale_Large(object sender, RoutedEventArgs e) => UpdateScale(7.0);
    void SetScale_Huge(object sender, RoutedEventArgs e) => UpdateScale(8.5);

    void SetSpeed_Slow(object sender, RoutedEventArgs e) => UpdateWalkSpeed(30);
    void SetSpeed_Normal(object sender, RoutedEventArgs e) => UpdateWalkSpeed(50);
    void SetSpeed_Fast(object sender, RoutedEventArgs e) => UpdateWalkSpeed(80);

    void SetStyle_Classic(object sender, RoutedEventArgs e) => UpdateStyle(ModelStyle.ClassicMinecraft);
    void SetStyle_Chibi(object sender, RoutedEventArgs e) => UpdateStyle(ModelStyle.Chibi);

    void ToggleSleepInGames_Click(object sender, RoutedEventArgs e) => UpdateOptions(MenuSleepGames.IsChecked, _settings.Topmost);
    void ToggleTopmost_Click(object sender, RoutedEventArgs e) => UpdateOptions(_settings.SleepInGames, MenuTopmost.IsChecked);
    void ToggleAutoRun_Click(object sender, RoutedEventArgs e)
    {
        _settings.AutoRun = MenuAutoRun.IsChecked;
        _settings.Save();
    }

    void CenterChibi_Click(object sender, RoutedEventArgs e) => CenterChibi();
    void ChooseSkin_Click(object sender, RoutedEventArgs e) => ChooseSkin();
    void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
