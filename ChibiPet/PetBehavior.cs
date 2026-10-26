using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace ChibiPet;

public enum PetState
{
    Idle,
    Walk,
    Dragged,
    Fall,
    Sleeping,
    Joy,
    Cry,
    Angry,
    Scared,
    Blush,
    Bored,
    Dance,
    CatchCursor,
    Mining,
    Fishing,
    Climbing,
    WallJump
}

public enum EmotionType
{
    None,
    Hearts,
    Tears,
    Zzz,
    Anger,
    Sweat,
    Music,
    Blush
}

/// <summary>
/// Логика поведения чибика, физика перемещения, эмоции и авто-реакции.
/// (X, Y) — координаты точки под ногами (центр ступней) в DIP.
/// </summary>
public sealed class PetBehavior
{
    const double Gravity = 2400;      // DIP/с²
    const double SideMargin = 40;

    readonly Random _rng = new();
    double _timer = 1, _vy, _vx;
    double _idleTime = 0;
    double _emotionTimer = 0;
    double _actionDuration = 0;
    bool _isChasingCursor = false;
    double _climbTopHoldTimer = 0;

    public PetState State { get; private set; } = PetState.Idle;
    public EmotionType Emotion { get; private set; } = EmotionType.None;
    public double X { get; set; }
    public double Y { get; set; }
    public double VY => _vy;
    public double Facing { get; private set; } = 1;   // +1 вправо, -1 влево
    public Rect Bounds { get; set; }
    public double WalkSpeed { get; set; } = 50;       // DIP/с

    public bool IsSleepingManually { get; set; }
    public bool AutoIdleEmotions { get; set; } = true;
    public bool AutoNightSleep { get; set; } = true;

