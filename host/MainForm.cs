using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GH3Launcher;

/* The window: one WebView2 showing the launcher's page (embedded in the exe, served as
   https://gh3.launcher/), the page's requests to the native side, and three local origins:
     https://gh3.launcher/audio/<song>/<stem>.mp3   a stem out of the game's FSB bank
     https://gh3.import/...                         songs being imported (Launcher\cache\import)
     https://gh3.covers/...                         album art of installed customs (Launcher\covers) */
sealed class MainForm : Form
{
    const string Host = "gh3.launcher";
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly Ctx ctx;
    readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(14, 10, 10) };
    readonly Dictionary<string, string> resources = new(StringComparer.OrdinalIgnoreCase);
    readonly bool debug = Environment.GetEnvironmentVariable("GH3_LAUNCHER_DEBUG") == "1";
    readonly GameService game;
    readonly GameSettings settings;
    readonly InputService input;
    readonly Backups backups;
    readonly SongService songs;
    readonly ImportService import;
    readonly GameAssets assets;
    readonly ThemeService themes;
    readonly GraphicsService graphics;
    readonly Stats stats;
    readonly string sessionStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);   // as ImportService stamps installedAt
    bool ready, fullscreen;
    Rectangle restoreBounds;
    FormWindowState restoreState;

    public MainForm(Ctx ctx)
    {
        this.ctx = ctx;
        ctx.Log($"GH3 Launcher {Application.ProductVersion} in {ctx.Root}");
        game = new GameService(ctx);
        settings = new GameSettings(ctx);
        input = new InputService(ctx);
        backups = new Backups(ctx);
        songs = new SongService(ctx, backups);
        import = new ImportService(ctx, songs, backups);
        assets = new GameAssets(ctx, songs);
        themes = new ThemeService(ctx, songs);
        graphics = new GraphicsService(ctx);
        stats = new Stats(ctx);
        Lang.Set((string)ctx.Section("launcher")["language"] ?? "en");

        Text = "Guitar Hero III";
        BackColor = Color.FromArgb(14, 10, 10);
        // the window wears the game's own icon (the exe's is the launcher's)
        try { Icon = Icon.ExtractAssociatedIcon(Path.Combine(ctx.Root, "GH3.exe")); } catch { try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { } }
        MinimumSize = new Size(1024, 640);
        StartPosition = FormStartPosition.Manual;
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        int w = (int)(wa.Width * 0.82), h = w * 9 / 16 + (Height - ClientSize.Height);
        if (h > wa.Height * 0.92) { h = (int)(wa.Height * 0.92); w = (h - (Height - ClientSize.Height)) * 16 / 9; }
        Bounds = new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2, w, h);
        Controls.Add(web);
        KeyPreview = true;

        foreach (var n in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            resources[n.Replace('\\', '/')] = n;

        ctx.Post = Post;
        game.Exited += (secs, code) => BeginInvoke(() => OnGameExit(secs, code));
        Shown += async (s, e) => await Init();
        FormClosing += (s, e) => input.Dispose();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.DarkTitleBar(Handle);
        input.Attach(Handle);
    }

    async Task Init()
    {
        if (Launcher("startFullscreen") is JsonValue v && v.TryGetValue<bool>(out var fs) && fs) SetFullscreen(true);
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GH3 Launcher", "WebView2");
        var args = "--autoplay-policy=no-user-gesture-required --disable-features=msSmartScreenProtection,msWebOOUI";
        if (debug) args += " --remote-debugging-port=9223 --remote-allow-origins=*";
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, data, new CoreWebView2EnvironmentOptions(args));
            await web.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            ctx.Log("webview2: " + ex);
            MessageBox.Show(this, Lang.Pick("This launcher needs the Microsoft Edge WebView2 Runtime.\n\n", "Este launcher necesita Microsoft Edge WebView2 Runtime.\n\n") + ex.Message, "GH3 Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }
        var core = web.CoreWebView2;
        var st = core.Settings;
        st.AreDefaultContextMenusEnabled = debug;
        st.AreDevToolsEnabled = debug;
        st.AreBrowserAcceleratorKeysEnabled = debug;
        st.IsZoomControlEnabled = false;
        st.IsStatusBarEnabled = false;
        st.IsPinchZoomEnabled = false;
        st.IsSwipeNavigationEnabled = false;
        st.IsGeneralAutofillEnabled = false;
        st.IsPasswordAutosaveEnabled = false;
        core.NewWindowRequested += (s, e) => e.Handled = true;
        core.NavigationStarting += (s, e) => { if (!e.Uri.StartsWith($"https://{Host}/")) e.Cancel = true; };

        core.SetVirtualHostNameToFolderMapping("gh3.import", ctx.ImportDir, CoreWebView2HostResourceAccessKind.Allow);
        core.SetVirtualHostNameToFolderMapping("gh3.covers", ctx.Covers, CoreWebView2HostResourceAccessKind.Allow);
        Directory.CreateDirectory(themes.Dir);
        core.SetVirtualHostNameToFolderMapping("gh3.themes", themes.Dir, CoreWebView2HostResourceAccessKind.Allow);
        songs.ClearAudioCache();
        core.SetVirtualHostNameToFolderMapping("gh3.cache", ctx.Cache, CoreWebView2HostResourceAccessKind.Allow);
        core.AddWebResourceRequestedFilter($"https://{Host}/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += Serve;
        core.WebMessageReceived += OnMessage;

        // background chores: controller profiles, the plugin in place, a session left open, the game's
        // art and sounds (first start), the song list warm
        input.Sync();
        input.DeployPlugin();
        bool running = game.Attach();
        stats.Recover(running, running ? game.Started : DateTime.Now);
        _ = Task.Run(async () =>
        {
            try { if (!assets.Ready) { await assets.Ensure(); Post("assets.ready", new { }); } await themes.EnsureGamePreview(); }
            catch (Exception ex) { ctx.Log("assets: " + ex.Message); }
            try { await songs.List(false); }
            catch (Exception ex) { ctx.Log("warmup: " + ex.Message); }
        });

        core.Navigate($"https://{Host}/index.html");
        ready = true;
    }

    JsonNode Launcher(string key) => ctx.Section("launcher")[key];

    /* ---- the page and the game's audio ---- */
    static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8", [".css"] = "text/css; charset=utf-8", [".js"] = "text/javascript; charset=utf-8",
        [".jpg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp", [".svg"] = "image/svg+xml",
        [".webm"] = "video/webm", [".mp4"] = "video/mp4", [".woff2"] = "font/woff2", [".json"] = "application/json", [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav"
    };

    void Serve(object sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var path = Uri.UnescapeDataString(new Uri(e.Request.Uri).AbsolutePath.TrimStart('/'));
        if (path.Length == 0) path = "index.html";
        var dev = Environment.GetEnvironmentVariable("GH3_LAUNCHER_WEB");
        Stream stream = null;
        // the game's art and sounds, read from the player's copy (GameAssets)
        string game = path.StartsWith("assets/art/") ? Path.Combine(assets.ArtDir, path["assets/art/".Length..])
                    : path.StartsWith("assets/sfx/") ? Path.Combine(assets.SfxDir, path["assets/sfx/".Length..]) : null;
        if (game != null && !path.Contains("..") && File.Exists(game)) stream = File.OpenRead(game);
        else if (!string.IsNullOrEmpty(dev) && File.Exists(Path.Combine(dev, path))) stream = File.OpenRead(Path.Combine(dev, path));
        else if (resources.TryGetValue("web/" + path, out var res)) stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(res);
        if (stream == null) { e.Response = NotFound(); return; }
        var ms = new MemoryStream();
        using (stream) stream.CopyTo(ms);
        ms.Position = 0;
        Types.TryGetValue(Path.GetExtension(path), out var type);
        var cache = path.StartsWith("assets/fonts/") ? "max-age=86400" : "no-store";
        e.Response = web.CoreWebView2.Environment.CreateWebResourceResponse(ms, 200, "OK", $"Content-Type: {type ?? "application/octet-stream"}\r\nCache-Control: {cache}");
    }

    CoreWebView2WebResourceResponse NotFound() => web.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "");

    /* ---- messages ---- */
    void Post(string evt, object data)
    {
        if (!ready || IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(() => Post(evt, data)); } catch { } return; }
        try { web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new { @event = evt, data }, Json)); }
        catch (Exception ex) { ctx.Log("post: " + ex.Message); }
    }

    async void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        int id = 0;
        try
        {
            var msg = JsonNode.Parse(e.WebMessageAsJson) as JsonObject;
            id = (int?)msg["id"] ?? 0;
            var type = (string)msg["type"];
            var data = msg["data"] as JsonObject ?? new JsonObject();
            object result;
            if (type == "import.drop")
            {
                // files and folders dropped on the page: their paths ride along as additional objects
                var paths = new List<string>();
                if (e.AdditionalObjects != null)
                    foreach (var o in e.AdditionalObjects)
                    {
                        if (o is CoreWebView2File f && !string.IsNullOrEmpty(f.Path)) paths.Add(f.Path);
                        else if (o is CoreWebView2FileSystemHandle h && !string.IsNullOrEmpty(h.Path)) paths.Add(h.Path);
                    }
                result = new { added = await import.Add(paths) };
            }
            else result = await Request(type, data);
            if (id != 0) web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, ok = true, result }, Json));
        }
        catch (Exception ex)
        {
            ctx.Log("request failed: " + ex);
            if (id != 0) web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, ok = false, error = ex.Message }, Json));
        }
    }

    static string Str(JsonObject d, string key, string def = "") => d[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : def;
    static bool Bool(JsonObject d, string key) => d[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    static string[] Strs(JsonObject d, string key) => (d[key] as JsonArray)?.Select(x => (string)x).Where(x => x != null).ToArray() ?? Array.Empty<string>();

    async Task<object> Request(string type, JsonObject d)
    {
        switch (type)
        {
            case "log": ctx.Log("page: " + Str(d, "text")); return true;
            case "app.state": return AppState();
            case "app.quit": BeginInvoke(Close); return true;
            case "window.fullscreen": SetFullscreen(Bool(d, "toggle") ? !fullscreen : Bool(d, "on")); return fullscreen;
            case "window.minimize": WindowState = FormWindowState.Minimized; return true;
            case "launcher.save":
                {
                    var l = ctx.Section("launcher");
                    if (d["settings"] is JsonObject s) foreach (var (k, v) in s) l[k] = v?.DeepClone();
                    ctx.SaveConfig();
                    Lang.Set((string)l["language"] ?? "en");
                    return l;
                }
            case "health.state":
                return Health.Check(ctx, assets).Select(i => new { id = i.Id, level = i.Level, title = Lang.Pick(i.En, i.Es), detail = Lang.Pick(i.DetailEn, i.DetailEs), fix = i.Fix });
            case "assets.ensure":
                assets.Clear();
                await assets.Ensure();
                return assets.Ready;
            case "stats.state": return stats.State(game.Running ? (DateTime.Now - game.Started).TotalSeconds : 0);
            case "app.restartAdmin":
                {
                    Process.Start(new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = true, Verb = "runas", WorkingDirectory = ctx.Root });
                    BeginInvoke(Close);
                    return true;
                }

            case "game.launch": return Launch();
            case "game.running": return game.Running;   // also picks up a GH3 started outside the launcher
            case "game.kill": game.Kill(); return true;
            case "game.front": game.Front(); return true;

            case "songs.list": return await songs.List(Bool(d, "refresh"));
            case "songs.delete":
                {
                    var r = await songs.Delete(Strs(d, "names"));
                    Post("songs.changed", new { });
                    return r;
                }
            case "songs.layout":
                {
                    var r = await songs.Layout(d["layout"] as JsonObject ?? new JsonObject());
                    Post("songs.changed", new { });
                    return r;
                }
            case "cheats.state": return await songs.Cheats();
            case "cheats.apply": return await songs.ApplyUnlocks(d["options"] as JsonObject ?? new JsonObject());
            case "songs.stems": return await Task.Run(() => songs.Stems(Str(d, "name")));
            case "songs.prepare": { var name = Str(d, "name"); var stems = Strs(d, "stems"); return await Task.Run(() => songs.Prepare(name, stems)); }
            case "menu.music": return await Task.Run(() => songs.MenuMusic());

            case "import.list": return import.List();
            case "import.pick": return new { added = await import.Add(Pick(Str(d, "kind"))) };
            case "graphics.state": return graphics.State();
            case "graphics.enable": return await graphics.Enable();
            case "graphics.disable": return graphics.Disable();
            case "graphics.preset": return graphics.UsePreset(Str(d, "id"));
            case "graphics.effect": return graphics.SetEffect(Str(d, "id"), d["on"] is JsonValue fxOn ? (bool?)fxOn : null, d["value"] is JsonValue fxValue ? (double?)fxValue : null);
            case "theme.state": return themes.State();
            case "theme.apply": return await themes.Apply(Str(d, "id"));
            case "theme.import": { var f = Pick("theme"); return f.Length == 0 ? themes.State() : await themes.Import(f[0]); }
            case "theme.download": return await themes.Download(Str(d, "id"));
            case "theme.remove": return themes.Remove(Str(d, "id"));
            case "theme.notes": return await themes.Notes(Str(d, "song"));
            case "import.add": return new { added = await import.Add(Strs(d, "paths")) };
            case "import.update": return import.Update(Str(d, "id"), d["fields"] as JsonObject ?? new JsonObject());
            case "import.updateMany": return import.UpdateMany(Strs(d, "ids"), d["fields"] as JsonObject ?? new JsonObject());
            case "import.remove": import.Remove(Strs(d, "ids")); return true;
            case "import.clearDone": import.ClearDone(); return true;
            case "import.install": return await import.Install(Strs(d, "ids"), Bool(d, "replace"));

            case "input.state": return input.State();
            case "input.watch": input.Watch(Bool(d, "on")); return true;
            case "input.style": return input.SetStyle(Str(d, "id"), Str(d, "style"));
            case "input.keyboard2": return input.SetKeyboard2(d["keyboard2"] as JsonObject ?? new JsonObject());
            case "input.tuning": input.SetTuning(d); return true;
            case "input.remove": return input.RemoveDevice(Str(d, "id"));

            case "settings.state": return settings.State();
            case "settings.aspyr": settings.WriteAspyr(d["values"] as JsonObject ?? new JsonObject()); return settings.State();
            case "settings.plus": settings.WritePlus(d["values"] as JsonObject ?? new JsonObject()); return settings.State();
            case "plugins.set": settings.SetPlugin(Str(d, "name"), Bool(d, "enabled")); return settings.State();

            case "backups.list": return backups.List();
            case "backups.restore":
                backups.Restore(Str(d, "kind"), Str(d, "id"));
                songs.Invalidate();
                Post("songs.changed", new { });
                return backups.List();
            case "backups.saveNow": backups.BackupSave(null, "manual"); return backups.List();

            case "tools.run": return RunTool(Str(d, "id"));
            default: throw new NotSupportedException("Unknown request: " + type);
        }
    }

    object AppState()
    {
        bool running = game.Running;
        return new
        {
            root = ctx.Root,
            version = Application.ProductVersion,
            pathTooLong = ctx.Root.Length >= 46,
            honeycomb = Directory.Exists(ctx.Honeycomb),
            game = new { running, seconds = running ? (DateTime.Now - game.Started).TotalSeconds : 0 },
            launcher = ctx.Section("launcher"),
            language = Lang.Current,
            sessionStart,   // songs installed since then are this session's new ones
            assetsReady = assets.Ready,
            assetsError = assets.Error,
            busy = songs.Busy || import.Installing,
            user = Environment.UserName,
            tools = new
            {
                // what the Tools page can offer in this folder (BetterGH3 brings some of them)
                honeycombGui = File.Exists(Path.Combine(ctx.Honeycomb, "Honeycomb-GUI.exe")),
                x360ce = File.Exists(Path.Combine(ctx.Root, "x360ce_x86.exe")),
                fixes = Directory.Exists(Path.Combine(ctx.Root, "_Optional fixes")) && File.Exists(Path.Combine(ctx.Root, "_Optional fixes", "Fix startup issues.bat")),
                onlineAccount = File.Exists(Path.Combine(ctx.Root, "_MAKE AN ONLINE ACCOUNT HERE_.url"))
            }
        };
    }

    object Launch()
    {
        if (import.Installing) throw new InvalidOperationException(Lang.Pick("Wait until the songs finish installing.", "Espera a que termine la instalación de canciones."));
        input.Watch(false);
        try { input.Apply(); } catch (Exception ex) { ctx.Log("apply input: " + ex.Message); }
        int pid = game.Launch(Launcher("highPriority") is JsonValue hp && hp.TryGetValue<bool>(out var h) && h);
        stats.Started(DateTime.Now);
        Post("game.started", new { pid });
        var onPlay = (string)Launcher("onPlay") ?? "minimize";
        if (onPlay == "minimize") _ = MinimizeSoon();
        else if (onPlay == "close") _ = CloseSoon();
        return new { pid };
    }

    async Task MinimizeSoon()
    {
        await Task.Delay(1200);
        if (game.Running) { if (fullscreen) SetFullscreen(false); WindowState = FormWindowState.Minimized; }
    }

    async Task CloseSoon()
    {
        await Task.Delay(1500);
        if (game.Running) Close();
    }

    void OnGameExit(double secs, int code)
    {
        stats.Ended(secs);
        Post("game.exited", new { seconds = secs, code });
        if (WindowState == FormWindowState.Minimized) WindowState = restoreState == FormWindowState.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
        Activate();
        Native.Front(Handle);
    }

    string[] Pick(string kind)
    {
        if (kind == "theme")
        {
            using var tfd = new OpenFileDialog
            {
                Title = Lang.Pick("A GH3 / FastGH3 theme", "Un tema de GH3 / FastGH3"),
                Filter = Lang.Pick("Themes", "Temas") + " (*.zip;*.gfx;*.tex;*.xen)|*.zip;*.gfx;*.tex;*.xen|" + Lang.Pick("All files", "Todos los archivos") + "|*.*"
            };
            return tfd.ShowDialog(this) == DialogResult.OK ? new[] { tfd.FileName } : Array.Empty<string>();
        }
        if (kind == "folder")
        {
            using var dlg = new FolderBrowserDialog { Description = Lang.Pick("Folder with Clone Hero songs", "Carpeta con canciones de Clone Hero"), UseDescriptionForTitle = true, ShowNewFolderButton = false };
            return dlg.ShowDialog(this) == DialogResult.OK ? new[] { dlg.SelectedPath } : Array.Empty<string>();
        }
        using var ofd = new OpenFileDialog
        {
            Title = Lang.Pick("Songs to import", "Canciones para importar"),
            Multiselect = true,
            Filter = Lang.Pick("Songs", "Canciones") + " (*.sng;*.zip;*.7z;*.rar;song.ini;notes.chart;notes.mid)|*.sng;*.zip;*.7z;*.rar;song.ini;notes.chart;notes.mid|"
                     + Lang.Pick("All files", "Todos los archivos") + "|*.*"
        };
        return ofd.ShowDialog(this) == DialogResult.OK ? ofd.FileNames : Array.Empty<string>();
    }

    object RunTool(string id)
    {
        string fixes = Path.Combine(ctx.Root, "_Optional fixes");
        void Shell(string file, string args = "", string dir = null)
        {
            if (!File.Exists(file) && !Directory.Exists(file)) throw new FileNotFoundException(Lang.Pick("Not found: ", "No existe: ") + file);
            Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true, WorkingDirectory = dir ?? Path.GetDirectoryName(file) });
        }
        void Folder(string path) { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false }); }
        switch (id)
        {
            case "honeycomb": Shell(Path.Combine(ctx.Honeycomb, "Honeycomb-GUI.exe")); break;
            case "x360ce": Shell(Path.Combine(ctx.Root, "x360ce_x86.exe"), "", ctx.Root); break;
            case "fix-startup": Shell(Path.Combine(fixes, "Fix startup issues.bat")); break;
            case "firewall": Shell(Path.Combine(fixes, "Allow through Firewall.bat")); break;
            case "gh3-shortcut": Shell(Path.Combine(fixes, "Create desktop shortcut.bat")); break;
            case "online-account": Shell(Path.Combine(ctx.Root, "_MAKE AN ONLINE ACCOUNT HERE_.url")); break;
            case "honeycomb-web": Process.Start(new ProcessStartInfo("https://github.com/AddyMills/Honeycomb-GUI/releases/latest") { UseShellExecute = true }); break;
            case "launcher-shortcut":
                {
                    var exe = Environment.ProcessPath;
                    var lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Guitar Hero III Launcher.lnk");
                    Native.CreateShortcut(lnk, exe, Path.GetDirectoryName(exe), exe + ",0", Lang.Pick("Guitar Hero III launcher", "Launcher de Guitar Hero III"));
                    return new { created = lnk };
                }
            case "uninstall":
                Process.Start(new ProcessStartInfo(Environment.ProcessPath, "--uninstall") { UseShellExecute = true, WorkingDirectory = ctx.Root });
                BeginInvoke(Close);
                break;
            case "folder:game": Folder(ctx.Root); break;
            case "folder:songs": Folder(ctx.Songs); break;
            case "folder:music": Folder(ctx.Music); break;
            case "folder:plugins": Folder(ctx.Plugins); break;
            case "folder:save": Folder(ctx.SaveDir); break;
            case "folder:backups": Folder(ctx.Backups); break;
            case "folder:launcher": Folder(ctx.Data); break;
            case "log": Shell(Path.Combine(ctx.Data, "launcher.log")); break;
            case "songtool-log": Shell(Path.Combine(ctx.Data, "songtool.log")); break;
            default: throw new NotSupportedException("Unknown tool: " + id);
        }
        return true;
    }

    void SetFullscreen(bool on)
    {
        if (on == fullscreen) return;
        fullscreen = on;
        if (on)
        {
            restoreState = WindowState; restoreBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = restoreBounds;
            WindowState = restoreState == FormWindowState.Minimized ? FormWindowState.Normal : restoreState;
        }
        Post("window.fullscreen", new { on });
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 || keyData == (Keys.Alt | Keys.Enter)) { SetFullscreen(!fullscreen); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
