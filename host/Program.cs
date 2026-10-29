using System.Diagnostics;
using System.Runtime.Loader;

namespace GH3Launcher;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // "GH3 Launcher.exe songtool <command> ...": the song tool in a child process (SongService)
        if (args.Length > 0 && args[0] == "songtool") return RunSongTool(args[1..]);

        var root = GameRoot();
        Lang.Load(root);
        if (args.Length > 0 && args[0] == "--uninstall")
        {
            ApplicationConfiguration.Initialize();
            return Uninstaller.Run(root);
        }

        // one launcher per folder: a second start brings the first one forward
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(AppContext.BaseDirectory.ToLowerInvariant())))[..16];
        using var single = new Mutex(true, "GH3-Launcher-" + key, out bool first);
        if (!first)
        {
            var me = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(me.ProcessName))
                if (p.Id != me.Id && p.MainWindowHandle != IntPtr.Zero) { Native.Front(p.MainWindowHandle); break; }
            return 0;
        }
        ApplicationConfiguration.Initialize();
        if (!File.Exists(Path.Combine(root, "GH3.exe")))
        {
            MessageBox.Show(Lang.Pick("Put \"GH3 Launcher.exe\" in the Guitar Hero III folder (next to GH3.exe), or install it with \"GH3 Launcher Setup\".",
                                      "Pon \"GH3 Launcher.exe\" en la carpeta de Guitar Hero III (junto a GH3.exe), o instálalo con \"GH3 Launcher Setup\"."),
                "GH3 Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        // the page needs the WebView2 runtime (Windows 11 has it; old Windows 10 may not)
        string webView = null;
        try { webView = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(); } catch { }
        if (string.IsNullOrEmpty(webView))
        {
            if (MessageBox.Show(Lang.Pick("GH3 Launcher needs the Microsoft Edge WebView2 Runtime, which is not installed.\n\nOpen the download page?",
                                          "GH3 Launcher necesita Microsoft Edge WebView2 Runtime, que no está instalado.\n\n¿Abrir la página de descarga?"),
                    "GH3 Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
            return 1;
        }
        Application.Run(new MainForm(new Ctx(root)));
        return 0;
    }

    /* GH3_LAUNCHER_GAME points a development build at the game folder */
    static string GameRoot()
    {
        var root = Environment.GetEnvironmentVariable("GH3_LAUNCHER_GAME");
        return string.IsNullOrEmpty(root) || !File.Exists(Path.Combine(root, "GH3.exe")) ? AppContext.BaseDirectory : root;
    }

    /* The song tool needs Honeycomb's GH-Toolkit-Core and friends, which live in Honeycomb-GUI\ and
       find their own files (Resources\, QBDebug\, ffmpeg.exe) from AppContext.BaseDirectory: both
       point there before any of the tool's code runs. */
    static int RunSongTool(string[] args)
    {
        var hc = Environment.GetEnvironmentVariable("GH3L_HONEYCOMB");
        if (string.IsNullOrEmpty(hc)) hc = Path.Combine(GameRoot(), "Honeycomb-GUI");
        hc = Path.GetFullPath(hc).TrimEnd('\\') + "\\";
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", hc);
        AssemblyLoadContext.Default.Resolving += (alc, name) =>
        {
            var dll = Path.Combine(hc, name.Name + ".dll");
            return File.Exists(dll) ? alc.LoadFromAssemblyPath(dll) : null;
        };
        if (Directory.Exists(hc)) Environment.CurrentDirectory = hc;
        return SongTool.Program.Main(args).GetAwaiter().GetResult();
    }
}
