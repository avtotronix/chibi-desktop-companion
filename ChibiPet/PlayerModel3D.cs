using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ChibiPet;

/// <summary>Поза персонажа. Все углы в градусах.</summary>
public struct Pose
{
    public double ArmRSwing, ArmLSwing, ArmSpread;
    public double LegRSwing, LegLSwing, LegSpread;
    public double HeadYaw, HeadPitch, HeadRoll;
    public double BodyPitch, BodyRoll;
    public double XOffset, YOffset;
}

/// <summary>
/// 3D-модель персонажа Minecraft.
/// Создает сплошную базовую модель тела, объёмный второй слой (шляпы, волосы, одежда),
/// слот для инструментов в руке (топор, кирка, удочка) и отдельную тень на полу.
/// </summary>
public sealed class PlayerModel3D
{
    public Model3DGroup Root { get; } = new();
    readonly Model3DGroup _bodyGroup = new();
    readonly Model3DGroup _heldItemSlot = new();
    readonly Model3DGroup _worldBlockSlot = new();
    readonly Model3DGroup _breastGroup = new();
    readonly GeometryModel3D _shadow;

    readonly TranslateTransform3D _breastBounceY = new(0, 0, 0);
    readonly AxisAngleRotation3D _breastBouncePitch = new(new Vector3D(1, 0, 0), 0);

    readonly AxisAngleRotation3D _bodyYaw = new(new Vector3D(0, 1, 0), 0);
    readonly AxisAngleRotation3D _bodyPitch = new(new Vector3D(1, 0, 0), 0);
    readonly AxisAngleRotation3D _bodyRoll = new(new Vector3D(0, 0, 1), 0);
    readonly TranslateTransform3D _bodyTranslate = new(0, 0, 0);

    readonly AxisAngleRotation3D _headYaw = new(new Vector3D(0, 1, 0), 0);
    readonly AxisAngleRotation3D _headPitch = new(new Vector3D(1, 0, 0), 0);
    readonly AxisAngleRotation3D _headRoll = new(new Vector3D(0, 0, 1), 0);

    readonly Limb _armR = new(), _armL = new(), _legR = new(), _legL = new();

    HeldToolType _heldTool = HeldToolType.None;

    sealed class Limb
    {
        public readonly AxisAngleRotation3D Swing = new(new Vector3D(1, 0, 0), 0);
        public readonly AxisAngleRotation3D Spread = new(new Vector3D(0, 0, 1), 0);

        public Transform3D Build(Vector3D pivot)
        {
            var g = new Transform3DGroup();
            g.Children.Add(new RotateTransform3D(Swing));
            g.Children.Add(new RotateTransform3D(Spread));
            g.Children.Add(new TranslateTransform3D(pivot));
            return g;
        }
    }

    public double Yaw
    {
        get => _bodyYaw.Angle;
        set => _bodyYaw.Angle = value;
    }

    public HeldToolType HeldTool
    {
        get => _heldTool;
        set
        {
            _heldTool = value;
            _heldItemSlot.Children.Clear();
            if (value == HeldToolType.NetheriteAxe)
                _heldItemSlot.Children.Add(MinecraftVoxelItems.CreateNetheriteAxe());
            else if (value == HeldToolType.NetheritePickaxe)
                _heldItemSlot.Children.Add(MinecraftVoxelItems.CreateNetheritePickaxe());
            else if (value == HeldToolType.FishingRod)
                _heldItemSlot.Children.Add(MinecraftVoxelItems.CreateFishingRod());
        }
    }

    public bool ShowShadow
    {
        get => _shadow.Material != null;
        set
        {
            if (value)
            {
                var brush = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
                brush.Freeze();
                _shadow.Material = new DiffuseMaterial(brush);
            }
            else
            {
                _shadow.Material = null;
            }
        }
    }

    public void SetBreastBounce(double y, double pitch)
    {
        _breastBounceY.OffsetY = y;
        _breastBouncePitch.Angle = pitch;
    }

