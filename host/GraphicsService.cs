using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace GH3Launcher;

/* Gráficos mejorados: ReShade (post-processing for GH3's Direct3D 9) with a few of its official
   effects, in presets or one by one. Nothing ships with the launcher: turning it on downloads
   ReShade's setup from reshade.me (never run: ReShade32.dll is read out of the zip inside it) and
   crosire's shaders from GitHub into Launcher\reshade\. On the game it is d3d9.dll (ReShade32.dll)
   + ReShade.ini next to GH3.exe, which point at Launcher\reshade\ for the shaders and the preset.
   Turning it off removes those two files (only if the dll is the launcher's).

   config.graphics = { on, preset, effects{ id: { on, value } } } */
sealed class GraphicsService
{
    const string Version = "6.8.0";
    const string SetupUrl = "https://reshade.me/downloads/ReShade_Setup_" + Version + ".exe";
    static readonly string[] ShaderZips =
    {
        "https://github.com/crosire/reshade-shaders/archive/refs/heads/slim.zip",     // ReShade.fxh and the other headers
        "https://github.com/crosire/reshade-shaders/archive/refs/heads/legacy.zip"    // the effects below
    };
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    readonly Ctx ctx;
    readonly SemaphoreSlim gate = new(1, 1);
    public GraphicsService(Ctx ctx) { this.ctx = ctx; }

    string Dir => Path.Combine(ctx.Data, "reshade");
    string Dll => Path.Combine(Dir, "ReShade32.dll");
    string GameDll => Path.Combine(ctx.Root, "d3d9.dll");
    string GameIni => Path.Combine(ctx.Root, "ReShade.ini");
    string PresetPath => Path.Combine(Dir, "GH3Launcher.ini");
    JsonObject Cfg => ctx.Section("graphics");

    /* the effects: technique@file, the setting the page's slider moves, its range, and fixed extras */
    sealed record Fx(string Id, string Technique, string File, string Key, double Min, double Max, double Def, (string key, string value)[] Extra);
    static readonly Fx[] Effects =
    {
        new("light", "AmbientLight", "AmbientLight.fx", "alInt", 0, 15, 6, new[] { ("alThreshold", "15.000000"), ("AL_Dirt", "0"), ("AL_Lens", "0"), ("AL_Adaptation", "1") }),
        new("bloom", "MagicBloom", "MagicBloom.fx", "fBloom_Intensity", 0, 2, 0.6, new[] { ("fBloom_Threshold", "2.500000"), ("fDirt_Intensity", "0.000000") }),
        new("color", "Colourfulness", "Colourfulness.fx", "colourfulness", -0.5, 1.5, 0.45, Array.Empty<(string, string)>()),
        new("levels", "LevelsPlus", "LevelsPlus.fx", null, 0, 1, 1, Array.Empty<(string, string)>()),
        new("sharpen", "AdaptiveSharpen", "AdaptiveSharpen.fx", "curve_height", 0.2, 2, 0.8, Array.Empty<(string, string)>()),
        new("motion", "MotionBlur", "FakeMotionBlur.fx", "mbRecall", 0, 0.9, 0.3, Array.Empty<(string, string)>())
    };

    /* presets: which effects at what strength (null = off), and GH3's own settings */
    static readonly Dictionary<string, (Dictionary<string, double?> fx, Dictionary<string, string> game)> Presets = new()
    {
        ["remaster"] = (new() { ["light"] = 6, ["bloom"] = null, ["color"] = 0.45, ["levels"] = 1, ["sharpen"] = 0.8, ["motion"] = 0.3 },
                        new() { ["Video.MultiSampleType"] = "8", ["Options.GraphicsQuality"] = "2", ["Options.Crowd"] = "1", ["Options.Physics"] = "1", ["Options.Flares"] = "1" }),
        ["sharp"] = (new() { ["light"] = null, ["bloom"] = null, ["color"] = 0.25, ["levels"] = null, ["sharpen"] = 1.2, ["motion"] = null },
                     new() { ["Video.MultiSampleType"] = "8", ["Options.GraphicsQuality"] = "2" }),
        ["cinema"] = (new() { ["light"] = 4, ["bloom"] = 0.7, ["color"] = 0.3, ["levels"] = 1, ["sharpen"] = 0.5, ["motion"] = 0.45 },
                      new() { ["Video.MultiSampleType"] = "8", ["Options.GraphicsQuality"] = "2", ["Options.Crowd"] = "1", ["Options.Physics"] = "1", ["Options.Flares"] = "1" }),
        ["vivid"] = (new() { ["light"] = 3, ["bloom"] = 0.4, ["color"] = 0.9, ["levels"] = 1, ["sharpen"] = 0.7, ["motion"] = null },
                     new() { ["Video.MultiSampleType"] = "8", ["Options.GraphicsQuality"] = "2", ["Options.Flares"] = "1" })
    };

