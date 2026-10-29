using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace GH3Launcher;

/* "GH3 Launcher.exe --uninstall" (Apps & features runs it): takes out what "GH3 Launcher Setup" put
   in the game folder, by its list Launcher\install.json: the files it added are deleted, the ones
   it replaced come back from Launcher\backups\setup\. Also the InputPlus plugin goes and
   WhammyBoost (which it had retired) comes back. Guitar Hero III, its saves and the songs installed
   stay; the launcher's own data (settings, backups, play time) only if asked. */
static class Uninstaller
{
    public const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GH3Launcher";

    public static int Run(string root)
    {
        root = root.TrimEnd('\\');
        var data = Path.Combine(root, "Launcher");
        var deleteData = new TaskDialogVerificationCheckBox(Lang.Pick("Also delete the launcher's data (settings, backups, play time)", "Borrar también los datos del launcher (ajustes, respaldos, tiempo jugado)"));
        var page = new TaskDialogPage
        {
            Caption = "GH3 Launcher",
            Heading = Lang.Pick("Uninstall GH3 Launcher?", "¿Desinstalar GH3 Launcher?"),
            Text = Lang.Pick("This removes the launcher and what its installer added (Honeycomb, FFmpeg, x360ce if it put them there). Guitar Hero III, its saves and the songs you installed stay in the game.",
                             "Se quita el launcher y lo que agregó su instalador (Honeycomb, FFmpeg, x360ce si los puso él). Guitar Hero III, sus partidas y las canciones que instalaste se quedan en el juego."),
            Icon = TaskDialogIcon.Warning,
            Verification = deleteData,
            Buttons = { TaskDialogButton.Yes, TaskDialogButton.Cancel }
        };
        if (TaskDialog.ShowDialog(page) != TaskDialogButton.Yes) return 0;
        if (Native.FindGame(root) != 0)
        {
            MessageBox.Show(Lang.Pick("Close Guitar Hero III first.", "Cierra Guitar Hero III primero."), "GH3 Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }
        var errors = new List<string>();
        void Try(Action a, string what) { try { a(); } catch (Exception ex) { errors.Add(what + ": " + ex.Message); } }

        // the plugin the launcher put in PLUGINS\, and the one it retired
        var plugins = Path.Combine(root, "PLUGINS");
        foreach (var f in new[] { "InputPlus.dll", "InputPlus.ini", "inputplus.log", Path.Combine("Disabled", "InputPlus.dll") })
            Try(() => { var p = Path.Combine(plugins, f); if (File.Exists(p)) File.Delete(p); }, f);
        foreach (var ext in new[] { ".dll", ".ini", ".log" })
            Try(() =>
            {
                var off = Path.Combine(plugins, "Disabled", "WhammyBoost" + ext);
                if (File.Exists(off) && !File.Exists(Path.Combine(plugins, "WhammyBoost" + ext))) File.Move(off, Path.Combine(plugins, "WhammyBoost" + ext));
            }, "WhammyBoost" + ext);

        // Gráficos mejorados: ReShade's d3d9.dll (only the launcher's) and its ReShade.ini
        Try(() =>
        {
            var ours = Path.Combine(data, "reshade", "ReShade32.dll");
            var dll = Path.Combine(root, "d3d9.dll");
            if (File.Exists(ours) && File.Exists(dll) && new FileInfo(ours).Length == new FileInfo(dll).Length && File.ReadAllBytes(ours).AsSpan().SequenceEqual(File.ReadAllBytes(dll)))
            {
                File.Delete(dll);
                foreach (var f in new[] { "ReShade.ini", "ReShade.log" }) if (File.Exists(Path.Combine(root, f))) File.Delete(Path.Combine(root, f));
            }
        }, "d3d9.dll");

        // a theme on: the game's own global zone back (Temas kept it untouched)
        foreach (var f in new[] { "global.pab.xen", "global.pak.xen" })
            Try(() =>
            {
                var saved = Path.Combine(data, "backups", "theme", f);
                if (File.Exists(saved)) File.Copy(saved, Path.Combine(root, "DATA", "ZONES", f), true);
            }, f);

        // what the installer added or replaced
        var manifest = Ctx.LoadJson(Path.Combine(data, "install.json"));
        var self = Path.GetFullPath(Environment.ProcessPath);
        if (manifest != null)
        {
            foreach (var rel in (manifest["replaced"] as JsonArray)?.Select(x => (string)x) ?? Enumerable.Empty<string>())
                Try(() =>
                {
                    var target = Path.Combine(root, rel);
                    var saved = Path.Combine(data, "backups", "setup", rel);
                    if (File.Exists(saved)) File.Copy(saved, target, true);
                }, rel);
            foreach (var rel in (manifest["added"] as JsonArray)?.Select(x => (string)x) ?? Enumerable.Empty<string>())
                Try(() =>
                {
                    var p = Path.GetFullPath(Path.Combine(root, rel));
                    if (!p.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) || p.Equals(self, StringComparison.OrdinalIgnoreCase)) return;
                    if (File.Exists(p)) File.Delete(p);
                }, rel);
            // folders it made, deepest first, when nothing else is left in them
            foreach (var rel in ((manifest["folders"] as JsonArray)?.Select(x => (string)x) ?? Enumerable.Empty<string>()).OrderByDescending(x => x.Length))
                Try(() =>
                {
                    var p = Path.Combine(root, rel);
                    if (Directory.Exists(p) && !Directory.EnumerateFileSystemEntries(p).Any()) Directory.Delete(p);
                }, rel);
            foreach (var lnk in (manifest["shortcuts"] as JsonArray)?.Select(x => (string)x) ?? Enumerable.Empty<string>())
                Try(() => { if (File.Exists(lnk)) File.Delete(lnk); }, lnk);
        }
        Try(() => Registry.CurrentUser.DeleteSubKeyTree(RegistryKey, false), "registry");
        if (deleteData.Checked) Try(() => { if (Directory.Exists(data)) Directory.Delete(data, true); }, "Launcher\\");
        else Try(() => File.Delete(Path.Combine(data, "install.json")), "install.json");
        Try(() =>
        {
            var web = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GH3 Launcher");
            if (Directory.Exists(web)) Directory.Delete(web, true);
        }, "WebView2 data");

        // this exe can't delete itself while it runs: a moment after it ends
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & del /f /q \"{self}\"") { CreateNoWindow = true, UseShellExecute = false });
        MessageBox.Show(errors.Count == 0
                ? Lang.Pick("GH3 Launcher was uninstalled.", "GH3 Launcher se desinstaló.")
                : Lang.Pick("GH3 Launcher was uninstalled, but some things could not be removed:\n\n", "GH3 Launcher se desinstaló, pero algunas cosas no se pudieron quitar:\n\n") + string.Join("\n", errors.Take(8)),
            "GH3 Launcher", MessageBoxButtons.OK, errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        return 0;
    }
}
