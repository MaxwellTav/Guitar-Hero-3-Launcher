using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace GH3Setup;

/* The installer's two languages: English (the default) and Spanish. */
static class T
{
    public static string Lang = "en";
    public static string S(string en, string es) => Lang == "es" ? es : en;
}

/* Command line: --target <game folder> --components launcher,honeycomb,x360ce,desktop,startmenu
   --lang en|es --silent (no window: for scripts and tests) --elevated (the copy Windows started as
   administrator) --payload <zip> (development). */
sealed class Options
{
    public string Target, Lang, PayloadPath;
    public bool Silent, Elevated;
    public HashSet<string> Components = new(StringComparer.OrdinalIgnoreCase) { "launcher", "honeycomb", "x360ce", "desktop", "startmenu" };

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : "";
            switch (args[i].ToLowerInvariant())
            {
                case "--target": o.Target = Next(); break;
                case "--lang": o.Lang = Next(); break;
                case "--payload": o.PayloadPath = Next(); break;
                case "--silent": o.Silent = true; break;
                case "--elevated": o.Elevated = true; break;
                case "--components": o.Components = Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase); break;
            }
        }
        o.Components.Add("launcher");
        return o;
    }

    public string ToArgs() =>
        $"--target \"{Target}\" --components {string.Join(",", Components)} --lang {T.Lang}" + (PayloadPath != null ? $" --payload \"{PayloadPath}\"" : "");
}

/* Where Guitar Hero III may be. */
static class Detect
{
    public static bool IsGame(string dir) => !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir, "GH3.exe"));

    public static List<string> Candidates()
    {
        var found = new List<string>();
        void Add(string d)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(d)) return;
                d = Path.GetFullPath(d.Trim().Trim('"')).TrimEnd('\\');
                if (File.Exists(d)) d = Path.GetDirectoryName(d);
                if (IsGame(d) && !found.Contains(d, StringComparer.OrdinalIgnoreCase)) found.Add(d);
            }
            catch { }
        }
        Add(AppContext.BaseDirectory);
        // installed games: Add/Remove programs entries, Aspyr's own key
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using (var un = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        foreach (var name in un?.GetSubKeyNames() ?? Array.Empty<string>())
                        {
                            using var k = un.OpenSubKey(name);
                            var dn = k?.GetValue("DisplayName") as string ?? "";
                            if (!dn.Contains("Guitar Hero III", StringComparison.OrdinalIgnoreCase) && !dn.Contains("Guitar Hero 3", StringComparison.OrdinalIgnoreCase)) continue;
                            Add(k.GetValue("InstallLocation") as string);
                            Add(k.GetValue("DisplayIcon") as string);
                        }
                    foreach (var path in new[] { @"SOFTWARE\Aspyr\Guitar Hero III", @"SOFTWARE\Activision\Guitar Hero III", @"SOFTWARE\RedOctane\Guitar Hero III" })
                    {
                        using var k = root.OpenSubKey(path);
                        if (k == null) continue;
                        foreach (var v in k.GetValueNames()) if (k.GetValue(v) is string s) Add(s);
                    }
                }
                catch { }
            }
        // the usual places
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (var d in new[]
        {
            Path.Combine(pf86, "Aspyr", "Guitar Hero III"), Path.Combine(pf, "Aspyr", "Guitar Hero III"), Path.Combine(pf86, "Activision", "Guitar Hero III"),
            Path.Combine(pf86, "Guitar Hero III"), Path.Combine(pf, "Guitar Hero III"),
            Path.Combine(home, "Guitar Hero III"), Path.Combine(home, "Desktop", "Guitar Hero III"), Path.Combine(home, "Downloads", "Guitar Hero III"),
            Path.Combine(home, "Documents", "Guitar Hero III"), Path.Combine(home, "Games", "Guitar Hero III")
        }) Add(d);
        foreach (var drive in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
            foreach (var d in new[] { "Guitar Hero III", "Games\\Guitar Hero III", "Juegos\\Guitar Hero III", "GH3", "Games\\GH3" })
                Add(Path.Combine(drive.RootDirectory.FullName, d));
        return found;
    }

    public static bool Writable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".gh3setup_write_test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            var data = Path.Combine(dir, "DATA", "PAK");
            if (Directory.Exists(data))
            {
                probe = Path.Combine(data, ".gh3setup_write_test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
            }
            return true;
        }
        catch { return false; }
    }

    public static bool IsAdmin()
    {
        using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    /* Microsoft Edge WebView2 Runtime (the launcher's page) */
    public static bool HasWebView2()
    {
        const string id = @"Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        foreach (var (hive, path) in new[] { (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\" + id), (Registry.LocalMachine, @"SOFTWARE\" + id), (Registry.CurrentUser, @"SOFTWARE\" + id) })
        {
            try
            {
                using var k = hive.OpenSubKey(path);
                if (k?.GetValue("pv") is string v && v.Length > 0 && v != "0.0.0.0") return true;
            }
            catch { }
        }
        return false;
    }

    /* things that would hold files open */
    public static string Running(string dir)
    {
        foreach (var name in new[] { "GH3", "GH3 Launcher", "x360ce_x86", "Honeycomb-GUI" })
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { if (p.MainModule?.FileName?.StartsWith(dir, StringComparison.OrdinalIgnoreCase) == true) return name + ".exe"; }
                catch { }
            }
        return null;
    }
}

