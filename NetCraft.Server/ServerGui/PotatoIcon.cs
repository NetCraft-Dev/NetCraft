using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using NetCraft.Game;
using NetCraft.Logging;

namespace NetCraft.Server.Gui;

//PotatoIcon --potato 的彩蛋 把窗口与任务栏图标换成一颗土豆
//只有 Windows 且开了 GUI 才会走到这里 任何一步失败只记一条日志 不能拖累开服
internal static partial class PotatoIcon
{
    //图标取自图床 原图 1024x1024 用前先缩
    private const string IconUrl = "https://ccvaults.com/assets/10.%20Items/10.%20Food/Potato.png";

    //图标边长上限 ICO 的目录项宽高各只有一字节 0 表示 256 再大就表达不出来
    private const int MaxIconSize = 256;

    //拉过一次就存到程序根目录 之后启动直接读本地 不再看网络脸色
    private static string CachePath => Path.Combine(AppPaths.BaseDirectory, "potato-icon.png");
    //单次请求超时 重试次数 与逐次拉长的间隔
    private const int RequestTimeoutSeconds = 6;
    private const int MaxAttempts = 5;
    private const int RetryDelayStepMs = 500;
    //PNG 签名 缓存对不上就当没有 免得坏文件把彩蛋永久卡死
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private const uint WmSetIcon = 0x0080;
    private const IntPtr IconSmall = 0;
    private const IntPtr IconBig = 1;
    //GCLP_HICON / GCLP_HICONSM 窗口类大小图标槽位
    private const int GclpHicon = -14;
    private const int GclpHiconSm = -34;
    //CreateIconFromResourceEx 认的图标版本号
    private const uint IconVersion = 0x00030000;
    //ICONDIR 6 字节 + ICONDIRENTRY 16 字节
    private const int IcoHeaderSize = 22;

    //Apply 拉图标并同时挂到 Avalonia 窗口与 Windows 任务栏
    //要等窗口露出来再调 那时平台句柄才有效
    public static async void Apply(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var raw = await LoadIconAsync();
            //原图 1024 装不进 ICO 先统一缩到图标尺寸
            var bytes = Resize(raw);
            ApplyToWindow(window, bytes);
            ApplyToTaskbar(window, bytes);
        }
        catch (Exception e)
        {
            Log.Warning($"Potato icon failed to load: {e.GetType().Name} {e.Message}");
        }
    }

    //LoadIconAsync 取图标字节 本地缓存优先 没有才走网络
    //网络抖动是常态 所以下载要静默重试 全挂了才把异常抛给调用方记一条日志
    private static async Task<byte[]> LoadIconAsync()
    {
        var cached = TryReadCache();
        if (cached.Length > 0) return cached;
        var bytes = await DownloadAsync().ConfigureAwait(false);
        WriteCache(bytes);
        return bytes;
    }

    //TryReadCache 读上次存下的图标 读不到或内容不对都当没有
    private static byte[] TryReadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return [];
            var bytes = File.ReadAllBytes(CachePath);
            var ok = bytes.Length > PngSignature.Length
                && bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);
            return ok ? bytes : [];
        }
        catch (Exception e)
        {
            Log.Debug($"Potato icon cache read failed: {e.GetType().Name}");
            return [];
        }
    }

    //WriteCache 存一份好让下次启动不再依赖网络 存不进去也不影响这次使用
    private static void WriteCache(byte[] bytes)
    {
        try
        {
            File.WriteAllBytes(CachePath, bytes);
        }
        catch (Exception e)
        {
            Log.Debug($"Potato icon cache write failed: {e.GetType().Name}");
        }
    }

    //DownloadAsync 拉图 失败静默重试 MaxAttempts 次 间隔逐次拉长
    private static async Task<byte[]> DownloadAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds) };
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await client.GetByteArrayAsync(IconUrl).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (attempt >= MaxAttempts) throw;
                Log.Debug($"Potato icon fetch failed on attempt {attempt}: {e.GetType().Name}");
                await Task.Delay(attempt * RetryDelayStepMs).ConfigureAwait(false);
            }
        }
    }

    //Resize 用 Avalonia 把原图缩到图标尺寸再编码回 PNG
    //这一步就是"先用 Avalonia 的 API 转换" 后面两种用法都吃这份缩小后的字节
    private static byte[] Resize(byte[] png)
    {
        using var input = new MemoryStream(png);
        using var bitmap = Bitmap.DecodeToWidth(input, MaxIconSize, BitmapInterpolationMode.HighQuality);
        using var output = new MemoryStream();
        bitmap.Save(output, (int?)null);
        return output.ToArray();
    }

    //ApplyToWindow 一份交给 Avalonia 当窗口图标
    private static void ApplyToWindow(Window window, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        window.Icon = new WindowIcon(stream);
    }

    //ApplyToTaskbar 另一份交给 WinAPI 改任务栏那颗
    //Avalonia 设的窗口图标任务栏未必采用 这里再直接发 WM_SETICON 并把窗口类的图标槽位一起换掉
    private static void ApplyToTaskbar(Window window, byte[] bytes)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;
        var icon = CreateIcon(bytes);
        if (icon == IntPtr.Zero) return;
        SendMessageW(handle, WmSetIcon, IconBig, icon);
        SendMessageW(handle, WmSetIcon, IconSmall, icon);
        SetClassLongPtrW(handle, GclpHicon, icon);
        SetClassLongPtrW(handle, GclpHiconSm, icon);
    }

    //CreateIcon 把 PNG 包成最小 ICO 结构再交给系统
    //CreateIconFromResourceEx 只认 ICONDIR + ICONDIRENTRY + 图像数据 直接喂裸 PNG 是不行的
    private static IntPtr CreateIcon(byte[] png)
    {
        int width, height;
        using (var stream = new MemoryStream(png))
        using (var bitmap = new Bitmap(stream))
        {
            width = bitmap.PixelSize.Width;
            height = bitmap.PixelSize.Height;
        }

        var buffer = new byte[IcoHeaderSize + png.Length];
        buffer[2] = 1;
        buffer[4] = 1;
        //宽高各一字节 0 代表 256
        buffer[6] = (byte)(width >= 256 ? 0 : width);
        buffer[7] = (byte)(height >= 256 ? 0 : height);
        buffer[10] = 1;
        buffer[12] = 32;
        BitConverter.GetBytes(png.Length).CopyTo(buffer, 14);
        BitConverter.GetBytes(IcoHeaderSize).CopyTo(buffer, 18);
        png.CopyTo(buffer, IcoHeaderSize);

        var pointer = Marshal.AllocHGlobal(buffer.Length);
        try
        {
            Marshal.Copy(buffer, 0, pointer, buffer.Length);
            return CreateIconFromResourceEx(pointer, (uint)buffer.Length, true, IconVersion, width, height, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr CreateIconFromResourceEx(IntPtr presbits, uint dwResSize,
        [MarshalAs(UnmanagedType.Bool)] bool fIcon, uint dwVer, int cxDesired, int cyDesired, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetClassLongPtrW", SetLastError = true)]
    private static partial IntPtr SetClassLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
