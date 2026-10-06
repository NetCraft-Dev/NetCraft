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

//PotatoIcon, the --potato easter egg, swaps the window and taskbar icon for a potato
//Reached only on Windows with the GUI enabled, any failed step logs one line and must not hinder server startup
internal static partial class PotatoIcon
{
    //Icon is taken from an image host, the original is 1024x1024 and is downscaled before use
    private const string IconUrl = "https://ccvaults.com/assets/10.%20Items/10.%20Food/Potato.png";

    //Icon side length cap, ICO directory entry width and height are one byte each and 0 means 256, anything larger cannot be expressed
    private const int MaxIconSize = 256;

    //Once fetched it is stored in the program root, later startups read it locally and no longer depend on the network
    private static string CachePath => Path.Combine(AppPaths.BaseDirectory, "potato-icon.png");
    //Single request timeout, retry count, and the progressively increasing interval
    private const int RequestTimeoutSeconds = 6;
    private const int MaxAttempts = 5;
    private const int RetryDelayStepMs = 500;
    //PNG signature, a mismatched cache is treated as absent so a corrupt file does not permanently break the easter egg
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private const uint WmSetIcon = 0x0080;
    private const IntPtr IconSmall = 0;
    private const IntPtr IconBig = 1;
    //GCLP_HICON / GCLP_HICONSM window class large and small icon slots
    private const int GclpHicon = -14;
    private const int GclpHiconSm = -34;
    //Icon version recognized by CreateIconFromResourceEx
    private const uint IconVersion = 0x00030000;
    //ICONDIR 6 bytes + ICONDIRENTRY 16 bytes
    private const int IcoHeaderSize = 22;

    //Apply fetches the icon and attaches it to both the Avalonia window and the Windows taskbar
    //Call only after the window appears, the platform handle is only valid then
    public static async void Apply(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var raw = await LoadIconAsync();
            //The 1024 original does not fit in an ICO, downscale it to the icon size first
            var bytes = Resize(raw);
            ApplyToWindow(window, bytes);
            ApplyToTaskbar(window, bytes);
        }
        catch (Exception e)
        {
            Log.Warning($"Potato icon failed to load: {e.GetType().Name} {e.Message}");
        }
    }

    //LoadIconAsync fetches the icon bytes, local cache first and network only when absent
    //Network flakiness is normal, so downloads retry silently and only a total failure throws to the caller to log one line
    private static async Task<byte[]> LoadIconAsync()
    {
        var cached = TryReadCache();
        if (cached.Length > 0) return cached;
        var bytes = await DownloadAsync().ConfigureAwait(false);
        WriteCache(bytes);
        return bytes;
    }

    //TryReadCache reads the previously stored icon, a read failure or wrong content is treated as absent
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

    //WriteCache stores a copy so the next startup no longer depends on the network, a failed write does not affect this run
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

    //DownloadAsync fetches the image, retries silently up to MaxAttempts times with a progressively increasing interval
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

    //Resize uses Avalonia to downscale the original to the icon size and re-encodes it as PNG
    //This is the "convert with the Avalonia API first" step, both later uses consume these downscaled bytes
    private static byte[] Resize(byte[] png)
    {
        using var input = new MemoryStream(png);
        using var bitmap = Bitmap.DecodeToWidth(input, MaxIconSize, BitmapInterpolationMode.HighQuality);
        using var output = new MemoryStream();
        bitmap.Save(output, (int?)null);
        return output.ToArray();
    }

    //ApplyToWindow hands one copy to Avalonia as the window icon
    private static void ApplyToWindow(Window window, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        window.Icon = new WindowIcon(stream);
    }

    //ApplyToTaskbar hands another copy to the WinAPI to change the taskbar icon
    //The taskbar does not necessarily adopt the window icon set by Avalonia, so WM_SETICON is sent directly and the window class icon slots are swapped too
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

    //CreateIcon wraps the PNG in the minimal ICO structure before handing it to the system
    //CreateIconFromResourceEx only accepts ICONDIR + ICONDIRENTRY + image data, feeding it a bare PNG does not work
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
        //Width and height are one byte each, 0 means 256
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
