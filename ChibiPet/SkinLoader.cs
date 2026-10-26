using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChibiPet;

public sealed class SkinData
{
    /// <summary>Пиксели 64x64 в формате BGRA32 (старые 64x32 уже приведены к новому формату).</summary>
    public byte[] Pixels { get; init; } = Array.Empty<byte>();

    /// <summary>true — тонкие руки (Alex, 3 пикселя), false — обычные (Steve, 4 пикселя).</summary>
    public bool Slim { get; init; }
}

public static class SkinLoader
{
    const int W = 64;

    public static SkinData Load(string path)
    {
        if (!File.Exists(path))
        {
            return CreateDefaultSteveSkin();
        }

        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.UriSource = new Uri(Path.GetFullPath(path));
        img.EndInit();
        img.Freeze();

        int w = img.PixelWidth, h = img.PixelHeight;
        if (w != 64 || (h != 64 && h != 32))
            throw new InvalidDataException($"Нужен скин 64×64 или 64×32, а у файла {w}×{h}. HD-скины пока не поддерживаются.");

        var conv = new FormatConvertedBitmap(img, PixelFormats.Bgra32, null, 0);
        var src = new byte[w * h * 4];
        conv.CopyPixels(src, w * 4, 0);

        var px = new byte[W * W * 4];
        Buffer.BlockCopy(src, 0, px, 0, src.Length);

        // Старый формат 64x32: левые рука и нога отсутствуют — зеркалим правые.
        if (h == 32)
        {
            MirrorLimb(px, 0, 16, 16, 48);   // правая нога -> левая
            MirrorLimb(px, 40, 16, 32, 48);  // правая рука -> левая
        }

        bool slim = h == 64 && IsTransparent(px, 54, 20, 2, 12) && IsTransparent(px, 46, 52, 2, 12);

        // Очищаем неиспользуемую спецификацией Minecraft область (56..64, 16..48),
        // где авторы скинов ставят свои подписи/вотермарки, чтобы они не накладывались на модельку.
        ClearUnusedRegions(px);

        return new SkinData { Pixels = px, Slim = slim };
    }

    static void ClearUnusedRegions(byte[] p)
    {
        // В Minecraft 64x64 область x=56..64, y=16..48 НЕ используется ни для одной части тела.
        // Очищаем её от подписей авторов.
        for (int y = 16; y < 48; y++)
        {
            for (int x = 56; x < 64; x++)
            {
                int i = (y * W + x) * 4;
                p[i] = p[i + 1] = p[i + 2] = p[i + 3] = 0;
            }
        }
    }

    static bool IsTransparent(byte[] p, int x, int y, int w, int h)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                if (p[(yy * W + xx) * 4 + 3] != 0) return false;
        return true;
    }

    static void MirrorLimb(byte[] p, int su, int sv, int du, int dv)
    {
        const int w = 4, h = 12, d = 4;
        CopyFlip(p, su + d,         sv,     du + d,         dv,     w, d);
        CopyFlip(p, su + d + w,     sv,     du + d + w,     dv,     w, d);
        CopyFlip(p, su + d,         sv + d, du + d,         dv + d, w, h);
        CopyFlip(p, su + d + w + d, sv + d, du + d + w + d, dv + d, w, h);
        CopyFlip(p, su,             sv + d, du + d + w,     dv + d, d, h);
        CopyFlip(p, su + d + w,     sv + d, du,             dv + d, d, h);
    }

    static void CopyFlip(byte[] p, int sx, int sy, int dx, int dy, int cw, int ch)
    {
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                Buffer.BlockCopy(p, ((sy + y) * W + sx + x) * 4, p, ((dy + y) * W + dx + (cw - 1 - x)) * 4, 4);
    }

    public static SkinData CreateDefaultSteveSkin()
    {
        var px = new byte[W * W * 4];

        byte[] skinColor = { 153, 175, 219, 255 };
        byte[] hairColor = { 20, 38, 59, 255 };
        byte[] shirtColor = { 178, 175, 0, 255 };
        byte[] pantsColor = { 128, 43, 43, 255 };
        byte[] shoesColor = { 80, 80, 80, 255 };
        byte[] eyeWhite = { 255, 255, 255, 255 };
        byte[] eyeBlue = { 180, 70, 40, 255 };

        void FillRect(int rx, int ry, int rw, int rh, byte[] col)
        {
            for (int y = ry; y < ry + rh; y++)
                for (int x = rx; x < rx + rw; x++)
                    if (x >= 0 && x < W && y >= 0 && y < W)
                        Buffer.BlockCopy(col, 0, px, (y * W + x) * 4, 4);
        }

        FillRect(0, 0, 32, 16, hairColor);
        FillRect(8, 8, 8, 8, skinColor);
        FillRect(8, 8, 8, 2, hairColor);
        FillRect(8, 12, 8, 2, hairColor);
        FillRect(9, 10, 2, 1, eyeWhite);
        FillRect(13, 10, 2, 1, eyeWhite);
        FillRect(10, 10, 1, 1, eyeBlue);
        FillRect(13, 10, 1, 1, eyeBlue);

        FillRect(16, 16, 24, 16, shirtColor);
        FillRect(19, 16, 2, 2, skinColor);

        FillRect(40, 16, 16, 4, shirtColor);
        FillRect(40, 20, 16, 12, skinColor);
        FillRect(32, 48, 16, 4, shirtColor);
        FillRect(32, 52, 16, 12, skinColor);

        FillRect(0, 16, 16, 12, pantsColor);
        FillRect(0, 28, 16, 4, shoesColor);
        FillRect(16, 48, 16, 12, pantsColor);
        FillRect(16, 60, 16, 4, shoesColor);

        return new SkinData { Pixels = px, Slim = false };
    }

    public static SkinData CreateDefaultAlexSkin()
    {
        var px = new byte[W * W * 4];

        byte[] skinColor = { 160, 185, 230, 255 };
        byte[] hairColor = { 45, 90, 180, 255 };
        byte[] shirtColor = { 60, 125, 75, 255 };
        byte[] pantsColor = { 60, 60, 75, 255 };
        byte[] bootsColor = { 70, 70, 70, 255 };
        byte[] eyeGreen = { 70, 150, 70, 255 };
        byte[] eyeWhite = { 255, 255, 255, 255 };

        void FillRect(int rx, int ry, int rw, int rh, byte[] col)
        {
            for (int y = ry; y < ry + rh; y++)
                for (int x = rx; x < rx + rw; x++)
                    if (x >= 0 && x < W && y >= 0 && y < W)
                        Buffer.BlockCopy(col, 0, px, (y * W + x) * 4, 4);
        }

        FillRect(0, 0, 32, 16, hairColor);
        FillRect(8, 8, 8, 8, skinColor);
        FillRect(8, 8, 8, 2, hairColor);
        FillRect(8, 10, 2, 1, eyeWhite);
        FillRect(10, 10, 1, 1, eyeGreen);
        FillRect(13, 10, 1, 1, eyeGreen);
        FillRect(14, 10, 2, 1, eyeWhite);

        FillRect(16, 16, 24, 16, shirtColor);

        FillRect(40, 16, 14, 4, shirtColor);
        FillRect(40, 20, 14, 12, skinColor);
        FillRect(32, 48, 14, 4, shirtColor);
        FillRect(32, 52, 14, 12, skinColor);

        FillRect(0, 16, 16, 12, pantsColor);
        FillRect(0, 28, 16, 4, bootsColor);
        FillRect(16, 48, 16, 12, pantsColor);
        FillRect(16, 60, 16, 4, bootsColor);

        return new SkinData { Pixels = px, Slim = true };
    }
}
