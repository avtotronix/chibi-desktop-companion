using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ChibiPet;

public enum HeldToolType { None, NetheriteAxe, NetheritePickaxe, FishingRod }

public enum TreeType
{
    Oak, Birch, Spruce, DarkOak, PaleOak, Jungle, Acacia, Mangrove, Cherry, Crimson, Warped
}

public enum OreType
{
    Coal, Iron, Copper, Gold, Lapis, Redstone, Diamond, Emerald, NetherQuartz, AncientDebris, NetherGold
}

public enum FishingLootType
{
    Cod, Salmon, TropicalFish, Pufferfish, EnchantedBook, TNT, Diamond
}

public static class MinecraftVoxelItems
{
    #region Вспомогательные 3D методы для текстурированных кубов и вокселей

    /// <summary>
    /// Создает 3D куб заданного размера с 16x16 пиксельной текстурой для боковых сторон и торцов (как в Minecraft)
    /// </summary>
    public static Model3DGroup CreateTexturedCube(BitmapSource sideBmp, BitmapSource topBmp, BitmapSource? bottomBmp = null, double size = 8.0)
    {
        var group = new Model3DGroup();
        bottomBmp ??= topBmp;

        double h = size / 2.0;

        // Создаем материалы с отключенной интерполяцией (NearestNeighbor) для идеального четкого Minecraft пиксель-арта
        var sideBrush = new ImageBrush(sideBmp);
        RenderOptions.SetBitmapScalingMode(sideBrush, BitmapScalingMode.NearestNeighbor);
        sideBrush.Freeze();
        var sideMat = new DiffuseMaterial(sideBrush);

        var topBrush = new ImageBrush(topBmp);
        RenderOptions.SetBitmapScalingMode(topBrush, BitmapScalingMode.NearestNeighbor);
        topBrush.Freeze();
        var topMat = new DiffuseMaterial(topBrush);

        var bottomBrush = new ImageBrush(bottomBmp);
        RenderOptions.SetBitmapScalingMode(bottomBrush, BitmapScalingMode.NearestNeighbor);
        bottomBrush.Freeze();
        var bottomMat = new DiffuseMaterial(bottomBrush);

        // 1. Боковые 4 грани
        var sideMesh = new MeshGeometry3D();
        void AddSideQuad(Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            int i = sideMesh.Positions.Count;
            sideMesh.Positions.Add(p0); sideMesh.TextureCoordinates.Add(new Point(0, 1));
            sideMesh.Positions.Add(p1); sideMesh.TextureCoordinates.Add(new Point(1, 1));
            sideMesh.Positions.Add(p2); sideMesh.TextureCoordinates.Add(new Point(1, 0));
            sideMesh.Positions.Add(p3); sideMesh.TextureCoordinates.Add(new Point(0, 0));

            sideMesh.TriangleIndices.Add(i);
            sideMesh.TriangleIndices.Add(i + 1);
            sideMesh.TriangleIndices.Add(i + 2);
            sideMesh.TriangleIndices.Add(i);
            sideMesh.TriangleIndices.Add(i + 2);
            sideMesh.TriangleIndices.Add(i + 3);
        }

        // Перед (+Z)
        AddSideQuad(new(-h, 0, h), new(h, 0, h), new(h, size, h), new(-h, size, h));
        // Зад (-Z)
        AddSideQuad(new(h, 0, -h), new(-h, 0, -h), new(-h, size, -h), new(h, size, -h));
        // Право (+X)
        AddSideQuad(new(h, 0, h), new(h, 0, -h), new(h, size, -h), new(h, size, h));
        // Лево (-X)
        AddSideQuad(new(-h, 0, -h), new(-h, 0, h), new(-h, size, h), new(-h, size, -h));

        group.Children.Add(new GeometryModel3D(sideMesh, sideMat) { BackMaterial = sideMat });

        // 2. Верхняя грань (+Y)
        var topMesh = new MeshGeometry3D();
        topMesh.Positions.Add(new(-h, size, h)); topMesh.TextureCoordinates.Add(new Point(0, 1));
        topMesh.Positions.Add(new(h, size, h));  topMesh.TextureCoordinates.Add(new Point(1, 1));
        topMesh.Positions.Add(new(h, size, -h)); topMesh.TextureCoordinates.Add(new Point(1, 0));
        topMesh.Positions.Add(new(-h, size, -h));topMesh.TextureCoordinates.Add(new Point(0, 0));
        topMesh.TriangleIndices.Add(0); topMesh.TriangleIndices.Add(1); topMesh.TriangleIndices.Add(2);
        topMesh.TriangleIndices.Add(0); topMesh.TriangleIndices.Add(2); topMesh.TriangleIndices.Add(3);
        group.Children.Add(new GeometryModel3D(topMesh, topMat) { BackMaterial = topMat });

        // 3. Нижняя грань (-Y)
        var botMesh = new MeshGeometry3D();
        botMesh.Positions.Add(new(-h, 0, -h)); botMesh.TextureCoordinates.Add(new Point(0, 1));
        botMesh.Positions.Add(new(h, 0, -h));  botMesh.TextureCoordinates.Add(new Point(1, 1));
        botMesh.Positions.Add(new(h, 0, h));   botMesh.TextureCoordinates.Add(new Point(1, 0));
        botMesh.Positions.Add(new(-h, 0, h));  botMesh.TextureCoordinates.Add(new Point(0, 0));
        botMesh.TriangleIndices.Add(0); botMesh.TriangleIndices.Add(1); botMesh.TriangleIndices.Add(2);
        botMesh.TriangleIndices.Add(0); botMesh.TriangleIndices.Add(2); botMesh.TriangleIndices.Add(3);
        group.Children.Add(new GeometryModel3D(botMesh, bottomMat) { BackMaterial = bottomMat });

        return group;
    }