    [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint p);
    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }

    public void Update(double dt)
    {
        double floor = Bounds.Bottom;

        if (_emotionTimer > 0)
        {
            _emotionTimer -= dt;
            if (_emotionTimer <= 0) Emotion = EmotionType.None;
        }

        if (IsSleepingManually && State != PetState.Sleeping && State != PetState.Dragged)
        {
            State = PetState.Sleeping;
            Emotion = EmotionType.Zzz;
            _emotionTimer = 999999;
        }

        switch (State)
        {
            case PetState.Idle:
                _idleTime += dt;
                if (Y < floor - 1.0) { StartFall(); break; }
                if (Y > floor) Y = floor;

                // Если пользователь поднимает курсор над головой чибика — начинаем ловить курсор
                if (AutoIdleEmotions && GetCursorPos(out var cpIdle))
                {
                    if (Math.Abs(cpIdle.X - X) <= 60 && cpIdle.Y < Y - 30 && cpIdle.Y > Y - 260)
                    {
                        TriggerCatchCursor();
                        break;
                    }
                }

                // Авто-реакции на простой:
                if (AutoIdleEmotions)
                {
                    // 10 минут простоя -> ложится спать
                    if (_idleTime >= 600)
                    {
                        TriggerSleep();
                        break;
                    }
                    // 5 минут простоя -> грустит / зевает
                    else if (_idleTime >= 300 && _idleTime - dt < 300)
                    {
                        TriggerBored();
                        break;
                    }
                }

                _timer -= dt;
                if (_timer <= 0) NextAction();
                break;

            case PetState.Walk:
                _idleTime = 0;
                X += Facing * WalkSpeed * dt;
                double minX = Bounds.Left + SideMargin, maxX = Bounds.Right - SideMargin;
                if (X <= minX)
                {
                    X = minX;
                    if (_rng.NextDouble() < 0.45) { StartClimbing(-1, isChasingCursor: false); break; }
                    Facing = 1;
                }
                else if (X >= maxX)
                {
                    X = maxX;
                    if (_rng.NextDouble() < 0.45) { StartClimbing(1, isChasingCursor: false); break; }
                    Facing = -1;
                }

                _timer -= dt;
                if (_timer <= 0) { State = PetState.Idle; _timer = Range(1.5, 4.5); }
                break;

            case PetState.Climbing:
                _idleTime = 0;
                double topLimit = Bounds.Top + 135; // Самый верх экрана (голова касается верхнего края)
                double wallX = Facing < 0 ? (Bounds.Left + SideMargin) : (Bounds.Right - SideMargin);
                X = wallX;

                bool cursorActive = GetCursorPos(out var cpClimb);
                if (cursorActive)
                {
                    // Проверяем поимку курсора на стене
                    bool hitX = Math.Abs(cpClimb.X - X) <= 48;
                    bool hitY = (cpClimb.Y >= Y - 180 && cpClimb.Y <= Y + 15);
                    if (hitX && hitY)
                    {
                        TriggerCatchSuccess();
                        break;
                    }

                    double distToWall = Math.Abs(cpClimb.X - wallX);
                    double cursorFeetY = Math.Clamp(cpClimb.Y + 80, topLimit, floor);

                    // Если чибик карабкается за курсором наверх или курсор находится рядом со стеной
                    if (_isChasingCursor || distToWall < 260 || cpClimb.Y < Y)
                    {
                        if (Y > cursorFeetY + 8)
                        {
                            // Курсор выше — карабкаемся наверх за курсором вплоть до самого верха экрана!
                            Y -= 150 * dt;
                            if (Y <= topLimit)
                            {
                                Y = topLimit;
                            }
                        }
                        else if (Y < cursorFeetY - 15 && Y < floor - 20)
                        {
                            // Курсор опустился ниже — спускаемся вниз к нему
                            Y += 135 * dt;
                        }

                        // Если курсор сместился в центр экрана на подходящей высоте — чибик эффектно прыгает со стены к курсору!
                        if (distToWall >= 140 && Math.Abs(Y - cursorFeetY) < 160)
                        {
                            StartWallJumpTowards(cpClimb.X, cpClimb.Y);
                            break;
                        }
                    }
                    else
                    {
                        // Обычное непрерывное карабканье на самый верх экрана
                        Y -= 140 * dt;
                    }
                }
                else
                {
                    // Обычное карабканье на самый верх экрана
                    Y -= 140 * dt;
                }

                // Достигли самого верха экрана!
                if (Y <= topLimit)
                {
                    Y = topLimit;
                    _climbTopHoldTimer -= dt;
                    if (_climbTopHoldTimer <= 0)
                    {
                        _climbTopHoldTimer = 1.0;
                        StartWallJump();
                    }
                }
                break;

            case PetState.WallJump:
                _idleTime = 0;
                _vy += Gravity * dt;
                Y += _vy * dt;
                X += _vx * dt;

                // Перехват курсора в полете при прыжке со стены!
                if (GetCursorPos(out var cpJump))
                {
                    bool hitX = Math.Abs(cpJump.X - X) <= 48;
                    bool hitY = (cpJump.Y >= Y - 180 && cpJump.Y <= Y + 15);
                    if (hitX && hitY)
                    {
                        TriggerCatchSuccess();
                        _vx = 0;
                        break;
                    }
                }

                if (Y >= floor)
                {
                    Y = floor;
                    _vy = 0;
                    _vx = 0;
                    TriggerClickJoy();
                }
                break;

            case PetState.CatchCursor:
                _idleTime = 0;
                if (GetCursorPos(out var cp))
                {
                    // Проверяем попадание курсора ТОЧНО в границы 3D модельки чибика
                    bool hitX = Math.Abs(cp.X - X) <= 44;
                    bool hitY = (cp.Y >= Y - 180 && cp.Y <= Y + 12);

                    if (hitX && hitY)
                    {
                        TriggerCatchSuccess();
                        break;
                    }

                    // Если курсор находится выше чибика в воздухе (над полом):
                    if (cp.Y < floor - 80)
                    {
                        // Чтобы догнать курсор наверху экрана, чибик бежит к стене и карабкается наверх!
                        double leftWallX = Bounds.Left + SideMargin;
                        double rightWallX = Bounds.Right - SideMargin;
                        double targetWallX = (cp.X < (Bounds.Left + Bounds.Right) / 2.0) ? leftWallX : rightWallX;
                        double wallDir = (targetWallX == leftWallX) ? -1 : 1;

                        if (Math.Abs(X - targetWallX) > 18)
                        {
                            Facing = targetWallX > X ? 1 : -1;
                            X += Facing * (WalkSpeed * 2.2) * dt;
                            X = Math.Clamp(X, leftWallX, rightWallX);
                        }
                        else
                        {
                            // Добежали до стены — начинаем карабкаться наверх за курсором!
                            X = targetWallX;
                            StartClimbing(wallDir, isChasingCursor: true);
                            break;
                        }
                    }
                    else
                    {
                        // Обычная погоня по полу
                        double dx = cp.X - X;
                        Facing = dx > 0 ? 1 : -1;
                        X += Facing * (WalkSpeed * 1.6) * dt;
                        X = Math.Clamp(X, Bounds.Left + SideMargin, Bounds.Right - SideMargin);
                    }
                }
                _actionDuration -= dt;
                if (_actionDuration <= 0) { State = PetState.Idle; _timer = 2; }
                break;

            case PetState.Fall:
                _idleTime = 0;
                _vy += Gravity * dt;
                Y += _vy * dt;
                if (Y >= floor)
                {
                    Y = floor;
                    _vy = 0;
                    State = PetState.Idle;
                    _timer = 1.0;
                }
                break;

            case PetState.Joy:
            case PetState.Cry:
            case PetState.Angry:
            case PetState.Scared:
            case PetState.Blush:
            case PetState.Bored:
            case PetState.Dance:
            case PetState.Mining:
            case PetState.Fishing:
                _timer -= dt;
                if (_timer <= 0)
                {
                    State = PetState.Idle;
                    _timer = Range(1.5, 3.5);
                }
                break;

            case PetState.Sleeping:
                if (!IsSleepingManually && _timer > 0)
                {
                    _timer -= dt;
                    if (_timer <= 0)
                    {
                        WakeUp();
                    }
                }
                break;
        }
    }

    public void TriggerEmotion(PetState state, EmotionType emotion, double duration = 3.5)
    {
        if (State == PetState.Dragged) return;
        IsSleepingManually = (state == PetState.Sleeping);
        State = state;
        Emotion = emotion;
        _emotionTimer = duration;
        _timer = duration;
        _actionDuration = duration;
    }

    public void TriggerClickJoy()
    {
        if (State == PetState.Dragged || State == PetState.Fall) return;
        TriggerEmotion(PetState.Joy, EmotionType.Hearts, 2.0);
    }

    public void TriggerCatchSuccess()
    {
        Emotion = EmotionType.Hearts;
        _emotionTimer = 3.0;
        if (Y < Bounds.Bottom - 30)
        {
            // Поймали в воздухе или на стене! Спрыгиваем с сальто
            StartWallJump();
        }
        else
        {
            TriggerClickJoy();
        }
    }

    public void TriggerCry() => TriggerEmotion(PetState.Cry, EmotionType.Tears, 3.5);
    public void TriggerAngry() => TriggerEmotion(PetState.Angry, EmotionType.Anger, 3.5);
    public void TriggerScared() => TriggerEmotion(PetState.Scared, EmotionType.Sweat, 3.0);
    public void TriggerBlush() => TriggerEmotion(PetState.Blush, EmotionType.Blush, 3.5);
    public void TriggerBored() => TriggerEmotion(PetState.Bored, EmotionType.Sweat, 4.0);
    public void TriggerSleep() => TriggerEmotion(PetState.Sleeping, EmotionType.Zzz, 999999);
    public void TriggerDance() => TriggerEmotion(PetState.Dance, EmotionType.Music, 5.0);
    public void TriggerMining(double duration = 5.0) => TriggerEmotion(PetState.Mining, EmotionType.None, duration);
    public void TriggerFishing(double duration = 25.0) => TriggerEmotion(PetState.Fishing, EmotionType.None, duration);
    public void TriggerCatchCursor() => TriggerEmotion(PetState.CatchCursor, EmotionType.Hearts, 12.0);

    public void StartClimbing(double dir, bool isChasingCursor = false)
    {
        Facing = dir;
        State = PetState.Climbing;
        _isChasingCursor = isChasingCursor;
        _climbTopHoldTimer = 1.0;
        _timer = 999;
    }

    public void StartWallJump()
    {
        State = PetState.WallJump;
        _vx = -Facing * 250;
        _vy = -160;
        Facing = -Facing;
    }

    public void StartWallJumpTowards(double targetX, double targetY)
    {
        State = PetState.WallJump;
        double dx = targetX - X;
        double sign = dx >= 0 ? 1 : -1;
        Facing = sign;
        _vx = sign * Math.Clamp(Math.Abs(dx) * 2.0, 180, 440);
        _vy = targetY < Y ? -280 : -140;
    }

    public void TriggerClimbWall()
    {
        if (State == PetState.Dragged) return;
        double minX = Bounds.Left + SideMargin, maxX = Bounds.Right - SideMargin;
        double midX = (Bounds.Left + Bounds.Right) / 2.0;
        if (X < midX)
        {
            X = minX;
            StartClimbing(-1, isChasingCursor: false);
        }
        else
        {
            X = maxX;
            StartClimbing(1, isChasingCursor: false);
        }
    }

    public void GoToCornerAndSleep()
    {
        X = Bounds.Right - 80;
        Y = Bounds.Bottom;
        TriggerSleep();
    }

    public void SetGameSleep(bool sleep)
    {
        if (sleep)
        {
            GoToCornerAndSleep();
        }
        else
        {
            WakeUp();
        }
    }

    public void WakeUp()
    {
        IsSleepingManually = false;
        State = PetState.Idle;
        Emotion = EmotionType.None;
        _emotionTimer = 0;
        _timer = Range(1, 3);
        _idleTime = 0;
    }

    public void ResetIdle()
    {
        _idleTime = 0;
    }

    public void BeginDrag()
    {
        State = PetState.Dragged;
        Emotion = EmotionType.Tears;
        _emotionTimer = 999999;
        _vy = 0;
        _idleTime = 0;
    }

    public void DragTo(double x, double y)
    {
        X = Math.Clamp(x, Bounds.Left, Bounds.Right);
        Y = Math.Clamp(y, Bounds.Top + 60, Bounds.Bottom);
        _idleTime = 0;
    }

    public void EndDrag()
    {
        if (State == PetState.Dragged)
        {
            Emotion = EmotionType.None;
            _emotionTimer = 0;
            StartFall();
        }
    }

    void StartFall()
    {
        State = PetState.Fall;
        _vy = 0;
    }

    void NextAction()
    {
        if (_rng.NextDouble() < 0.65)
        {
            State = PetState.Walk;
            Facing = _rng.NextDouble() < 0.5 ? 1 : -1;
            _timer = Range(2.5, 6.0);
        }
        else
        {
            _timer = Range(1.5, 4.0);
        }
    }

    double Range(double a, double b) => a + _rng.NextDouble() * (b - a);
}