/* The installation itself. Files go in with the game's layout; one already there is kept aside in
   Launcher\backups\setup\ before being replaced, an identical one is left alone, the player's
   x360ce.ini is never replaced. Launcher\install.json lists what was added and replaced, for
   "GH3 Launcher.exe --uninstall". */
sealed class Installer
{
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GH3Launcher";
    readonly Payload payload;
    readonly Options o;
    readonly Action<double, string> progress;

    public Installer(Payload payload, Options o, Action<double, string> progress) { this.payload = payload; this.o = o; this.progress = progress; }

    public static string Version => typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    /* zip entry -> path in the game folder (null: not part of the chosen components) */
    string Map(ZipArchiveEntry e)
    {
        var n = e.FullName.Replace('\\', '/');
        if (n.Equals("GH3 Launcher.exe", StringComparison.OrdinalIgnoreCase)) return "GH3 Launcher.exe";
        if (n.StartsWith("Licenses/", StringComparison.OrdinalIgnoreCase)) return Path.Combine("Launcher", "Licenses", n["Licenses/".Length..].Replace('/', '\\'));
        if (n.StartsWith("Honeycomb-GUI/", StringComparison.OrdinalIgnoreCase)) return o.Components.Contains("honeycomb") ? n.Replace('/', '\\') : null;
        if (n.StartsWith("x360ce/", StringComparison.OrdinalIgnoreCase)) return o.Components.Contains("x360ce") ? n["x360ce/".Length..].Replace('/', '\\') : null;
        return null;
    }

    public long Bytes() => payload.Zip.Entries.Where(e => e.Name.Length > 0 && Map(e) != null).Sum(e => e.Length);