    bool Downloaded => File.Exists(Dll) && File.Exists(Path.Combine(Dir, "Shaders", "ReShade.fxh")) && Effects.All(e => File.Exists(Path.Combine(Dir, "Shaders", e.File)));
    bool Installed => File.Exists(GameDll) && File.Exists(Dll) && SameFile(GameDll, Dll);

    public object State()
    {
        var cfg = Cfg;
        var fx = cfg["effects"] as JsonObject;
        return new
        {
            on = Installed, downloaded = Downloaded, version = Version, preset = (string)cfg["preset"] ?? "remaster",
            otherDll = File.Exists(GameDll) && !Installed,
            effects = Effects.Select(e =>
            {
                var c = fx?[e.Id] as JsonObject;
                return new { id = e.Id, on = (bool?)c?["on"] ?? DefaultOn(e), value = (double?)c?["value"] ?? e.Def, min = e.Min, max = e.Max, slider = e.Key != null };
            }),
            gameRunning = ctx.GameRunning()
        };
    }

    static bool DefaultOn(Fx e) => Presets["remaster"].fx.TryGetValue(e.Id, out var v) && v != null;

    /* a preset: its effects into the config (and its GH3 settings, returned for the page to apply) */
    public object UsePreset(string id)
    {
        if (!Presets.TryGetValue(id, out var p)) throw new ArgumentException("preset: " + id);
        var fx = new JsonObject();
        foreach (var e in Effects)
        {
            var v = p.fx.GetValueOrDefault(e.Id);
            fx[e.Id] = new JsonObject { ["on"] = v != null, ["value"] = v ?? e.Def };
        }
        Cfg["preset"] = id;
        Cfg["effects"] = fx;
        ctx.SaveConfig();
        if (Installed) WritePreset();
        return new { state = State(), game = p.game };
    }

    public object SetEffect(string id, bool? on, double? value)
    {
        var e = Effects.FirstOrDefault(x => x.Id == id) ?? throw new ArgumentException("effect: " + id);
        var fx = ctx.Section("graphics", "effects");
        if (fx[id] is not JsonObject c) fx[id] = c = new JsonObject { ["on"] = DefaultOn(e), ["value"] = e.Def };
        if (on != null) c["on"] = on.Value;
        if (value != null) c["value"] = Math.Clamp(value.Value, e.Min, e.Max);
        Cfg["preset"] = "custom";
        ctx.SaveConfig();
        if (Installed) WritePreset();
        return State();
    }

    /* ---- on / off ---- */
    public async Task<object> Enable()
    {
        if (ctx.GameRunning()) throw new InvalidOperationException(Lang.Pick("Close Guitar Hero III first.", "Cierra Guitar Hero III primero."));
        if (File.Exists(GameDll) && !Installed)
            throw new InvalidOperationException(Lang.Pick("There is already a d3d9.dll next to GH3.exe (another mod): remove it first.", "Ya hay un d3d9.dll junto a GH3.exe (otro mod): quítalo primero."));
        await gate.WaitAsync();
        try
        {
            if (!Downloaded) await DownloadAll();
            if (Cfg["effects"] == null) UsePreset("remaster");
            WritePreset();
            WriteIni();
            File.Copy(Dll, GameDll + ".tmp", true);
            File.Move(GameDll + ".tmp", GameDll, true);
            Cfg["on"] = true;
            ctx.SaveConfig();
            ctx.Log("graphics: ReShade " + Version + " on");
            return State();
        }
        finally { gate.Release(); }
    }

    public object Disable()
    {
        if (ctx.GameRunning()) throw new InvalidOperationException(Lang.Pick("Close Guitar Hero III first.", "Cierra Guitar Hero III primero."));
        if (Installed) File.Delete(GameDll);
        foreach (var f in new[] { GameIni, Path.Combine(ctx.Root, "ReShade.log"), Path.Combine(ctx.Root, "ReShade.log1") })
            if (File.Exists(f)) File.Delete(f);
        Cfg["on"] = false;
        ctx.SaveConfig();
        ctx.Log("graphics: off");
        return State();
    }

