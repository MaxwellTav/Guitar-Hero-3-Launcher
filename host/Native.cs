using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GH3Launcher;

/* The few Win32 calls the launcher needs. */
static class Native
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    public static void DarkTitleBar(IntPtr hwnd)
    {
        int on = 1;
        DwmSetWindowAttribute(hwnd, 20, ref on, 4);   // DWMWA_USE_IMMERSIVE_DARK_MODE
        int caption = 0x00100A0A;                       // DWMWA_CAPTION_COLOR (BGR): the launcher's near-black
        DwmSetWindowAttribute(hwnd, 35, ref caption, 4);
    }

    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    public static void Front(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        if (IsIconic(hwnd)) ShowWindow(hwnd, 9);   // SW_RESTORE
        SetForegroundWindow(hwnd);
    }

    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageNameW(IntPtr h, uint flags, StringBuilder name, ref uint size);

    public static string ImagePath(int pid)
    {
        var h = OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
        if (h == IntPtr.Zero) return "";
        try
        {
            var sb = new StringBuilder(1024);
            uint n = (uint)sb.Capacity;
            return QueryFullProcessImageNameW(h, 0, sb, ref n) ? sb.ToString() : "";
        }
        finally { CloseHandle(h); }
    }

    /* The id of a GH3.exe running from `root`, or 0. */
    public static int FindGame(string root)
    {
        var exe = Path.Combine(root, "GH3.exe");
        foreach (var p in Process.GetProcessesByName("GH3"))
            using (p)
                if (string.Equals(ImagePath(p.Id), exe, StringComparison.OrdinalIgnoreCase)) return p.Id;
        return 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettingsW(string device, int mode, ref DEVMODE dm);

    /* The primary display's resolutions (largest first) and its current one. */
    public static (List<(int w, int h)> modes, (int w, int h) current, int hz) DisplayModes()
    {
        var set = new HashSet<(int, int)>();
        var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        for (int i = 0; EnumDisplaySettingsW(null, i, ref dm); i++)
            if (dm.dmBitsPerPel >= 32 && dm.dmPelsWidth >= 800 && dm.dmPelsHeight >= 600) set.Add((dm.dmPelsWidth, dm.dmPelsHeight));
        dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        EnumDisplaySettingsW(null, -1, ref dm);   // ENUM_CURRENT_SETTINGS
        var list = set.OrderByDescending(m => m.Item1 * m.Item2).ThenByDescending(m => m.Item1).ToList();
        return (list, (dm.dmPelsWidth, dm.dmPelsHeight), dm.dmDisplayFrequency);
    }

    /* A .lnk on the user's desktop. */
    public static void CreateShortcut(string lnk, string target, string workDir, string icon, string description)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        dynamic shell = Activator.CreateInstance(type);
        try
        {
            var s = shell.CreateShortcut(lnk);
            s.TargetPath = target;
            s.WorkingDirectory = workDir;
            s.IconLocation = icon;
            s.Description = description;
            s.Save();
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
}
