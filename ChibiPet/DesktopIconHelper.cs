namespace ChibiPet;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class DesktopIconHelper
{
    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    const int SHCNE_ASSOCCHANGED = 0x08000000;
    const uint SHCNF_IDLIST = 0;

    public static void UpdateDesktopIcon(SkinData? skin)
    {
        if (skin == null || skin.Pixels == null || skin.Pixels.Length < 64 * 64 * 4) return;

        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string iconPath = Path.Combine(baseDir, "ChibiPet_Avatar.ico");

            // 1. Извлекаем лицо 8x8 из текстуры скина Minecraft
            using var face8 = ExtractAvatarFace(skin);

            // 2. Генерируем multi-size .ICO файл (256, 128, 64, 48, 32, 16)
            SaveMultiSizeIco(face8, iconPath);

            // 3. Создаем или обновляем ярлык на рабочем столе
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktopDir, "ChibiPet.lnk");

            string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                ?? Path.Combine(baseDir, "ChibiPet.exe");

            CreateOrUpdateShortcut(shortcutPath, exePath, iconPath);

            // 4. Оповещаем Windows Explorer об обновлении иконки на рабочем столе
            if (File.Exists(shortcutPath))
            {
                File.SetLastWriteTime(shortcutPath, DateTime.Now);
            }
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // Безопасно игнорируем исключения, если нет прав на запись
        }
    }

    static Bitmap ExtractAvatarFace(SkinData skin)
    {
        var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int baseIdx = ((8 + y) * 64 + (8 + x)) * 4;
                byte b = skin.Pixels[baseIdx];
                byte g = skin.Pixels[baseIdx + 1];
                byte r = skin.Pixels[baseIdx + 2];
                byte a = skin.Pixels[baseIdx + 3];

                // Накладываем второй слой (шапка/волосы/шлем: x: 40..47, y: 8..15)
                int hatIdx = ((8 + y) * 64 + (40 + x)) * 4;
                byte hatA = skin.Pixels[hatIdx + 3];
                if (hatA > 10)
                {
                    if (hatA >= 240)
                    {
                        b = skin.Pixels[hatIdx];
                        g = skin.Pixels[hatIdx + 1];
                        r = skin.Pixels[hatIdx + 2];
                        a = 255;
                    }
                    else
                    {
                        double alphaF = hatA / 255.0;
                        b = (byte)(skin.Pixels[hatIdx] * alphaF + b * (1.0 - alphaF));
                        g = (byte)(skin.Pixels[hatIdx + 1] * alphaF + g * (1.0 - alphaF));
                        r = (byte)(skin.Pixels[hatIdx + 2] * alphaF + r * (1.0 - alphaF));
                        a = 255;
                    }
                }

                bmp.SetPixel(x, y, Color.FromArgb(a, r, g, b));
            }
        }

        return bmp;
    }

    static void SaveMultiSizeIco(Bitmap face8, string outputPath)
    {
        int[] sizes = { 256, 128, 64, 48, 32, 16 };
        var pngDataList = new List<byte[]>();

        foreach (int size in sizes)
        {
            using var scaledBmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaledBmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;
                g.Clear(Color.Transparent);

                // Рисуем лицо пиксель-в-пиксель
                g.DrawImage(face8, new Rectangle(0, 0, size, size), 0, 0, 8, 8, GraphicsUnit.Pixel);
            }

            using var ms = new MemoryStream();
            scaledBmp.Save(ms, ImageFormat.Png);
            pngDataList.Add(ms.ToArray());
        }

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var bw = new BinaryWriter(fs);

        // ICONDIR Header
        bw.Write((ushort)0); // idReserved
        bw.Write((ushort)1); // idType (1 = Icon)
        bw.Write((ushort)sizes.Length); // idCount

        int offset = 6 + 16 * sizes.Length;

        // ICONDIRENTRY for each size
        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i];
            byte[] data = pngDataList[i];

            bw.Write(size == 256 ? (byte)0 : (byte)size); // bWidth
            bw.Write(size == 256 ? (byte)0 : (byte)size); // bHeight
            bw.Write((byte)0); // bColorCount
            bw.Write((byte)0); // bReserved
            bw.Write((ushort)1); // wPlanes
            bw.Write((ushort)32); // wBitCount
            bw.Write((uint)data.Length); // dwBytesInRes
            bw.Write((uint)offset); // dwImageOffset

            offset += data.Length;
        }

        // PNG payload for each size
        foreach (var data in pngDataList)
        {
            bw.Write(data);
        }
    }

    static void CreateOrUpdateShortcut(string shortcutPath, string targetExePath, string iconPath)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetExePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetExePath);
        shortcut.IconLocation = $"{iconPath},0";
        shortcut.Description = "ChibiPet — 3D Minecraft Desktop Companion";
        shortcut.Save();
    }
}