    /// <summary>
    /// Создает 3D воксельную модель из 16x16 пиксельной сетки (настоящий 3D предмет с объемом и толщиной)
    /// </summary>
    public static Model3DGroup CreateExtrudedVoxelModel(string[] rows, Dictionary<char, Color> palette, double scale = 0.45, double depth = 0.35, double pivotX = 8, double pivotY = 8)
    {
        var group = new Model3DGroup();
        var colorMeshes = new Dictionary<Color, MeshGeometry3D>();

        int h = rows.Length;
        int w = rows[0].Length;

        for (int y = 0; y < h; y++)
        {
            string row = rows[y];
            for (int x = 0; x < w; x++)
            {
                char ch = row[x];
                if (ch == '.' || ch == ' ' || !palette.TryGetValue(ch, out var col))
                    continue;

                if (!colorMeshes.TryGetValue(col, out var mesh))
                {
                    mesh = new MeshGeometry3D();
                    colorMeshes[col] = mesh;
                }

                double cx = (x - pivotX) * scale;
                double cy = (pivotY - y) * scale;
                double cz = 0;
                double hx = scale / 2.0;
                double hy = scale / 2.0;
                double hz = depth / 2.0;

                void AddBoxQuad(Point3D p0, Point3D p1, Point3D p2, Point3D p3)
                {
                    int i = mesh.Positions.Count;
                    mesh.Positions.Add(p0); mesh.Positions.Add(p1); mesh.Positions.Add(p2); mesh.Positions.Add(p3);
                    mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2);
                    mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 3);
                }

                // Перед (+Z)
                AddBoxQuad(new(cx - hx, cy - hy, cz + hz), new(cx + hx, cy - hy, cz + hz), new(cx + hx, cy + hy, cz + hz), new(cx - hx, cy + hy, cz + hz));
                // Зад (-Z)
                AddBoxQuad(new(cx + hx, cy - hy, cz - hz), new(cx - hx, cy - hy, cz - hz), new(cx - hx, cy + hy, cz - hz), new(cx + hx, cy + hy, cz - hz));
                // Право (+X)
                AddBoxQuad(new(cx + hx, cy - hy, cz + hz), new(cx + hx, cy - hy, cz - hz), new(cx + hx, cy + hy, cz - hz), new(cx + hx, cy + hy, cz + hz));
                // Лево (-X)
                AddBoxQuad(new(cx - hx, cy - hy, cz - hz), new(cx - hx, cy - hy, cz + hz), new(cx - hx, cy + hy, cz + hz), new(cx - hx, cy + hy, cz - hz));
                // Верх (+Y)
                AddBoxQuad(new(cx - hx, cy + hy, cz + hz), new(cx + hx, cy + hy, cz + hz), new(cx + hx, cy + hy, cz - hz), new(cx - hx, cy + hy, cz - hz));
                // Низ (-Y)
                AddBoxQuad(new(cx - hx, cy - hy, cz - hz), new(cx + hx, cy - hy, cz - hz), new(cx + hx, cy - hy, cz + hz), new(cx - hx, cy - hy, cz + hz));
            }
        }

        foreach (var kvp in colorMeshes)
        {
            var brush = new SolidColorBrush(kvp.Key);
            brush.Freeze();
            var mat = new DiffuseMaterial(brush);
            group.Children.Add(new GeometryModel3D(kvp.Value, mat) { BackMaterial = mat });
        }

        return group;
    }

    /// <summary>
    /// Создает 16x16 BitmapSource из массива пикселей Bgra32
    /// </summary>
    static BitmapSource Make16x16Bitmap(uint[] pixels)
    {
        var bmp = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4);
        bmp.Freeze();
        return bmp;
    }

    static uint Argb(byte a, byte r, byte g, byte b) =>
        ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

    static uint Rgb(byte r, byte g, byte b) => Argb(255, r, g, b);

    #endregion

    #region Инструменты (Топор, Кирка, Удочка) по фотографиям 3, 4, 5

    /// <summary>
    /// Незеритовый топор (Netherite Axe) по фото 4: темная рукоятка, мощное незеритовое лезвие с бородкой и шипом
    /// </summary>
    public static Model3DGroup CreateNetheriteAxe()
    {
        string[] sprite = new[]
        {
            "................",
            ".......#bbbbb...",
            "......#aaaaabb#.",
            ".....#aaaaccca#.",
            ".....#aaaaaabb#.",
            ".....#aaaa#..##.",
            "....#aaaa#..h...",
            "....#aa##..h....",
            ".....##...h.....",
            ".........h......",
            "........h.......",
            ".......h........",
            "......h.........",
            ".....h..........",
            "....h...........",
            "................"
        };

        var palette = new Dictionary<char, Color>
        {
            ['h'] = Color.FromRgb(65, 42, 25),       // Деревянная рукоять
            ['#'] = Color.FromRgb(30, 24, 32),       // Темный контур незерита
            ['a'] = Color.FromRgb(49, 41, 54),       // Основной незеритовый металл
            ['b'] = Color.FromRgb(72, 61, 80),       // Верхнее ребро / лезвие
            ['c'] = Color.FromRgb(102, 88, 114)      // Светлый блик незерита
        };

        var model = CreateExtrudedVoxelModel(sprite, palette, scale: 0.42, depth: 0.32, pivotX: 4, pivotY: 13);
        
        // В Minecraft топор в руке направлен лезвием вперед и зажат в кулаке
        var transform = new Transform3DGroup();
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -90)));
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 20)));
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), -8)));
        model.Transform = transform;
        return model;
    }

    /// <summary>
    /// Незеритовая кирка (Netherite Pickaxe) по фото 5: классический изогнутый незеритовый клюв кирки
    /// </summary>
    public static Model3DGroup CreateNetheritePickaxe()
    {
        string[] sprite = new[]
        {
            "................",
            ".....#bbbbbba#..",
            "....#aaaacccba#.",
            "...#aab#....#ba#",
            "...#a#.......#a#",
            "..#a#...h.....##",
            "..##...h........",
            "......h.........",
            ".....h..........",
            "....h...........",
            "...h............",
            "..h.............",
            ".h..............",
            "h...............",
            "................",
            "................"
        };

        var palette = new Dictionary<char, Color>
        {
            ['h'] = Color.FromRgb(65, 42, 25),
            ['#'] = Color.FromRgb(30, 24, 32),
            ['a'] = Color.FromRgb(49, 41, 54),
            ['b'] = Color.FromRgb(72, 61, 80),
            ['c'] = Color.FromRgb(102, 88, 114)
        };

        var model = CreateExtrudedVoxelModel(sprite, palette, scale: 0.42, depth: 0.32, pivotX: 3, pivotY: 13);

        var transform = new Transform3DGroup();
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -90)));
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 20)));
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), -8)));
        model.Transform = transform;
        return model;
    }

    /// <summary>
    /// Удочка (Fishing Rod) по фото 3: деревянное удилище по диагонали, круглая катушка внизу и тонкая леска
    /// </summary>
    public static Model3DGroup CreateFishingRod()
    {
        string[] sprite = new[]
        {
            "...............#",
            "..............#|",
            ".............#.|",
            "............#..|",
            "...........#...|",
            "..........#....|",
            ".........#.....|",
            "........#......|",
            ".......#.......|",
            "......#........|",
            ".....#.sss.....|",
            "....#.srrrs....|",
            "...#..srrrs...|.",
            "..#....sss...|..",
            ".#..........|...",
            "#..........|...."
        };

        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(95, 58, 28),       // Деревянный бланк удочки
            ['s'] = Color.FromRgb(85, 85, 90),       // Металлический корпус катушки
            ['r'] = Color.FromRgb(170, 170, 175),   // Серебристая шпуля
            ['|'] = Color.FromRgb(205, 205, 210)    // Леска
        };

        var model = CreateExtrudedVoxelModel(sprite, palette, scale: 0.44, depth: 0.28, pivotX: 3, pivotY: 13);

        var transform = new Transform3DGroup();
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -90)));
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 20)));
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), -8)));
        model.Transform = transform;
        return model;
    }

    /// <summary>
    /// Создает 3D место рыбалки: водная гладь с кругами ряби и плавающий красно-белый поплавок (Bobber)
    /// </summary>
    public static Model3DGroup CreateFishingSpot(double bobberDip = 0.0, bool isSplash = false)
    {
        var group = new Model3DGroup();

        // 1. Пятно воды на полу
        var waterMesh = new MeshGeometry3D();
        double r = 4.8;
        int segs = 14;
        for (int i = 0; i < segs; i++)
        {
            double a0 = (i / (double)segs) * Math.PI * 2.0;
            double a1 = ((i + 1) / (double)segs) * Math.PI * 2.0;
            Point3D c = new(0, 0.05, 0);
            Point3D p0 = new(Math.Cos(a0) * r, 0.05, Math.Sin(a0) * r);
            Point3D p1 = new(Math.Cos(a1) * r, 0.05, Math.Sin(a1) * r);

            int baseIdx = waterMesh.Positions.Count;
            waterMesh.Positions.Add(c);
            waterMesh.Positions.Add(p0);
            waterMesh.Positions.Add(p1);
            waterMesh.TriangleIndices.Add(baseIdx);
            waterMesh.TriangleIndices.Add(baseIdx + 1);
            waterMesh.TriangleIndices.Add(baseIdx + 2);
        }
        var waterBrush = new SolidColorBrush(Color.FromArgb(190, 40, 130, 225));
        waterBrush.Freeze();
        var waterMat = new DiffuseMaterial(waterBrush);
        group.Children.Add(new GeometryModel3D(waterMesh, waterMat) { BackMaterial = waterMat });

        // Внутреннее светлое кольцо ряби
        var rippleMesh = new MeshGeometry3D();
        double rIn = 2.4, rOut = 2.8;
        for (int i = 0; i < segs; i++)
        {
            double a0 = (i / (double)segs) * Math.PI * 2.0;
            double a1 = ((i + 1) / (double)segs) * Math.PI * 2.0;
            Point3D p00 = new(Math.Cos(a0) * rIn, 0.07, Math.Sin(a0) * rIn);
            Point3D p01 = new(Math.Cos(a0) * rOut, 0.07, Math.Sin(a0) * rOut);
            Point3D p10 = new(Math.Cos(a1) * rIn, 0.07, Math.Sin(a1) * rIn);
            Point3D p11 = new(Math.Cos(a1) * rOut, 0.07, Math.Sin(a1) * rOut);

            int bi = rippleMesh.Positions.Count;
            rippleMesh.Positions.Add(p00); rippleMesh.Positions.Add(p01);
            rippleMesh.Positions.Add(p11); rippleMesh.Positions.Add(p10);
            rippleMesh.TriangleIndices.Add(bi); rippleMesh.TriangleIndices.Add(bi + 1); rippleMesh.TriangleIndices.Add(bi + 2);
            rippleMesh.TriangleIndices.Add(bi); rippleMesh.TriangleIndices.Add(bi + 2); rippleMesh.TriangleIndices.Add(bi + 3);
        }
        var ripBrush = new SolidColorBrush(Color.FromArgb(220, 140, 205, 255));
        ripBrush.Freeze();
        var ripMat = new DiffuseMaterial(ripBrush);
        group.Children.Add(new GeometryModel3D(rippleMesh, ripMat) { BackMaterial = ripMat });

        // 2. Красно-белый поплавок (Bobber)
        double by = 0.5 + bobberDip;
        group.Children.Add(CreateVoxelBox(new(0, by + 0.35, 0), 1.2, 0.7, 1.2, Color.FromRgb(220, 35, 35)));
        group.Children.Add(CreateVoxelBox(new(0, by - 0.35, 0), 1.2, 0.7, 1.2, Color.FromRgb(245, 245, 245)));
        group.Children.Add(CreateVoxelBox(new(0, by + 0.9, 0), 0.25, 0.6, 0.25, Color.FromRgb(40, 40, 40)));

        // 3. Брызги при поклёвке
        if (isSplash)
        {
            group.Children.Add(CreateVoxelBox(new(1.1, by + 0.8, 0.8), 0.4, 0.4, 0.4, Color.FromRgb(200, 235, 255)));
            group.Children.Add(CreateVoxelBox(new(-1.2, by + 0.9, -0.6), 0.4, 0.4, 0.4, Color.FromRgb(200, 235, 255)));
            group.Children.Add(CreateVoxelBox(new(0.4, by + 1.2, -1.0), 0.35, 0.35, 0.35, Color.FromRgb(200, 235, 255)));
        }

        return group;
    }

    public static GeometryModel3D CreateVoxelBox(Point3D center, double w, double h, double d, Color color)
    {
        double hx = w / 2.0, hy = h / 2.0, hz = d / 2.0;
        double cx = center.X, cy = center.Y, cz = center.Z;
        var mesh = new MeshGeometry3D();

        void Quad(Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            int i = mesh.Positions.Count;
            mesh.Positions.Add(p0); mesh.Positions.Add(p1); mesh.Positions.Add(p2); mesh.Positions.Add(p3);
            mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2);
            mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 3);
        }

        Quad(new(cx - hx, cy - hy, cz + hz), new(cx + hx, cy - hy, cz + hz), new(cx + hx, cy + hy, cz + hz), new(cx - hx, cy + hy, cz + hz));
        Quad(new(cx + hx, cy - hy, cz - hz), new(cx - hx, cy - hy, cz - hz), new(cx - hx, cy + hy, cz - hz), new(cx + hx, cy + hy, cz - hz));
        Quad(new(cx + hx, cy - hy, cz + hz), new(cx + hx, cy - hy, cz - hz), new(cx + hx, cy + hy, cz - hz), new(cx + hx, cy + hy, cz + hz));
        Quad(new(cx - hx, cy - hy, cz - hz), new(cx - hx, cy - hy, cz + hz), new(cx - hx, cy + hy, cz + hz), new(cx - hx, cy + hy, cz - hz));
        Quad(new(cx - hx, cy + hy, cz + hz), new(cx + hx, cy + hy, cz + hz), new(cx + hx, cy + hy, cz - hz), new(cx - hx, cy + hy, cz - hz));
        Quad(new(cx - hx, cy - hy, cz - hz), new(cx + hx, cy - hy, cz - hz), new(cx + hx, cy - hy, cz + hz), new(cx - hx, cy - hy, cz + hz));

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var mat = new DiffuseMaterial(brush);
        return new GeometryModel3D(mesh, mat) { BackMaterial = mat };
    }

    #endregion

    #region Руды (Ore Blocks) по фотографии 1 (Каменные и Глубинносланцевые)

    public static Model3DGroup CreateOreBlock(OreType type, bool deepslate = false, int crackStage = 0)
    {
        var sideBmp = GenerateOreTexture(type, deepslate, isTop: false, crackStage);
        var topBmp = GenerateOreTexture(type, deepslate, isTop: true, crackStage);
        return CreateTexturedCube(sideBmp, topBmp, topBmp, 8.0);
    }

    static void ApplyCracks(uint[] p, int crackStage)
    {
        if (crackStage <= 0) return;

        uint dark = Rgb(18, 18, 20);
        uint edge = Rgb(235, 235, 240);

        void Mark(int x, int y)
        {
            if (x >= 0 && x < 16 && y >= 0 && y < 16)
            {
                p[y * 16 + x] = dark;
                if (x + 1 < 16 && p[y * 16 + x + 1] != dark)
                    p[y * 16 + x + 1] = edge;
            }
        }

        // Стадия 1 (легкие трещины в центре блока)
        Mark(7, 7); Mark(8, 7); Mark(8, 8); Mark(9, 8); Mark(9, 7); Mark(6, 9); Mark(8, 6);

        if (crackStage >= 2)
        {
            // Стадия 2 (ветвистые трещины к краям)
            Mark(6, 6); Mark(5, 5); Mark(4, 4); Mark(4, 5); Mark(3, 4);
            Mark(6, 10); Mark(5, 11); Mark(4, 12);
            Mark(10, 6); Mark(11, 5); Mark(12, 4);
            Mark(10, 9); Mark(11, 10); Mark(12, 11); Mark(13, 12);
        }

        if (crackStage >= 3)
        {
            // Стадия 3 (глубокие сквозные расколы по всей поверхности)
            Mark(2, 3); Mark(1, 3); Mark(3, 13); Mark(2, 14);
            Mark(13, 3); Mark(14, 2); Mark(14, 13);
            Mark(7, 3); Mark(7, 4); Mark(7, 5);
            Mark(8, 11); Mark(8, 12); Mark(8, 13);
            Mark(2, 8); Mark(3, 8); Mark(4, 8);
            Mark(12, 8); Mark(13, 8); Mark(14, 8);
        }
    }

    static BitmapSource GenerateOreTexture(OreType type, bool deepslate, bool isTop = false, int crackStage = 0)
    {
        uint[] p = new uint[256];

        // 1. Базовый фон камня или сланца
        if (type == OreType.NetherQuartz || type == OreType.NetherGold)
        {
            // Незерак (Netherrack)
            uint c0 = Rgb(95, 22, 22), c1 = Rgb(115, 29, 29), c2 = Rgb(75, 15, 15), c3 = Rgb(130, 35, 35);
            uint[] netherNoise = { c0, c1, c2, c1, c3, c0, c2, c1, c1, c0, c3, c2, c0, c1, c2, c0 };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    p[y * 16 + x] = netherNoise[(x * 7 + y * 13) % 16];
        }
        else if (type == OreType.AncientDebris)
        {
            // Древние обломки (Ancient Debris)
            uint darkB = Rgb(71, 49, 39), midB = Rgb(94, 66, 53), lightB = Rgb(122, 90, 75);
            for (int y = 0; y < 16; y++)
            {
                uint rowCol = (y % 3 == 0) ? lightB : (y % 2 == 0 ? midB : darkB);
                for (int x = 0; x < 16; x++)
                    p[y * 16 + x] = ((x + y) % 5 == 0) ? lightB : rowCol;
            }
            ApplyCracks(p, crackStage);
            return Make16x16Bitmap(p);
        }
        else if (deepslate)
        {
            // Глубинный сланец (Deepslate) - слоистая темная структура
            uint s0 = Rgb(45, 45, 52), s1 = Rgb(35, 35, 40), s2 = Rgb(26, 26, 31), s3 = Rgb(52, 52, 60);
            for (int y = 0; y < 16; y++)
            {
                int layer = (y / 4) % 2;
                for (int x = 0; x < 16; x++)
                {
                    int n = (x * 3 + y * 7) % 4;
                    p[y * 16 + x] = layer == 0 ? (n == 0 ? s3 : (n == 1 ? s1 : s0)) : (n == 0 ? s0 : (n == 1 ? s2 : s1));
                }
            }
        }
        else
        {
            // Обычный камень (Stone) - ровный серый шум как в Minecraft
            uint g0 = Rgb(118, 118, 118), g1 = Rgb(128, 128, 128), g2 = Rgb(102, 102, 102), g3 = Rgb(89, 89, 89);
            uint[] stoneNoise = { g0, g1, g2, g0, g3, g1, g0, g2, g1, g3, g0, g1, g2, g0, g1, g3 };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    p[y * 16 + x] = stoneNoise[(x * 5 + y * 11) % 16];
        }

        // 2. Наложение уникального аутентичного узора руды по фото 1
        void PutSpot(int cx, int cy, uint mainCol, uint darkCol, uint lightCol)
        {
            if (cx >= 0 && cx < 16 && cy >= 0 && cy < 16) p[cy * 16 + cx] = lightCol;
            if (cx + 1 < 16 && cy < 16) p[cy * 16 + cx + 1] = mainCol;
            if (cx < 16 && cy + 1 < 16) p[(cy + 1) * 16 + cx] = darkCol;
            if (cx + 1 < 16 && cy + 1 < 16) p[(cy + 1) * 16 + cx + 1] = mainCol;
        }

        switch (type)
        {
            case OreType.Diamond:
                // Алмазы: бирюзово-голубые кристаллы с белым блеском
                uint dMain = Rgb(46, 203, 212), dDark = Rgb(15, 120, 140), dLight = Rgb(182, 252, 252);
                PutSpot(3, 4, dMain, dDark, dLight);
                PutSpot(10, 3, dMain, dDark, dLight);
                PutSpot(6, 9, dMain, dDark, dLight);
                PutSpot(11, 11, dMain, dDark, dLight);
                PutSpot(2, 12, dMain, dDark, dLight);
                break;

            case OreType.Emerald:
                // Изумруды: ограненные ярко-зеленые кристаллы
                uint eMain = Rgb(23, 221, 98), eDark = Rgb(12, 168, 68), eLight = Rgb(122, 252, 164);
                PutSpot(4, 3, eMain, eDark, eLight);
                PutSpot(9, 6, eMain, eDark, eLight);
                PutSpot(3, 10, eMain, eDark, eLight);
                PutSpot(11, 11, eMain, eDark, eLight);
                break;

            case OreType.Gold:
            case OreType.NetherGold:
                // Золото: 4-конечные золотые звездочки
                uint gMain = Rgb(252, 238, 75), gDark = Rgb(182, 133, 22), gLight = Rgb(255, 250, 160);
                PutSpot(3, 5, gMain, gDark, gLight);
                PutSpot(10, 4, gMain, gDark, gLight);
                PutSpot(6, 10, gMain, gDark, gLight);
                PutSpot(12, 11, gMain, gDark, gLight);
                break;

            case OreType.Iron:
                // Железо: бежево-рыжеватые продолговатые самородки
                uint iMain = Rgb(216, 175, 147), iDark = Rgb(141, 98, 74), iLight = Rgb(240, 215, 195);
                PutSpot(4, 4, iMain, iDark, iLight);
                PutSpot(9, 3, iMain, iDark, iLight);
                PutSpot(5, 9, iMain, iDark, iLight);
                PutSpot(11, 10, iMain, iDark, iLight);
                PutSpot(2, 11, iMain, iDark, iLight);
                break;

            case OreType.Copper:
                // Медь: терракотовые капли + зеленовато-бирюзовые окисленные крапинки
                uint cMain = Rgb(224, 115, 77), cDark = Rgb(184, 82, 50), cOxide = Rgb(72, 178, 156);
                PutSpot(3, 4, cMain, cDark, cOxide);
                PutSpot(10, 5, cMain, cDark, cOxide);
                PutSpot(6, 9, cMain, cDark, cOxide);
                PutSpot(11, 11, cMain, cDark, cOxide);
                break;

            case OreType.Redstone:
                // Редстоун: рубиново-красные капли
                uint rMain = Rgb(255, 20, 20), rDark = Rgb(145, 0, 0), rLight = Rgb(255, 120, 120);
                PutSpot(4, 3, rMain, rDark, rLight);
                PutSpot(10, 5, rMain, rDark, rLight);
                PutSpot(3, 10, rMain, rDark, rLight);
                PutSpot(8, 11, rMain, rDark, rLight);
                break;

            case OreType.Lapis:
                // Лазурит: глубоко-синие прямоугольные камни
                uint lMain = Rgb(24, 85, 179), lDark = Rgb(15, 57, 128), lLight = Rgb(57, 121, 221);
                PutSpot(3, 4, lMain, lDark, lLight);
                PutSpot(9, 5, lMain, lDark, lLight);
                PutSpot(5, 10, lMain, lDark, lLight);
                PutSpot(11, 11, lMain, lDark, lLight);
                break;

            case OreType.Coal:
                // Уголь: округлые черные комки
                uint kMain = Rgb(30, 30, 30), kDark = Rgb(15, 15, 15), kLight = Rgb(60, 60, 60);
                PutSpot(4, 4, kMain, kDark, kLight);
                PutSpot(10, 4, kMain, kDark, kLight);
                PutSpot(5, 9, kMain, kDark, kLight);
                PutSpot(11, 11, kMain, kDark, kLight);
                break;

            case OreType.NetherQuartz:
                // Кварц Нижнего мира: острые диагональные белые прожилки
                uint qMain = Rgb(234, 229, 222), qDark = Rgb(181, 171, 161), qLight = Rgb(255, 255, 255);
                PutSpot(3, 4, qMain, qDark, qLight);
                PutSpot(8, 6, qMain, qDark, qLight);
                PutSpot(12, 10, qMain, qDark, qLight);
                PutSpot(5, 11, qMain, qDark, qLight);
                break;
        }

        ApplyCracks(p, crackStage);
        return Make16x16Bitmap(p);
    }

    #endregion

    #region Бревна деревьев (Wood Logs) по фотографии 2 (все 11 видов)

    public static Model3DGroup CreateTreeBlock(TreeType type, int crackStage = 0)
    {
        var sideBmp = GenerateTreeSideTexture(type, crackStage);
        var topBmp = GenerateTreeTopTexture(type, crackStage);
        return CreateTexturedCube(sideBmp, topBmp, topBmp, 8.0);
    }

    static BitmapSource GenerateTreeTopTexture(TreeType type, int crackStage = 0)
    {
        uint[] p = new uint[256];
        uint barkBorder, ringDark, ringLight, core;

        switch (type)
        {
            case TreeType.Birch:
                // Белая кора с золотисто-кремовой сердцевиной
                barkBorder = Rgb(220, 220, 220); ringDark = Rgb(189, 166, 109); ringLight = Rgb(213, 193, 137); core = Rgb(227, 210, 159);
                break;
            case TreeType.Spruce:
                barkBorder = Rgb(56, 36, 16); ringDark = Rgb(85, 57, 28); ringLight = Rgb(108, 77, 41); core = Rgb(133, 95, 52);
                break;
            case TreeType.DarkOak:
                barkBorder = Rgb(39, 25, 11); ringDark = Rgb(50, 30, 13); ringLight = Rgb(67, 43, 20); core = Rgb(87, 57, 27);
                break;
            case TreeType.PaleOak:
                barkBorder = Rgb(140, 141, 144); ringDark = Rgb(135, 136, 139); ringLight = Rgb(162, 163, 166); core = Rgb(184, 185, 188);
                break;
            case TreeType.Jungle:
                barkBorder = Rgb(89, 71, 38); ringDark = Rgb(138, 108, 61); ringLight = Rgb(163, 131, 78); core = Rgb(186, 150, 92);
                break;
            case TreeType.Acacia:
                // Серая кора снаружи и СОЧНАЯ ЯРКО-ОРАНЖЕВАЯ сердцевина внутри по фото 2!
                barkBorder = Rgb(103, 98, 91); ringDark = Rgb(158, 74, 31); ringLight = Rgb(192, 95, 44); core = Rgb(218, 111, 53);
                break;
            case TreeType.Mangrove:
                barkBorder = Rgb(74, 59, 50); ringDark = Rgb(95, 32, 23); ringLight = Rgb(120, 44, 34); core = Rgb(148, 55, 43);
                break;
            case TreeType.Cherry:
                // Темная кора и нежно-розовые кольца вишни!
                barkBorder = Rgb(54, 33, 43); ringDark = Rgb(185, 107, 121); ringLight = Rgb(219, 142, 155); core = Rgb(241, 174, 185);
                break;
            case TreeType.Crimson:
                // Багровое дерево
                barkBorder = Rgb(88, 16, 32); ringDark = Rgb(74, 12, 27); ringLight = Rgb(102, 21, 39); core = Rgb(133, 28, 52);
                break;
            case TreeType.Warped:
                // Искаженное дерево (бирюзовое)
                barkBorder = Rgb(19, 78, 74); ringDark = Rgb(26, 102, 96); ringLight = Rgb(35, 130, 122); core = Rgb(50, 161, 151);
                break;
            case TreeType.Oak:
            default:
                barkBorder = Rgb(95, 70, 42); ringDark = Rgb(132, 101, 58); ringLight = Rgb(158, 125, 77); core = Rgb(184, 147, 92);
                break;
        }

        // Рисуем концентрические кольца среза дерева
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                int dist = Math.Max(Math.Abs(x - 7), Math.Abs(y - 7));
                if (dist >= 7) p[y * 16 + x] = barkBorder;
                else if (dist >= 5) p[y * 16 + x] = ringDark;
                else if (dist >= 3) p[y * 16 + x] = ringLight;
                else if (dist >= 1) p[y * 16 + x] = ringDark;
                else p[y * 16 + x] = core;
            }
        }
        ApplyCracks(p, crackStage);
        return Make16x16Bitmap(p);
    }

    static BitmapSource GenerateTreeSideTexture(TreeType type, int crackStage = 0)
    {
        uint[] p = new uint[256];

        switch (type)
        {
            case TreeType.Birch:
                // Белая березовая кора с черными горизонтальными штрихами!
                uint w0 = Rgb(235, 235, 235), w1 = Rgb(248, 248, 248), w2 = Rgb(215, 215, 215);
                uint b0 = Rgb(45, 43, 40), b1 = Rgb(75, 73, 70);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                        p[y * 16 + x] = (x + y * 3) % 4 == 0 ? w2 : ((x * 2 + y) % 3 == 0 ? w1 : w0);
                // Черные полоски (чечевички березы)
                void BirchNotch(int x, int y, int len)
                {
                    for (int i = 0; i < len && x + i < 16; i++) p[y * 16 + x + i] = (i == 0 || i == len - 1) ? b1 : b0;
                }
                BirchNotch(2, 3, 4);
                BirchNotch(10, 5, 5);
                BirchNotch(4, 9, 3);
                BirchNotch(8, 12, 4);
                BirchNotch(1, 14, 3);
                break;

            case TreeType.Acacia:
                // Серая кора акации
                uint a0 = Rgb(103, 98, 91), a1 = Rgb(81, 77, 71), a2 = Rgb(120, 115, 107);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                        p[y * 16 + x] = (x % 3 == 0) ? a1 : ((x % 2 == 0) ? a0 : a2);
                break;

            case TreeType.Warped:
                // Бирюзовая кора с синими гифами
                uint wp0 = Rgb(19, 78, 74), wp1 = Rgb(26, 111, 105), wpVein = Rgb(65, 211, 199);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        bool isVein = (x == 4 && (y < 6 || y > 10)) || (x == 5 && y >= 5 && y <= 11) || (x == 11 && y > 3);
                        p[y * 16 + x] = isVein ? wpVein : ((x % 2 == 0) ? wp0 : wp1);
                    }
                break;

            case TreeType.Crimson:
                // Багровая кора с красными гифами
                uint cr0 = Rgb(74, 12, 26), cr1 = Rgb(95, 16, 34), crVein = Rgb(180, 24, 52);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        bool isVein = (x == 3 && y > 2) || (x == 10 && (y < 7 || y > 11)) || (x == 11 && y >= 6 && y <= 12);
                        p[y * 16 + x] = isVein ? crVein : ((x % 2 == 0) ? cr0 : cr1);
                    }
                break;

            case TreeType.Cherry:
                // Темно-бордовая кора сакуры с тонкими светлыми насечками
                uint ch0 = Rgb(54, 33, 43), ch1 = Rgb(36, 20, 28), ch2 = Rgb(72, 46, 59), chSlit = Rgb(105, 68, 86);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                        p[y * 16 + x] = (x % 2 == 0) ? ch0 : ch1;
                p[4 * 16 + 3] = chSlit; p[4 * 16 + 4] = chSlit;
                p[10 * 16 + 9] = chSlit; p[10 * 16 + 10] = chSlit;
                p[13 * 16 + 2] = chSlit;
                break;

            case TreeType.PaleOak:
                // Бледный дуб: серебристо-пепельная кора
                uint po0 = Rgb(148, 149, 152), po1 = Rgb(122, 123, 126), po2 = Rgb(90, 91, 93);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                        p[y * 16 + x] = (x % 3 == 0) ? po2 : ((x % 2 == 0) ? po1 : po0);
                break;

            case TreeType.Spruce:
            case TreeType.DarkOak:
            case TreeType.Mangrove:
            case TreeType.Jungle:
            case TreeType.Oak:
            default:
                // Вертикальные волокна коры
                uint c0 = type == TreeType.Spruce ? Rgb(67, 46, 24) :
                          type == TreeType.DarkOak ? Rgb(39, 25, 11) :
                          type == TreeType.Mangrove ? Rgb(74, 59, 50) :
                          type == TreeType.Jungle ? Rgb(89, 71, 38) : Rgb(107, 83, 53);

                uint c1 = type == TreeType.Spruce ? Rgb(46, 30, 13) :
                          type == TreeType.DarkOak ? Rgb(27, 17, 7) :
                          type == TreeType.Mangrove ? Rgb(57, 43, 35) :
                          type == TreeType.Jungle ? Rgb(70, 55, 28) : Rgb(84, 63, 37);

                uint c2 = type == TreeType.Spruce ? Rgb(82, 57, 32) :
                          type == TreeType.DarkOak ? Rgb(54, 35, 17) :
                          type == TreeType.Mangrove ? Rgb(92, 74, 64) :
                          type == TreeType.Jungle ? Rgb(107, 85, 47) : Rgb(122, 96, 62);

                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                        p[y * 16 + x] = (x % 3 == 0) ? c1 : ((x % 2 == 0) ? c0 : c2);
                break;
        }

        ApplyCracks(p, crackStage);
        return Make16x16Bitmap(p);
    }

    #endregion

    #region Рыба и улов (Fishing Loot) по фотографиям 5, 6, 7

    public static Model3DGroup CreateFishingLoot(FishingLootType loot)
    {
        switch (loot)
        {
            case FishingLootType.Cod:
                return CreateRawCodModel();
            case FishingLootType.Salmon:
                return CreateRawSalmonModel();
            case FishingLootType.TropicalFish:
                return CreateClownfishModel();
            case FishingLootType.Pufferfish:
                return CreatePufferfishModel();
            case FishingLootType.Diamond:
                return CreateDiamondModel();
            case FishingLootType.EnchantedBook:
                return CreateEnchantedBookModel();
            case FishingLootType.TNT:
                return CreateTNTModel();
            default:
                return CreateRawCodModel();
        }
    }

    /// <summary>
    /// Сырая треска (Raw Cod) по фото 5: бежево-коричневая рыбка с плавниками и глазом
    /// </summary>
    static Model3DGroup CreateRawCodModel()
    {
        string[] sprite = new[]
        {
            "................",
            "........#.......",
            ".......#m#..#...",
            "......#mmm##m#..",
            ".....#mmmmmmm#..",
            "....#mmmmmmmm#..",
            "...#mmmmmmmm##..",
            "..#bmmmmmmmmm#..",
            "..#ebmmmmmmmmm#.",
            "..#bbmmmmmmmmm#.",
            "...#bbmmmmmm##..",
            "....#bbbbbb##...",
            ".....#bbbb#.....",
            "......####......",
            "................",
            "................"
        };

        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(100, 72, 45),       // Контур
            ['m'] = Color.FromRgb(176, 141, 102),     // Коричневая спинка
            ['b'] = Color.FromRgb(222, 192, 158),     // Светлое брюшко
            ['e'] = Color.FromRgb(30, 24, 18)         // Глаз
        };

        var model = CreateExtrudedVoxelModel(sprite, palette, scale: 0.38, depth: 0.35, pivotX: 8, pivotY: 8);
        return model;
    }

    /// <summary>
    /// Сырой лосось (Raw Salmon) по фото 6: ярко-красное тело и шалфейно-зеленая голова и плавники
    /// </summary>
    static Model3DGroup CreateRawSalmonModel()
    {
        string[] sprite = new[]
        {
            "................",
            ".........r......",
            "........rrr..r..",
            ".......rrrrrrrr.",
            "......rrrrrrrrr.",
            ".....rrrrrrrrrr.",
            "....ggrrrrrrrrr.",
            "...gggrrrrrrrrr.",
            "..eeggrrrrrrrrr.",
            "...gggrrrrrrrr..",
            "....ggrrrrrrr...",
            "......rrrrrr....",
            ".......rrrr.....",
            "........rr......",
            "................",
            "................"
        };

        var palette = new Dictionary<char, Color>
        {
            ['r'] = Color.FromRgb(183, 51, 51),       // Красное мясо лосося
            ['g'] = Color.FromRgb(86, 117, 94),       // Зеленая голова и чешуя
            ['e'] = Color.FromRgb(25, 35, 27)         // Глаз
        };

        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.38, depth: 0.35, pivotX: 8, pivotY: 8);
    }

    /// <summary>
    /// Тропическая рыбка-клоун (Clownfish) по фото 7: ярко-оранжевая с белыми полосами
    /// </summary>
    static Model3DGroup CreateClownfishModel()
    {
        string[] sprite = new[]
        {
            "................",
            ".......o.o......",
            "......ooooo.o...",
            ".....oowooowwo..",
            "....eoowooowwo..",
            "....eoowooowwo..",
            ".....oowooowwo..",
            "......ooooo.o...",
            ".......o.o......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        };

        var palette = new Dictionary<char, Color>
        {
            ['o'] = Color.FromRgb(241, 103, 30),      // Оранжевый
            ['w'] = Color.FromRgb(245, 245, 245),     // Белые полосы
            ['e'] = Color.FromRgb(20, 20, 20)         // Черный глаз
        };

        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.42, depth: 0.35, pivotX: 8, pivotY: 5);
    }

    /// <summary>
    /// Иглобрюх (Pufferfish) по фото 7: раздутый золотисто-желтый шар с шипами и синими плавниками
    /// </summary>
    static Model3DGroup CreatePufferfishModel()
    {
        string[] sprite = new[]
        {
            "................",
            "......ssss......",
            "....syyyyyys....",
            "...syyeyyyyeys..",
            "..bbyyyyyyyybb..",
            "..bbyyy..yyybb..",
            "...swwwwwwwws...",
            "....swwwwwws....",
            "......ssss......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        };

        var palette = new Dictionary<char, Color>
        {
            ['s'] = Color.FromRgb(207, 128, 21),      // Колючки / шипы
            ['y'] = Color.FromRgb(234, 155, 34),      // Золотистая спинка
            ['w'] = Color.FromRgb(244, 232, 207),     // Белое брюшко
            ['b'] = Color.FromRgb(59, 187, 211),      // Голубые плавники
            ['e'] = Color.FromRgb(28, 20, 14)         // Глаза
        };

        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.42, depth: 0.42, pivotX: 8, pivotY: 5);
    }

    /// <summary>
    /// Алмаз (Diamond Item) по фото 7: каноничный бирюзовый кристалл Minecraft
    /// </summary>
    static Model3DGroup CreateDiamondModel()
    {
        string[] sprite = new[]
        {
            "................",
            "......####......",
            "....##wddd##....",
            "...#wwdddddd#...",
            "..#wwdddddddd#..",
            "..#wddddddddm#..",
            "..#ddddddddmm#..",
            "...#ddddddmm#...",
            "....#ddddmm#....",
            ".....#ddmm#.....",
            "......#mm#......",
            ".......##.......",
            "................",
            "................",
            "................",
            "................"
        };

        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(12, 86, 88),        // Темный контур
            ['w'] = Color.FromRgb(255, 255, 255),     // Белый блик
            ['d'] = Color.FromRgb(77, 237, 244),      // Светящийся бирюзовый
            ['m'] = Color.FromRgb(30, 181, 200)       // Тень алмаза
        };

        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.42, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    /// <summary>
    /// Зачарованная книга (Enchanted Book) по фото 7: кожаная книга с фиолетовой лентой
    /// </summary>
    static Model3DGroup CreateEnchantedBookModel()
    {
        string[] sprite = new[]
        {
            "................",
            ".....########...",
            "....#cccccccc#..",
            "...#ccppcccc##..",
            "..#ccppcccc#p#..",
            "..#ccppppp#pp#..",
            "..#ccppcc#ppp#..",
            "..#ccppc#pppp#..",
            "..#wwww#ccccc#..",
            "...#ww#ccccc#...",
            "....##ccccc#....",
            ".....#####......",
            "................",
            "................",
            "................",
            "................"
        };

        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(55, 30, 15),        // Контур
            ['c'] = Color.FromRgb(105, 60, 30),       // Коричневая кожа
            ['w'] = Color.FromRgb(232, 226, 212),     // Страницы книги
            ['p'] = Color.FromRgb(226, 40, 232)       // Фиолетово-малиновое сияние чар
        };

        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.42, depth: 0.35, pivotX: 8, pivotY: 6);
    }

    /// <summary>
    /// Динамит (TNT Block) по фото 7: 3D блок с красными шашками, белой лентой "TNT" и фитилем
    /// </summary>
    static Model3DGroup CreateTNTModel()
    {
        uint[] side = new uint[256];
        uint[] top = new uint[256];

        uint redMain = Rgb(200, 40, 30), redDark = Rgb(160, 28, 20);
        uint whiteBanner = Rgb(240, 240, 240);
        uint textCol = Rgb(20, 20, 20);

        // Боковые стороны: шашки сверху и снизу, белая полоса посередине с буквами TNT
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                if (y >= 6 && y <= 9)
                {
                    side[y * 16 + x] = whiteBanner;
                }
                else
                {
                    side[y * 16 + x] = (x % 2 == 0) ? redMain : redDark;
                }
            }
        }

        // Рисуем буквы "TNT" на белой полосе
        // T (x: 2..4)
        side[6 * 16 + 2] = textCol; side[6 * 16 + 3] = textCol; side[6 * 16 + 4] = textCol;
        side[7 * 16 + 3] = textCol; side[8 * 16 + 3] = textCol; side[9 * 16 + 3] = textCol;
        // N (x: 6..8)
        side[6 * 16 + 6] = textCol; side[7 * 16 + 6] = textCol; side[8 * 16 + 6] = textCol; side[9 * 16 + 6] = textCol;
        side[7 * 16 + 7] = textCol; side[8 * 16 + 7] = textCol;
        side[6 * 16 + 8] = textCol; side[7 * 16 + 8] = textCol; side[8 * 16 + 8] = textCol; side[9 * 16 + 8] = textCol;
        // T (x: 10..12)
        side[6 * 16 + 10] = textCol; side[6 * 16 + 11] = textCol; side[6 * 16 + 12] = textCol;
        side[7 * 16 + 11] = textCol; side[8 * 16 + 11] = textCol; side[9 * 16 + 11] = textCol;

        // Верх: красные шашки по кругу и серый фитиль в центре
        uint fuseCol = Rgb(70, 70, 70);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                top[y * 16 + x] = (x >= 6 && x <= 9 && y >= 6 && y <= 9) ? fuseCol : ((x % 2 == 0) ? redMain : redDark);

        var sideBmp = Make16x16Bitmap(side);
        var topBmp = Make16x16Bitmap(top);
        return CreateTexturedCube(sideBmp, topBmp, topBmp, 5.5);
    }

    #endregion

    #region Выпадающий лут руд и деревьев (Mined Resource Drops)

    public static Model3DGroup CreateMiniDrop(bool isWood, TreeType tree, OreType ore)
    {
        if (isWood)
        {
            var sideBmp = GenerateTreeSideTexture(tree, 0);
            var topBmp = GenerateTreeTopTexture(tree, 0);
            return CreateTexturedCube(sideBmp, topBmp, topBmp, 3.8);
        }

        return ore switch
        {
            OreType.Coal => CreateCoalModel(),
            OreType.Iron => CreateRawIronModel(),
            OreType.Copper => CreateRawCopperModel(),
            OreType.Gold => CreateRawGoldModel(),
            OreType.Lapis => CreateLapisModel(),
            OreType.Redstone => CreateRedstoneModel(),
            OreType.Diamond => CreateDiamondModel(),
            OreType.Emerald => CreateEmeraldModel(),
            OreType.NetherQuartz => CreateNetherQuartzModel(),
            OreType.AncientDebris => CreateNetheriteScrapModel(),
            OreType.NetherGold => CreateRawGoldModel(),
            _ => CreateDiamondModel()
        };
    }

    static Model3DGroup CreateCoalModel()
    {
        string[] sprite = new[]
        {
            "................",
            "......####......",
            "....##cccc##....",
            "...#ccllcccc#...",
            "..#cclllccccc#..",
            "..#cllllccccc#..",
            "..#clllcccccc#..",
            "...#cccccccc#...",
            "....#cccccc#....",
            ".....#cccc#.....",
            "......####......",
            "................",
            "................",
            "................",
            "................",
            "................"
        };
        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(18, 18, 20),
            ['c'] = Color.FromRgb(36, 36, 40),
            ['l'] = Color.FromRgb(65, 65, 75)
        };
        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.40, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    static Model3DGroup CreateRawIronModel()
    {
        string[] sprite = new[]
        {
            "................",
            ".....#####......",
            "....#iiiii#.....",
            "...#iiiiiii#....",
            "..#iiidddii#....",
            "..#iidd#ddii#...",
            "..#idd###dii#...",
            "...#dd###dii#...",
            "....#ddddii#....",
            ".....#iiii#.....",
            "......####......",
            "................",
            "................",
            "................",
            "................",
            "................"
        };
        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(85, 62, 45),
            ['i'] = Color.FromRgb(216, 175, 147),
            ['d'] = Color.FromRgb(150, 110, 85)
        };
        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.40, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    static Model3DGroup CreateRawCopperModel()
    {
        string[] sprite = new[]
        {
            "................",
            "....#####.......",
            "...#ccccc##.....",
            "..#ccggccc#.....",
            "..#cggggcc#.....",
            "..#ccggccc#.....",
            "...#cccccc##....",
            "....#ccccc#.....",
            ".....#ccc#......",
            "......###.......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        };
        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(95, 45, 25),
            ['c'] = Color.FromRgb(204, 108, 74),
            ['g'] = Color.FromRgb(90, 168, 140)
        };
        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.40, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    static Model3DGroup CreateRawGoldModel()
    {
        string[] sprite = new[]
        {
            "................",
            ".....####.......",
            "....#wwgg#......",
            "...#wwgggg#.....",
            "..#wwggggdd#....",
            "..#wgggggdd#....",
            "...#ggggddd#....",
            "....#ddddd#.....",
            ".....#####......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        };
        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(130, 95, 15),
            ['w'] = Color.FromRgb(255, 255, 195),
            ['g'] = Color.FromRgb(255, 215, 0),
            ['d'] = Color.FromRgb(205, 150, 20)
        };
        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.40, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    static Model3DGroup CreateLapisModel()
    {
        string[] sprite = new[]
        {
            "................",
            "......##........",
            ".....#bb#.......",
            "....#bbll#......",
            "...#bblllg#.....",
            "..#bblllllg#....",
            "..#bllllllg#....",
            "...#llllll#.....",
            "....#llll#......",
            ".....####.......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        };
        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(12, 34, 110),
            ['l'] = Color.FromRgb(38, 67, 182),
            ['b'] = Color.FromRgb(85, 140, 245),
            ['g'] = Color.FromRgb(225, 185, 55)
        };
        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.40, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    static Model3DGroup CreateRedstoneModel()
    {
        string[] sprite = new[]
        {
            "................",
            ".......##.......",
            ".....##hh##.....",
            "....#hhrrhh#....",
            "...#hhrrrrrh#...",
            "..#rrrrrrrrrr#..",
            "..#rrrrrrrrrr#..",
            "...#rrrrrrrr#...",
            "....##rrrr##....",
            "......####......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        };
        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(120, 0, 0),
            ['r'] = Color.FromRgb(230, 18, 18),
            ['h'] = Color.FromRgb(255, 105, 105)
        };
        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.40, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    static Model3DGroup CreateEmeraldModel()
    {
        string[] sprite = new[]
        {
            "................",
            "......####......",
            "....##whee##....",
            "...#wwheeeee#...",
            "..#wwheeeeeee#..",
            "..#wheeeeeeeh#..",
            "..#heeeeeeehh#..",
            "...#eeeeeehh#...",
            "....#eeeehh#....",
            ".....#eehh#.....",
            "......####......",
            "................",
            "................",
            "................",
            "................",
            "................"
        };
        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(5, 60, 20),
            ['w'] = Color.FromRgb(220, 255, 220),
            ['h'] = Color.FromRgb(95, 245, 135),
            ['e'] = Color.FromRgb(23, 201, 80)
        };
        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.40, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    static Model3DGroup CreateNetherQuartzModel()
    {
        string[] sprite = new[]
        {
            "................",
            "......##........",
            ".....#ww#.......",
            "....#wwqq#......",
            "...#wwqqqq#.....",
            "..#wwqqqqgg#....",
            "..#wqqqqgggg#...",
            "...#qqqgggg#....",
            "....#ggggg#.....",
            ".....#####......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        };
        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(165, 155, 145),
            ['w'] = Color.FromRgb(255, 255, 255),
            ['q'] = Color.FromRgb(240, 235, 230),
            ['g'] = Color.FromRgb(185, 175, 165)
        };
        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.40, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    static Model3DGroup CreateNetheriteScrapModel()
    {
        string[] sprite = new[]
        {
            "................",
            ".....#####......",
            "....#hhhhh#.....",
            "...#hhsssss#....",
            "..#hhsssssss#...",
            "..#hssssssss#...",
            "...#sssssss#....",
            "....#sssss#.....",
            ".....#####......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        };
        var palette = new Dictionary<char, Color>
        {
            ['#'] = Color.FromRgb(42, 30, 25),
            ['h'] = Color.FromRgb(115, 85, 72),
            ['s'] = Color.FromRgb(75, 52, 42)
        };
        return CreateExtrudedVoxelModel(sprite, palette, scale: 0.40, depth: 0.32, pivotX: 8, pivotY: 6);
    }

    #endregion

    #region Названия для диалоговых пузырей

    public static string GetTreeName(TreeType t) => t switch
    {
        TreeType.Oak => "Дуб 🪵",
        TreeType.Birch => "Берёза 🪵",
        TreeType.Spruce => "Ель 🪵",
        TreeType.DarkOak => "Тёмный дуб 🪵",
        TreeType.PaleOak => "Бледный дуб 🪵",
        TreeType.Jungle => "Тропическое дерево 🪵",
        TreeType.Acacia => "Акация 🪵",
        TreeType.Mangrove => "Мангровое дерево 🪵",
        TreeType.Cherry => "Вишня 🌸",
        TreeType.Crimson => "Багровое дерево 🍄",
        TreeType.Warped => "Искажённое дерево 🍄",
        _ => "Дерево 🪵"
    };

    public static string GetOreName(OreType o) => o switch
    {
        OreType.Coal => "Угольная руда ⚫",
        OreType.Iron => "Железная руда ⚪",
        OreType.Copper => "Медная руда 🟠",
        OreType.Gold => "Золотая руда 🟡",
        OreType.Lapis => "Лазуритовая руда 🔵",
        OreType.Redstone => "Красная руда (Редстоун) 🔴",
        OreType.Diamond => "Алмазная руда 💎",
        OreType.Emerald => "Изумрудная руда 🟢",
        OreType.NetherQuartz => "Кварцевая руда ⚪",
        OreType.AncientDebris => "Древние обломки (Незерит) 🪙",
        OreType.NetherGold => "Золотая руда Нижнего мира ✨",
        _ => "Руда 💎"
    };

    #endregion
}