    async Task DownloadAll()
    {
        Directory.CreateDirectory(Dir);
        ctx.Post("graphics.progress", new { stage = "reshade" });
        var setup = await Http.GetByteArrayAsync(SetupUrl);
        File.WriteAllBytes(Dll, ExtractFromSetup(setup, "ReShade32.dll"));
        ctx.Post("graphics.progress", new { stage = "shaders" });
        foreach (var url in ShaderZips)
        {
            var bytes = await Http.GetByteArrayAsync(url);
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            foreach (var e in zip.Entries)
            {
                if (e.Length == 0) continue;
                var parts = e.FullName.Split('/');
                int i = Array.FindIndex(parts, p => p is "Shaders" or "Textures");
                if (i < 0) continue;
                var rel = string.Join(Path.DirectorySeparatorChar, parts[i..]);
                if (rel.Contains("..")) continue;
                var dest = Path.Combine(Dir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                e.ExtractToFile(dest, true);
            }
        }
        if (!Downloaded) throw new InvalidOperationException(Lang.Pick("ReShade's files didn't come as expected.", "Los archivos de ReShade no llegaron como se esperaba."));
    }

    /* ReShade's setup carries its dlls in a zip appended to the exe: read it, never run it */
    static byte[] ExtractFromSetup(byte[] exe, string name)
    {
        var sig = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var nameBytes = Encoding.ASCII.GetBytes(name);
        for (int i = 0; i < exe.Length - 30; i++)
        {
            int k = exe.AsSpan(i).IndexOf(sig);
            if (k < 0) break;
            i += k;
            int nl = BitConverter.ToUInt16(exe, i + 26);
            if (i + 30 + nl > exe.Length || !exe.AsSpan(i + 30, nl).SequenceEqual(nameBytes)) continue;
            // the zip starts at its first entry, the dll: its offsets count from there
            using var zip = new ZipArchive(new MemoryStream(exe, i, exe.Length - i), ZipArchiveMode.Read);
            var entry = zip.GetEntry(name) ?? throw new InvalidDataException(name);
            using var s = entry.Open();
            using var m = new MemoryStream();
            s.CopyTo(m);
            return m.ToArray();
        }
        throw new InvalidDataException(Lang.Pick("ReShade's setup doesn't have ", "El instalador de ReShade no trae ") + name);
    }

    void WriteIni()
    {
        // paths relative to the game folder, so the folder can move
        var ini = new StringBuilder()
            .Append("[GENERAL]\r\n")
            .Append("EffectSearchPaths=.\\Launcher\\reshade\\Shaders\\**\r\n")
            .Append("TextureSearchPaths=.\\Launcher\\reshade\\Textures\\**\r\n")
            .Append("PresetPath=.\\Launcher\\reshade\\GH3Launcher.ini\r\n")
            .Append("PerformanceMode=1\r\n")
            .Append("[INPUT]\r\nKeyOverlay=36,0,0,0\r\n")
            .Append("[OVERLAY]\r\nTutorialProgress=4\r\nShowClock=0\r\nShowFPS=0\r\n");
        File.WriteAllText(GameIni, ini.ToString(), new UTF8Encoding(false));
    }

    void WritePreset()
    {
        Directory.CreateDirectory(Dir);
        var fx = Cfg["effects"] as JsonObject ?? new JsonObject();
        var on = new List<string>();
        var body = new StringBuilder();
        foreach (var e in Effects)
        {
            var c = fx[e.Id] as JsonObject;
            if (!((bool?)c?["on"] ?? DefaultOn(e))) continue;
            on.Add(e.Technique + "@" + e.File);
            body.Append('[').Append(e.File).Append("]\r\n");
            if (e.Key != null) body.Append(e.Key).Append('=').Append(((double?)c?["value"] ?? e.Def).ToString("0.000000", CultureInfo.InvariantCulture)).Append("\r\n");
            foreach (var (k, v) in e.Extra) body.Append(k).Append('=').Append(v).Append("\r\n");
            body.Append("\r\n");
        }
        var head = $"Techniques={string.Join(",", on)}\r\nTechniqueSorting={string.Join(",", on)}\r\n\r\n";
        File.WriteAllText(PresetPath, head + body, new UTF8Encoding(false));
    }

    static bool SameFile(string a, string b)
    {
        try
        {
            var fa = new FileInfo(a); var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            using var sa = fa.OpenRead(); using var sb = fb.OpenRead();
            return SHA256.HashData(sa).AsSpan().SequenceEqual(SHA256.HashData(sb));
        }
        catch { return false; }
    }
}