    public void SetWorldBlock(Model3DGroup? block)
    {
        _worldBlockSlot.Children.Clear();
        if (block != null)
        {
            _worldBlockSlot.Children.Add(block);
        }
    }

    public void SetWorldBlockPosition(double x, double y, double z)
    {
        _worldBlockSlot.Transform = new TranslateTransform3D(x, y, z);
    }

    public void SetWorldBlockTransform(double x, double y, double z, double yaw = 0, double pitch = 0, double scale = 1.0)
    {
        var g = new Transform3DGroup();
        if (scale != 1.0) g.Children.Add(new ScaleTransform3D(scale, scale, scale));
        if (pitch != 0) g.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), pitch)));
        if (yaw != 0) g.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), yaw)));
        g.Children.Add(new TranslateTransform3D(x, y, z));
        _worldBlockSlot.Transform = g;
    }

    public void Apply(Pose p)
    {
        _bodyPitch.Angle = p.BodyPitch;
        _bodyRoll.Angle = p.BodyRoll;
        _bodyTranslate.OffsetX = p.XOffset;
        _bodyTranslate.OffsetY = p.YOffset;

        _headYaw.Angle = p.HeadYaw;
        _headPitch.Angle = p.HeadPitch;
        _headRoll.Angle = p.HeadRoll;

        _armR.Swing.Angle = p.ArmRSwing;
        _armL.Swing.Angle = p.ArmLSwing;
        _armR.Spread.Angle = -p.ArmSpread;
        _armL.Spread.Angle = p.ArmSpread;

        _legR.Swing.Angle = p.LegRSwing;
        _legL.Swing.Angle = p.LegLSwing;
        _legR.Spread.Angle = -p.LegSpread;
        _legL.Spread.Angle = p.LegSpread;
    }

    public PlayerModel3D(SkinData skin, ModelStyle style = ModelStyle.ClassicMinecraft, GenderConfig? gender = null)
    {
        var bodyTransformGroup = new Transform3DGroup();
        bodyTransformGroup.Children.Add(new RotateTransform3D(_bodyPitch));
        bodyTransformGroup.Children.Add(new RotateTransform3D(_bodyRoll));
        bodyTransformGroup.Children.Add(new RotateTransform3D(_bodyYaw));
        bodyTransformGroup.Children.Add(_bodyTranslate);
        _bodyGroup.Transform = bodyTransformGroup;

        // Тень на полу (не зависит от наклона тела персонажа)
        _shadow = MakeShadow(10, 6);
        Root.Children.Add(_shadow);
        Root.Children.Add(_bodyGroup);
        Root.Children.Add(_worldBlockSlot);

        var brush = MakeBrush(skin.Pixels);
        var material = new DiffuseMaterial(brush);

        var bases = new List<GeometryModel3D>();
        var overlays = new List<GeometryModel3D>();

        double headSize = 8;
        double torsoW = 8, torsoH = 12, torsoD = 4;
        double legW = 4, legH = 12, limbD = 4;
        double armTex = skin.Slim ? 3 : 4;
        double armH = 12;
        double shoulderDrop = 1.0;

        if (style == ModelStyle.Chibi)
        {
            headSize = 9.5;
            torsoH = 8;
            legH = 6;
            armH = 8;
        }

        void Part(Transform3D t, double w, double h, double d, double topY,
                  int u, int v, int ou, int ov, int tw, int th, int td, double inflate)
        {
            var b = MakeSolidBox(material, w, h, d, topY, u, v, tw, th, td);
            b.Transform = t;
            bases.Add(b);

            var o = MakeVoxelOverlay(material, skin.Pixels, w, h, d, topY, inflate, ou, ov, tw, th, td);
            if (o != null)
            {
                o.Transform = t;
                overlays.Add(o);
            }
        }

        double armX = torsoW / 2 + armTex / 2.0;
        double shoulderY = legH + torsoH - shoulderDrop;

        // Ноги
        Part(_legR.Build(new Vector3D(-legW / 2, legH, 0)), legW, legH, limbD, 0, 0, 16, 0, 32, 4, 12, 4, 0.25);
        Part(_legL.Build(new Vector3D(legW / 2, legH, 0)), legW, legH, limbD, 0, 16, 48, 0, 48, 4, 12, 4, 0.25);

        // Туловище
        Part(new TranslateTransform3D(0, legH, 0), torsoW, torsoH, torsoD, torsoH, 16, 16, 16, 32, 8, 12, 4, 0.25);

        // Женская моделька (грудь в стиле Wildfire's Female Gender Mod)
        if (gender != null && gender.Enabled)
        {
            double scale = 0.50 + 0.45 * Math.Clamp(gender.Size, 0.1, 1.2);
            // Реалистичная глубина в анатомических пропорциях Minecraft: 0.35..0.75 блока
            // Больше не торчит вперед как шест/клюв!
            double depth = Math.Clamp(gender.Depth, 0.5, 1.8) * (0.58 * scale);
            double yOff = gender.YOffset * 0.4;
            double cleavage = Math.Clamp(gender.Cleavage, 0.05, 0.5);

            double chestTopY = (style == ModelStyle.Chibi) ? (legH + torsoH - 1.2 + yOff) : (legH + torsoH - 2.0 + yOff);
            double chestH = 3.6 * scale;
            double chestBottomY = chestTopY - chestH;
            double chestCenterY = (chestTopY + chestBottomY) / 2.0;
            double zBase = torsoD / 2.0; // 2.0

            var breastTransformGroup = new Transform3DGroup();
            // ВАЖНО: точка вращения СТРОГО в центре груди на плоскости грудной клетки, 
            // а не у ступней (0,0,0), поэтому грудь больше НИКОГДА не вылетает вперед!
            breastTransformGroup.Children.Add(new RotateTransform3D(_breastBouncePitch, new Point3D(0, chestCenterY, zBase)));
            breastTransformGroup.Children.Add(_breastBounceY);
            _breastGroup.Transform = breastTransformGroup;

            // Левая половинка груди (X < 0)
            var leftBreast = MakeBreastMesh(
                material,
                -torsoW / 2.0 + 0.3, -cleavage,
                chestBottomY, chestTopY,
                zBase, zBase + depth,
                scale, isLeft: true);
            _breastGroup.Children.Add(leftBreast);

            // Правая половинка груди (X > 0)
            var rightBreast = MakeBreastMesh(
                material,
                torsoW / 2.0 - 0.3, cleavage,
                chestBottomY, chestTopY,
                zBase, zBase + depth,
                scale, isLeft: false);
            _breastGroup.Children.Add(rightBreast);

            // Оверлей (слой одежды / куртки / брони)
            if (HasChestOverlay(skin.Pixels))
            {
                var leftOverlay = MakeBreastMesh(
                    material,
                    -torsoW / 2.0 + 0.3, -cleavage,
                    chestBottomY, chestTopY,
                    zBase + 0.15, zBase + depth + 0.15,
                    scale, isLeft: true, isOverlay: true);
                _breastGroup.Children.Add(leftOverlay);

                var rightOverlay = MakeBreastMesh(
                    material,
                    torsoW / 2.0 - 0.3, cleavage,
                    chestBottomY, chestTopY,
                    zBase + 0.15, zBase + depth + 0.15,
                    scale, isLeft: false, isOverlay: true);
                _breastGroup.Children.Add(rightOverlay);
            }

            _bodyGroup.Children.Add(_breastGroup);
        }

        // Руки
        var armRTransform = _armR.Build(new Vector3D(-armX, shoulderY, 0));
        Part(armRTransform, armTex, armH, limbD, shoulderDrop, 40, 16, 40, 32, (int)armTex, 12, 4, 0.25);
        Part(_armL.Build(new Vector3D(armX, shoulderY, 0)), armTex, armH, limbD, shoulderDrop, 32, 48, 48, 48, (int)armTex, 12, 4, 0.25);

        // Слот для инструмента в правой руке (находится строго в кулаке персонажа)
        double handY = shoulderDrop - armH + 1.2;
        var itemTransform = new Transform3DGroup();
        itemTransform.Children.Add(new TranslateTransform3D(0, handY, 0));
        itemTransform.Children.Add(armRTransform);
        _heldItemSlot.Transform = itemTransform;
        _bodyGroup.Children.Add(_heldItemSlot);

        // Голова
        var head = new Transform3DGroup();
        head.Children.Add(new RotateTransform3D(_headRoll));
        head.Children.Add(new RotateTransform3D(_headPitch));
        head.Children.Add(new RotateTransform3D(_headYaw));
        head.Children.Add(new TranslateTransform3D(0, legH + torsoH, 0));
        Part(head, headSize, headSize, headSize, headSize, 0, 0, 32, 0, 8, 8, 8, 0.4);

        foreach (var m in bases) _bodyGroup.Children.Add(m);
        foreach (var m in overlays) _bodyGroup.Children.Add(m);
    }

    static GeometryModel3D MakeShadow(double radiusX, double radiusZ)
    {
        var mesh = new MeshGeometry3D();
        const int segments = 16;
        mesh.Positions.Add(new Point3D(0, 0.05, 0));

        for (int i = 0; i <= segments; i++)
        {
            double angle = i * 2 * Math.PI / segments;
            mesh.Positions.Add(new Point3D(Math.Cos(angle) * radiusX, 0.05, Math.Sin(angle) * radiusZ));
        }

        for (int i = 1; i <= segments; i++)
        {
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(i + 1);
            mesh.TriangleIndices.Add(i);
        }

        var brush = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        brush.Freeze();
        return new GeometryModel3D(mesh, new DiffuseMaterial(brush));
    }

    static ImageBrush MakeBrush(byte[] px)
    {
        const int S = 8, N = 64 * S;
        var big = new byte[N * N * 4];
        for (int y = 0; y < 64; y++)
        {
            int row = y * S * N * 4;
            for (int x = 0; x < 64; x++)
                for (int k = 0; k < S; k++)
                    Buffer.BlockCopy(px, (y * 64 + x) * 4, big, row + (x * S + k) * 4, 4);
            for (int k = 1; k < S; k++)
                Buffer.BlockCopy(big, row, big, row + k * N * 4, N * 4);
        }
        var bmp = BitmapSource.Create(N, N, 96, 96, PixelFormats.Bgra32, null, big, N * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp)
        {
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 1, 1)
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);
        brush.Freeze();
        return brush;
    }

    static GeometryModel3D MakeSolidBox(Material mat, double w, double h, double d, double topY,
                                       int u, int v, int tw, int th, int td)
    {
        double hx = w / 2.0, hz = d / 2.0;
        double y1 = topY, y0 = topY - h;
        var m = new MeshGeometry3D();

        Face(m, new(-hx, y1, hz),  new(hx, y1, hz),  new(hx, y0, hz),  new(-hx, y0, hz),  u + td,          v + td, tw, th); // перед
        Face(m, new(hx, y1, -hz),  new(-hx, y1, -hz), new(-hx, y0, -hz), new(hx, y0, -hz), u + 2 * td + tw, v + td, tw, th); // зад
        Face(m, new(-hx, y1, -hz), new(-hx, y1, hz), new(-hx, y0, hz), new(-hx, y0, -hz), u,               v + td, td, th); // право (-X)
        Face(m, new(hx, y1, hz),   new(hx, y1, -hz), new(hx, y0, -hz), new(hx, y0, hz),   u + td + tw,     v + td, td, th); // лево (+X)
        Face(m, new(-hx, y1, -hz), new(hx, y1, -hz), new(hx, y1, hz), new(-hx, y1, hz),   u + td,          v,      tw, td); // верх
        Face(m, new(-hx, y0, hz),  new(hx, y0, hz),  new(hx, y0, -hz), new(-hx, y0, -hz), u + td + tw,     v,      tw, td); // низ

        return new GeometryModel3D(m, mat) { BackMaterial = mat };
    }

    static GeometryModel3D? MakeVoxelOverlay(Material mat, byte[] skinPx, double w, double h, double d, double topY, double inf,
                                           int ou, int ov, int tw, int th, int td)
    {
        double hx = w / 2.0 + inf, hz = d / 2.0 + inf;
        double y1 = topY + inf, y0 = topY - h - inf;
        var m = new MeshGeometry3D();

        bool HasPixel(int x, int y)
        {
            if (x < 0 || x >= 64 || y < 0 || y >= 64) return false;
            return skinPx[(y * 64 + x) * 4 + 3] >= 128;
        }

        // Передняя грань (+Z)
        GenerateVoxelFace(m, HasPixel, ou + td, ov + td, tw, th,
            (ix, iy) => new Point3D(-hx + ix * (2 * hx / tw), y1 - iy * ((y1 - y0) / th), hz),
            (ix, iy) => new Point3D(-hx + (ix + 1) * (2 * hx / tw), y1 - iy * ((y1 - y0) / th), hz),
            (ix, iy) => new Point3D(-hx + (ix + 1) * (2 * hx / tw), y1 - (iy + 1) * ((y1 - y0) / th), hz),
            (ix, iy) => new Point3D(-hx + ix * (2 * hx / tw), y1 - (iy + 1) * ((y1 - y0) / th), hz));

        // Задняя грань (-Z)
        GenerateVoxelFace(m, HasPixel, ou + 2 * td + tw, ov + td, tw, th,
            (ix, iy) => new Point3D(hx - ix * (2 * hx / tw), y1 - iy * ((y1 - y0) / th), -hz),
            (ix, iy) => new Point3D(hx - (ix + 1) * (2 * hx / tw), y1 - iy * ((y1 - y0) / th), -hz),
            (ix, iy) => new Point3D(hx - (ix + 1) * (2 * hx / tw), y1 - (iy + 1) * ((y1 - y0) / th), -hz),
            (ix, iy) => new Point3D(hx - ix * (2 * hx / tw), y1 - (iy + 1) * ((y1 - y0) / th), -hz));

        // Правая грань (-X)
        GenerateVoxelFace(m, HasPixel, ou, ov + td, td, th,
            (ix, iy) => new Point3D(-hx, y1 - iy * ((y1 - y0) / th), -hz + ix * (2 * hz / td)),
            (ix, iy) => new Point3D(-hx, y1 - iy * ((y1 - y0) / th), -hz + (ix + 1) * (2 * hz / td)),
            (ix, iy) => new Point3D(-hx, y1 - (iy + 1) * ((y1 - y0) / th), -hz + (ix + 1) * (2 * hz / td)),
            (ix, iy) => new Point3D(-hx, y1 - (iy + 1) * ((y1 - y0) / th), -hz + ix * (2 * hz / td)));

        // Левая грань (+X)
        GenerateVoxelFace(m, HasPixel, ou + td + tw, ov + td, td, th,
            (ix, iy) => new Point3D(hx, y1 - iy * ((y1 - y0) / th), hz - ix * (2 * hz / td)),
            (ix, iy) => new Point3D(hx, y1 - iy * ((y1 - y0) / th), hz - (ix + 1) * (2 * hz / td)),
            (ix, iy) => new Point3D(hx, y1 - (iy + 1) * ((y1 - y0) / th), hz - (ix + 1) * (2 * hz / td)),
            (ix, iy) => new Point3D(hx, y1 - (iy + 1) * ((y1 - y0) / th), hz - ix * (2 * hz / td)));

        // Верхняя грань (+Y)
        GenerateVoxelFace(m, HasPixel, ou + td, ov, tw, td,
            (ix, iy) => new Point3D(-hx + ix * (2 * hx / tw), y1, -hz + iy * (2 * hz / td)),
            (ix, iy) => new Point3D(-hx + (ix + 1) * (2 * hx / tw), y1, -hz + iy * (2 * hz / td)),
            (ix, iy) => new Point3D(-hx + (ix + 1) * (2 * hx / tw), y1, -hz + (iy + 1) * (2 * hz / td)),
            (ix, iy) => new Point3D(-hx + ix * (2 * hx / tw), y1, -hz + (iy + 1) * (2 * hz / td)));

        // Нижняя грань (-Y)
        GenerateVoxelFace(m, HasPixel, ou + td + tw, ov, tw, td,
            (ix, iy) => new Point3D(-hx + ix * (2 * hx / tw), y0, hz - iy * (2 * hz / td)),
            (ix, iy) => new Point3D(-hx + (ix + 1) * (2 * hx / tw), y0, hz - iy * (2 * hz / td)),
            (ix, iy) => new Point3D(-hx + (ix + 1) * (2 * hx / tw), y0, hz - (iy + 1) * (2 * hz / td)),
            (ix, iy) => new Point3D(-hx + ix * (2 * hx / tw), y0, hz - (iy + 1) * (2 * hz / td)));

        if (m.Positions.Count == 0) return null;
        return new GeometryModel3D(m, mat) { BackMaterial = mat };
    }

    static void GenerateVoxelFace(MeshGeometry3D m, Func<int, int, bool> hasPixel,
                                  int u, int v, int countX, int countY,
                                  Func<int, int, Point3D> getTl,
                                  Func<int, int, Point3D> getTr,
                                  Func<int, int, Point3D> getBr,
                                  Func<int, int, Point3D> getBl)
    {
        for (int iy = 0; iy < countY; iy++)
        {
            for (int ix = 0; ix < countX; ix++)
            {
                int px = u + ix;
                int py = v + iy;
                if (!hasPixel(px, py)) continue;

                var tl = getTl(ix, iy);
                var tr = getTr(ix, iy);
                var br = getBr(ix, iy);
                var bl = getBl(ix, iy);

                Face(m, tl, tr, br, bl, px, py, 1, 1);
            }
        }
    }

    static void Face(MeshGeometry3D m, Point3D tl, Point3D tr, Point3D br, Point3D bl,
                     double px, double py, double pw, double ph)
    {
        int i = m.Positions.Count;
        m.Positions.Add(tl); m.Positions.Add(tr); m.Positions.Add(br); m.Positions.Add(bl);
        m.TextureCoordinates.Add(new Point(px / 64.0, py / 64.0));
        m.TextureCoordinates.Add(new Point((px + pw) / 64.0, py / 64.0));
        m.TextureCoordinates.Add(new Point((px + pw) / 64.0, (py + ph) / 64.0));
        m.TextureCoordinates.Add(new Point(px / 64.0, (py + ph) / 64.0));

        m.TriangleIndices.Add(i);     m.TriangleIndices.Add(i + 3); m.TriangleIndices.Add(i + 2);
        m.TriangleIndices.Add(i);     m.TriangleIndices.Add(i + 2); m.TriangleIndices.Add(i + 1);
    }

    static bool HasChestOverlay(byte[] skinPx)
    {
        for (int y = 37; y <= 41; y++)
        {
            for (int x = 20; x <= 27; x++)
            {
                if (skinPx[(y * 64 + x) * 4 + 3] >= 128) return true;
            }
        }
        return false;
    }

    static GeometryModel3D MakeBreastMesh(
        Material mat,
        double xOuter, double xInner,
        double yBottom, double yTop,
        double zBase, double zFront,
        double scale,
        bool isLeft,
        bool isOverlay = false)
    {
        var m = new MeshGeometry3D();

        // Естественные анатомические пропорции каплевидной формы:
        // Апекс (вершина) находится чуть ниже центра груди (44% от низа)
        double yApex = yBottom + (yTop - yBottom) * 0.44;
        double yMidUp = (yTop + yApex) * 0.52;
        double yMidDown = (yBottom + yApex) * 0.48;

        // По горизонтали апекс слегка смещен к центру (как в реальной анатомии)
        double xApex = (xOuter + xInner) * 0.5 + (isLeft ? (0.25 * scale) : (-0.25 * scale));

        double depth = zFront - zBase;

        // UV базовые координаты скина Майнкрафт:
        double uOuter = isLeft ? 20.0 : 28.0;
        double uMid = isLeft ? 22.0 : 26.0;
        double uInner = isLeft ? 24.0 : 24.0;

        double vTop = isOverlay ? 36.0 : 20.0;
        double vMidUp = isOverlay ? 37.5 : 21.5;
        double vApex = isOverlay ? 39.0 : 23.0;
        double vMidDown = isOverlay ? 40.5 : 24.5;
        double vBot = isOverlay ? 42.0 : 26.0;

        void Tri(Point3D p0, Point3D p1, Point3D p2, Point uv0, Point uv1, Point uv2)
        {
            int idx = m.Positions.Count;
            m.Positions.Add(p0); m.Positions.Add(p1); m.Positions.Add(p2);
            m.TextureCoordinates.Add(new Point(uv0.X / 64.0, uv0.Y / 64.0));
            m.TextureCoordinates.Add(new Point(uv1.X / 64.0, uv1.Y / 64.0));
            m.TextureCoordinates.Add(new Point(uv2.X / 64.0, uv2.Y / 64.0));
            m.TriangleIndices.Add(idx);
            m.TriangleIndices.Add(idx + 1);
            m.TriangleIndices.Add(idx + 2);
        }

        void Quad(Point3D p0, Point3D p1, Point3D p2, Point3D p3, Point uv0, Point uv1, Point uv2, Point uv3)
        {
            Tri(p0, p1, p2, uv0, uv1, uv2);
            Tri(p0, p2, p3, uv0, uv2, uv3);
        }

        // --- 1. Сетка точек поверхности груди ---
        // Верхний ряд (основание у ключиц, z = zBase)
        Point3D p0_out = new(xOuter, yTop, zBase);
        Point3D p0_mid = new(xApex, yTop, zBase);
        Point3D p0_in  = new(xInner, yTop, zBase);

        // Верхний скат (мягкий склон вниз к апексу)
        Point3D p1_out = new(xOuter + (isLeft ? 0.15 : -0.15) * scale, yMidUp, zBase + depth * 0.35);
        Point3D p1_mid = new(xApex, yMidUp, zBase + depth * 0.75);
        Point3D p1_in  = new(xInner - (isLeft ? 0.08 : -0.08) * scale, yMidUp, zBase + depth * 0.30);

        // Центр груди (Апекс / максимальный выступ)
        Point3D p2_out = new(xOuter + (isLeft ? 0.25 : -0.25) * scale, yApex, zBase + depth * 0.45);
        Point3D p2_mid = new(xApex, yApex, zBase + depth);
        Point3D p2_in  = new(xInner, yApex, zBase + depth * 0.35);

        // Нижний скат (подгрудная складка, плавное скругление к ребрам)
        Point3D p3_out = new(xOuter + (isLeft ? 0.15 : -0.15) * scale, yMidDown, zBase + depth * 0.40);
        Point3D p3_mid = new(xApex, yMidDown, zBase + depth * 0.85);
        Point3D p3_in  = new(xInner, yMidDown, zBase + depth * 0.25);

        // Нижний ряд (основание у ребер, z = zBase)
        Point3D p4_out = new(xOuter, yBottom, zBase);
        Point3D p4_mid = new(xApex, yBottom, zBase);
        Point3D p4_in  = new(xInner, yBottom, zBase);

        // Точки UV для сетки:
        Point uv0_out = new(uOuter, vTop),    uv0_mid = new(uMid, vTop),    uv0_in = new(uInner, vTop);
        Point uv1_out = new(uOuter, vMidUp),  uv1_mid = new(uMid, vMidUp),  uv1_in = new(uInner, vMidUp);
        Point uv2_out = new(uOuter, vApex),   uv2_mid = new(uMid, vApex),   uv2_in = new(uInner, vApex);
        Point uv3_out = new(uOuter, vMidDown),uv3_mid = new(uMid, vMidDown),uv3_in = new(uInner, vMidDown);
        Point uv4_out = new(uOuter, vBot),    uv4_mid = new(uMid, vBot),    uv4_in = new(uInner, vBot);

        // --- 2. Построение выпуклой фасетной поверхности (Передний купол) ---
        Quad(p0_out, p0_mid, p1_mid, p1_out, uv0_out, uv0_mid, uv1_mid, uv1_out);
        Quad(p0_mid, p0_in,  p1_in,  p1_mid, uv0_mid, uv0_in,  uv1_in,  uv1_mid);

        Quad(p1_out, p1_mid, p2_mid, p2_out, uv1_out, uv1_mid, uv2_mid, uv2_out);
        Quad(p1_mid, p1_in,  p2_in,  p2_mid, uv1_mid, uv1_in,  uv2_in,  uv2_mid);

        Quad(p2_out, p2_mid, p3_mid, p3_out, uv2_out, uv2_mid, uv3_mid, uv3_out);
        Quad(p2_mid, p2_in,  p3_in,  p3_mid, uv2_mid, uv2_in,  uv3_in,  uv3_mid);

        Quad(p3_out, p3_mid, p4_mid, p4_out, uv3_out, uv3_mid, uv4_mid, uv4_out);
        Quad(p3_mid, p3_in,  p4_in,  p4_mid, uv3_mid, uv3_in,  uv4_in,  uv4_mid);

        // --- 3. Боковые стенки (герметичное соединение с туловищем без щелей) ---
        // Внешняя боковая грань (к подмышке/боку)
        Point3D b1_out = new(xOuter, yMidUp, zBase);
        Point3D b2_out = new(xOuter, yApex, zBase);
        Point3D b3_out = new(xOuter, yMidDown, zBase);

        double sideU = isLeft ? 16.0 : 28.0;
        Point uv_b0 = new(sideU, vTop),    uv_b1 = new(sideU, vMidUp);
        Point uv_b2 = new(sideU, vApex),   uv_b3 = new(sideU, vMidDown);
        Point uv_b4 = new(sideU, vBot);

        Tri(p0_out, p1_out, b1_out, uv0_out, uv1_out, uv_b1);
        Quad(b1_out, p1_out, p2_out, b2_out, uv_b1, uv1_out, uv2_out, uv_b2);
        Quad(b2_out, p2_out, p3_out, b3_out, uv_b2, uv2_out, uv3_out, uv_b3);
        Tri(b3_out, p3_out, p4_out, uv_b3, uv3_out, uv4_out);

        // Внутренняя грань декольте (к центру грудины)
        Point3D b1_in = new(xInner, yMidUp, zBase);
        Point3D b2_in = new(xInner, yApex, zBase);
        Point3D b3_in = new(xInner, yMidDown, zBase);

        Point uv_in0 = new(uInner, vTop),    uv_in1 = new(uInner, vMidUp);
        Point uv_in2 = new(uInner, vApex),   uv_in3 = new(uInner, vMidDown);
        Point uv_in4 = new(uInner, vBot);

        Tri(p0_in, b1_in, p1_in, uv0_in, uv_in1, uv1_in);
        Quad(b1_in, b2_in, p2_in, p1_in, uv_in1, uv_in2, uv2_in, uv1_in);
        Quad(b2_in, b3_in, p3_in, p2_in, uv_in2, uv_in3, uv3_in, uv2_in);
        Tri(b3_in, p4_in, p3_in, uv_in3, uv4_in, uv3_in);

        return new GeometryModel3D(m, mat) { BackMaterial = mat };
    }
}
