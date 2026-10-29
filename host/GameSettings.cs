using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace GH3Launcher;

/* GH3's own settings: AspyrConfig.xml (video, detail, audio sync), GH3+'s settings.ini in the game
   folder, and the GH3+ plugins in PLUGINS\ (a plugin is turned off by moving it to PLUGINS\Disabled). */
sealed class GameSettings
{
    readonly Ctx ctx;
    public GameSettings(Ctx ctx) { this.ctx = ctx; }

    /* ---- AspyrConfig.xml: <r><s id="Video.Width">1920</s>...</r> ---- */
    static readonly string[] AspyrKeys =
    {
        "Video.Width", "Video.Height", "Video.MultiSampleType", "Video.MultiSampleQuality", "Debugging.FullScreen",
        "Options.GraphicsQuality", "Options.Crowd", "Options.Physics", "Options.Flares", "Options.FrontRowCamera",
        "Sound.SongSkew", "Sound.DspBufferLength", "Sound.DspNumBuffers", "Sound.StreamBufferSize",
        "Sound.StreamDecodeBufferSize", "Sound.SongSyncInterval"
    };

    public Dictionary<string, string> ReadAspyr()
    {
        var d = new Dictionary<string, string>();
        try
        {
            if (!File.Exists(ctx.AspyrConfig)) return d;
            foreach (var s in XDocument.Load(ctx.AspyrConfig).Root.Elements("s"))
                d[(string)s.Attribute("id")] = s.Value;
        }
        catch (Exception ex) { ctx.Log("aspyr read: " + ex.Message); }
        return d;
    }

    public void WriteAspyr(JsonObject values)
    {
        XDocument doc;
        try { doc = File.Exists(ctx.AspyrConfig) ? XDocument.Load(ctx.AspyrConfig) : null; } catch { doc = null; }
        doc ??= new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("r"));
        foreach (var (key, node) in values)
        {
            if (!AspyrKeys.Contains(key)) continue;
            var el = doc.Root.Elements("s").FirstOrDefault(e => (string)e.Attribute("id") == key);
            if (node == null) { el?.Remove(); continue; }
            var v = node.ToString();
            if (el == null) doc.Root.Add(new XElement("s", new XAttribute("id", key), v));
            else el.Value = v;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ctx.AspyrConfig));
        var tmp = ctx.AspyrConfig + ".tmp";
        using (var w = System.Xml.XmlWriter.Create(tmp, new System.Xml.XmlWriterSettings { Indent = true, IndentChars = "\t", Encoding = new System.Text.UTF8Encoding(false) }))
            doc.Save(w);
        File.Move(tmp, ctx.AspyrConfig, true);
    }

    /* ---- GH3+ settings.ini (read by FastGH3.dll and NoteLimitFix.dll at startup) ---- */
    string PlusIni => Path.Combine(ctx.Root, "settings.ini");
    static readonly (string section, string key)[] PlusKeys =
        { ("GFX", "Windowed"), ("GFX", "Borderless"), ("GFX", "VSync"), ("Player", "MaxFPS"), ("Player", "MaxNotes") };

    public Dictionary<string, string> ReadPlus()
    {
        var ini = IniFile.Load(PlusIni);
        var d = new Dictionary<string, string>();
        foreach (var (s, k) in PlusKeys) d[$"{s}.{k}"] = ini.Get(s, k);
        return d;
    }

    public void WritePlus(JsonObject values)
    {
        var ini = IniFile.Load(PlusIni);
        foreach (var (s, k) in PlusKeys)
        {
            if (!values.TryGetPropertyValue($"{s}.{k}", out var node)) continue;
            var v = node?.ToString();
            if (string.IsNullOrWhiteSpace(v)) ini.Remove(s, k);
            else ini.Set(s, k, v);
        }
        ini.Save(PlusIni);
    }

    /* ---- plugins ---- */
    static readonly Dictionary<string, (string title, string desc, bool locked)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["core.dll"] = ("Núcleo GH3+", "Carga los demás plugins. No se puede desactivar.", true),
        ["InputPlus.dll"] = ("InputPlus", "Del launcher: Teclado 2 (segundo jugador, guitarra o control) y whammy mejorado.", false),
        ["BorderlessWindow.dll"] = ("Ventana sin bordes", "Pantalla completa en modo ventana, sin cambiar la resolución del monitor.", false),
        ["FastGH3.dll"] = ("FastGH3", "Parches de FastGH3: límite de FPS, VSync y modo ventana (settings.ini).", false),
        ["NoteLimitFix.dll"] = ("Sin límite de notas", "Permite charts con muchísimas notas (customs largos).", false),
        ["SongLimitFix.dll"] = ("Sin límite de canciones", "Permite más canciones en las listas del juego.", false),
        ["TapHopoChord.dll"] = ("Taps, opens y acordes HOPO", "Notas tap/open de Clone Hero en los customs.", false),
        ["UncappedFPS.dll"] = ("FPS sin límite", "Quita el tope de 60 FPS del juego.", false),
        ["WhammyFix.dll"] = ("Whammy Fix", "Corrige el cálculo del whammy del juego.", false),
        ["WhammyBoost.dll"] = ("WhammyBoost (anterior)", "Reemplazado por InputPlus; déjalo desactivado.", false),
        ["xinput1_3.dll"] = ("Forzar todo como guitarra", "DLL XInput alternativo; con x360ce no hace falta.", false),
    };

    public List<object> Plugins()
    {
        var list = new List<object>();
        void Add(string dir, bool enabled)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*.dll"))
            {
                var name = Path.GetFileName(f);
                Known.TryGetValue(name, out var k);
                list.Add(new { name, enabled, title = k.title ?? Path.GetFileNameWithoutExtension(name), desc = k.desc ?? "", locked = k.locked, size = new FileInfo(f).Length });
            }
        }
        Add(ctx.Plugins, true);
        Add(Path.Combine(ctx.Plugins, "Disabled"), false);
        return list;
    }

    public void SetPlugin(string name, bool enabled)
    {
        name = Path.GetFileName(name);
        if (Known.TryGetValue(name, out var k) && k.locked) throw new InvalidOperationException("Este plugin no se puede desactivar.");
        if (ctx.GameRunning()) throw new InvalidOperationException("Cierra Guitar Hero III antes de cambiar plugins.");
        var off = Path.Combine(ctx.Plugins, "Disabled");
        Directory.CreateDirectory(off);
        string from = enabled ? off : ctx.Plugins, to = enabled ? ctx.Plugins : off;
        var stem = Path.GetFileNameWithoutExtension(name);
        foreach (var ext in new[] { ".dll", ".ini" })
        {
            var src = Path.Combine(from, stem + ext);
            if (File.Exists(src)) File.Move(src, Path.Combine(to, stem + ext), true);
        }
    }

    public string Language()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Aspyr\Guitar Hero III");
            return k?.GetValue("Language") as string ?? "";
        }
        catch { return ""; }
    }

    public object State()
    {
        var (modes, current, hz) = Native.DisplayModes();
        return new
        {
            aspyr = ReadAspyr(),
            plus = ReadPlus(),
            plugins = Plugins(),
            language = Language(),
            display = new { modes = modes.Select(m => new[] { m.w, m.h }), current = new[] { current.w, current.h }, hz },
            aspyrExists = File.Exists(ctx.AspyrConfig)
        };
    }
}