    public void Run()
    {
        var root = Path.GetFullPath(o.Target).TrimEnd('\\');
        if (!Detect.IsGame(root)) throw new InvalidOperationException(T.S("GH3.exe is not in that folder.", "GH3.exe no está en esa carpeta."));
        var busy = Detect.Running(root);
        if (busy != null) throw new InvalidOperationException(T.S($"Close {busy} first.", $"Cierra {busy} primero."));

        var data = Path.Combine(root, "Launcher");
        var manifestPath = Path.Combine(data, "install.json");
        JsonObject manifest = null;
        try { if (File.Exists(manifestPath)) manifest = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject; } catch { }
        var added = Set(manifest?["added"]);
        var replaced = Set(manifest?["replaced"]);
        var folders = Set(manifest?["folders"]);
        var shortcuts = Set(manifest?["shortcuts"]);

        var entries = payload.Zip.Entries.Where(e => e.Name.Length > 0).Select(e => (e, rel: Map(e))).Where(x => x.rel != null).ToList();
        long total = Math.Max(1, entries.Sum(x => x.e.Length)), done = 0;
        foreach (var (e, rel) in entries)
        {
            progress(0.02 + 0.88 * done / total, rel);
            var dest = Path.Combine(root, rel);
            // folders it makes: listed, so they can go again
            var dir = Path.GetDirectoryName(dest);
            var make = new List<string>();
            for (var d = dir; d != null && d.Length > root.Length && !Directory.Exists(d); d = Path.GetDirectoryName(d)) make.Add(d);
            foreach (var d in make) folders.Add(Path.GetRelativePath(root, d));
            Directory.CreateDirectory(dir);

            if (File.Exists(dest))
            {
                // the player's own controller mapping stays
                if (rel.Equals("x360ce.ini", StringComparison.OrdinalIgnoreCase)) { done += e.Length; continue; }
                if (new FileInfo(dest).Length == e.Length && SameContent(dest, e)) { done += e.Length; continue; }
                if (!added.Contains(rel) && !replaced.Contains(rel))
                {
                    var keep = Path.Combine(data, "backups", "setup", rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(keep));
                    if (!File.Exists(keep)) File.Copy(dest, keep);
                    replaced.Add(rel);
                }
            }
            else if (!replaced.Contains(rel)) added.Add(rel);

            var tmp = dest + ".setup_tmp";
            e.ExtractToFile(tmp, true);
            File.Move(tmp, dest, true);
            done += e.Length;
        }

        // shortcuts
        progress(0.92, T.S("Shortcuts", "Accesos directos"));
        var exe = Path.Combine(root, "GH3 Launcher.exe");
        if (o.Components.Contains("desktop"))
            shortcuts.Add(Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GH3 Launcher.lnk"), exe, root));
        if (o.Components.Contains("startmenu"))
            shortcuts.Add(Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "GH3 Launcher.lnk"), exe, root));

        // the language picked here is the launcher's too, the first time
        var config = Path.Combine(data, "config.json");
        if (!File.Exists(config))
        {
            Directory.CreateDirectory(data);
            File.WriteAllText(config, new JsonObject { ["launcher"] = new JsonObject { ["language"] = T.Lang } }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            added.Add(Path.Combine("Launcher", "config.json"));
        }

        // the list for the uninstaller, and Apps & features
        progress(0.96, T.S("Registering", "Registrando"));
        Directory.CreateDirectory(data);
        var m = new JsonObject
        {
            ["version"] = Version,
            ["installed"] = DateTime.Now.ToString("s"),
            ["added"] = Arr(added), ["replaced"] = Arr(replaced), ["folders"] = Arr(folders), ["shortcuts"] = Arr(shortcuts)
        };
        File.WriteAllText(manifestPath, m.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", "GH3 Launcher");
            k.SetValue("DisplayVersion", Version);
            k.SetValue("Publisher", "GH3 Launcher (open source)");
            k.SetValue("InstallLocation", root);
            k.SetValue("DisplayIcon", exe + ",0");
            k.SetValue("UninstallString", $"\"{exe}\" --uninstall");
            k.SetValue("URLInfoAbout", "https://github.com/");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            k.SetValue("EstimatedSize", (int)(Bytes() / 1024), RegistryValueKind.DWord);
        }
        progress(1, T.S("Done", "Listo"));
    }

    static HashSet<string> Set(JsonNode n) =>
        new((n as JsonArray)?.Select(x => (string)x).Where(x => x != null) ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

    static JsonArray Arr(IEnumerable<string> s) => new(s.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(x => (JsonNode)x).ToArray());

    static bool SameContent(string file, ZipArchiveEntry e)
    {
        using var a = File.OpenRead(file);
        using var b = e.Open();
        return SHA256.HashData(a).AsSpan().SequenceEqual(SHA256.HashData(b));
    }

    static string Shortcut(string lnk, string target, string workDir)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lnk));
        var type = Type.GetTypeFromProgID("WScript.Shell");
        dynamic shell = Activator.CreateInstance(type);
        try
        {
            var s = shell.CreateShortcut(lnk);
            s.TargetPath = target;
            s.WorkingDirectory = workDir;
            s.IconLocation = target + ",0";
            s.Description = "GH3 Launcher";
            s.Save();
        }
        finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
        return lnk;
    }
}
