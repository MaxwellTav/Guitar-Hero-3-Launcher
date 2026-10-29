using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace GH3Launcher;

/* Controllers. Pads reach GH3 through x360ce (xinput1_3.dll next to GH3.exe), and x360ce's own app
   (x360ce_x86.exe) is what sets them up: x360ce.ini has an IG_<instance> section per DirectInput
   device (its buttons) and [Mappings] PAD1..4 says which pad each one is. The launcher reads that
   file to show the devices and what GH3 gets from them, live. The launcher changes it only when
   the player asks: removing a device, or switching how a device is played.
   How a device is played is the x360ce type GH3 gets: "guitar" (a guitar: you strum) or
   "controller" (a plain Xbox pad, which GH3 plays without strumming, with its frets on LT LB RB RT
   A). Switching moves the fret buttons between the two layouts so each physical button keeps its
   color. A real Xbox pad x360ce passes through is always a controller.
   PLUGINS\InputPlus.ini (the InputPlus plugin re-reads it every second) holds keyboard 2, a virtual
   pad on a PAD x360ce leaves free for a second player on the keyboard (guitar or controller the
   same way; keyboard 1 is GH3's own), and the whammy tuning.

   config.input = { keyboard2{ enabled, style, focusOnly, keys{action: [vk]}, labels{vk: text} },
                    whammy{...}, chordWindowMs, pulseMs } */
sealed class InputService
{
    readonly Ctx ctx;
    DirectInput di;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    DateTime lastRefresh;
    string lastPost;
    IntPtr hwnd;

    string X360Path => Path.Combine(ctx.Root, "x360ce.ini");
    string PlusIniPath => Path.Combine(ctx.Plugins, "InputPlus.ini");

    public static readonly string[] Actions = { "green", "red", "yellow", "blue", "orange", "strumUp", "strumDown", "left", "right", "start", "select", "whammy", "tilt" };
    static readonly string[] PlusNames = { "Green", "Red", "Yellow", "Blue", "Orange", "StrumUp", "StrumDown", "Left", "Right", "Start", "Select", "Whammy", "Tilt" };

    public InputService(Ctx ctx)
    {
        this.ctx = ctx;
        timer.Tick += (s, e) => Tick();
    }

    JsonObject Cfg => ctx.Section("input");

    public void Attach(IntPtr hwnd) { this.hwnd = hwnd; }

    DirectInput DI()
    {
        if (di == null)
        {
            try { di = new DirectInput(hwnd); di.Refresh(); lastRefresh = DateTime.Now; }
            catch (Exception ex) { ctx.Log("dinput: " + ex.Message); }
        }
        return di;
    }

    public List<DirectInput.Device> Connected()
    {
        var d = DI();
        if (d == null) return new();
        if ((DateTime.Now - lastRefresh).TotalSeconds > 1.5) { try { d.Refresh(); } catch (Exception ex) { ctx.Log("dinput refresh: " + ex.Message); } lastRefresh = DateTime.Now; }
        return d.Devices;
    }

    /* ---- config: defaults, and what older launchers left (they kept their own copy of every pad's
       mapping and wrote x360ce.ini from it; their keyboard guitar was player 1's) ---- */
    public void Sync()
    {
        try
        {
            var cfg = Cfg;
            foreach (var k in new[] { "devices", "primary", "keyboard", "keyboardSlot", "x360ceStamp", "styles", "productStyles" }) cfg.Remove(k);
            cfg["keyboard2"] ??= DefaultKeyboard2();
            cfg["whammy"] ??= ImportWhammy();
            cfg["chordWindowMs"] ??= 12;
            cfg["pulseMs"] ??= 35;
            ctx.SaveConfig();
        }
        catch (Exception ex) { ctx.Log("input sync: " + ex); }
    }

    JsonObject ImportWhammy()
    {
        var w = new JsonObject
        {
            ["Enabled"] = 1, ["ParkFix"] = 1, ["RestDeadzone"] = 0.10, ["Sensitivity"] = 1.5, ["Amplify"] = 1, ["MotionGain"] = 2.5,
            ["MotionSmoothMs"] = 120, ["Turbo"] = 1, ["TurboHz"] = 9, ["TurboDepth"] = 0.7, ["TurboThreshold"] = 0.05, ["DebugLog"] = 0
        };
        foreach (var path in new[] { Path.Combine(ctx.Plugins, "WhammyBoost.ini"), Path.Combine(ctx.Plugins, "Disabled", "WhammyBoost.ini"), PlusIniPath })
        {
            if (!File.Exists(path)) continue;
            var ini = IniFile.Load(path);
            foreach (var (k, v) in ini.Keys("Whammy"))
                if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) w[k] = d;
            break;
        }
        return w;
    }

    /* Keyboard 2 stays away from what GH3's own keyboard and menus use (arrows, Enter, Esc ...):
       letters for the frets, the numeric keypad (Num Lock on) for the rest. */
    static JsonObject DefaultKeyboard2()
    {
        JsonArray K(params int[] vks) { var a = new JsonArray(); foreach (var v in vks) a.Add(v); return a; }
        return new JsonObject
        {
            ["enabled"] = false, ["style"] = "guitar", ["focusOnly"] = true,
            ["keys"] = new JsonObject
            {
                ["green"] = K('A'), ["red"] = K('S'), ["yellow"] = K('D'), ["blue"] = K('F'), ["orange"] = K('G'),
                ["strumUp"] = K(0x68), ["strumDown"] = K(0x62, 0x65), ["left"] = K(0x64), ["right"] = K(0x66),
                ["start"] = K(0x6B), ["select"] = K(0x60), ["whammy"] = K(0x69), ["tilt"] = K(0x6E)
            }
        };
    }

    /* ---- x360ce.ini as x360ce's app left it ---- */
    sealed record Pad(string Id, Guid Instance, string Product, string Name, int Slot, bool PassThrough, int ControllerType, Dictionary<string, string> Keys);

    (List<Pad> pads, string[] mappings) ReadX360()
    {
        var pads = new List<Pad>();
        var mappings = new string[5];
        if (!File.Exists(X360Path)) return (pads, mappings);
        var ini = IniFile.Load(X360Path);
        for (int i = 1; i <= 4; i++) mappings[i] = (ini.Get("Mappings", "PAD" + i) ?? "").Trim();
        foreach (var sec in ini.Sections().Where(s => s.StartsWith("IG_", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in ini.Keys(sec)) keys[k] = v;
            if (!Guid.TryParse(keys.GetValueOrDefault("InstanceGuid"), out var inst) && !Guid.TryParse(sec[3..], out inst)) continue;
            var product = Guid.TryParse(keys.GetValueOrDefault("ProductGuid"), out var pg) ? pg.ToString("D") : null;
            int slot = Array.FindIndex(mappings, m => string.Equals(m, sec, StringComparison.OrdinalIgnoreCase));
            pads.Add(new Pad(sec, inst, product, keys.GetValueOrDefault("ProductName") ?? "?", Math.Max(slot, 0), keys.GetValueOrDefault("PassThrough") == "1",
                int.TryParse(keys.GetValueOrDefault("ControllerType"), out var ct) ? ct : 1, keys));
        }
        return (pads, mappings);
    }

    /* the PAD keyboard 2 plays on: the first one x360ce leaves empty, else one whose device is unplugged */
    static int KeyboardSlot(string[] mappings, List<Pad> pads, List<DirectInput.Device> connected)
    {
        for (int i = 1; i <= 4; i++) if (string.IsNullOrEmpty(mappings[i])) return i;
        for (int i = 1; i <= 4; i++)
        {
            var p = pads.FirstOrDefault(x => x.Slot == i);
            if (p == null || !connected.Any(c => c.Instance == p.Instance)) return i;
        }
        return 0;
    }

    /* xinput1_3.dll next to GH3.exe has to be x360ce's, or no pad is remapped */
    public static string DllState(string root)
    {
        var dll = Path.Combine(root, "xinput1_3.dll");
        if (!File.Exists(dll)) return "missing";
        try { return (FileVersionInfo.GetVersionInfo(dll).ProductName ?? "").Contains("Controller Emulator", StringComparison.OrdinalIgnoreCase) ? "ok" : "other"; }
        catch { return "other"; }
    }

    static bool X360Running()
    {
        foreach (var name in new[] { "x360ce", "x360ce_x86", "x360ce_x64" })
        {
            var ps = Process.GetProcessesByName(name);
            foreach (var p in ps) p.Dispose();
            if (ps.Length > 0) return true;
        }
        return false;
    }

    /* ---- how each device is played: the type x360ce gives GH3 ---- */
    static bool GuitarType(int controllerType) => controllerType is 6 or 7 or 11;

    static string StyleOf(Pad p) => p.PassThrough
        ? (p.Name.Contains("guitar", StringComparison.OrdinalIgnoreCase) ? "guitar" : "controller")   // the real device's own type
        : GuitarType(p.ControllerType) ? "guitar" : "controller";

    // the five frets in each layout, green to orange (x360ce key names)
    static readonly string[] GuitarFrets = { "A", "B", "Y", "X", "Left Shoulder" };
    static readonly string[] PadFrets = { "Left Trigger", "Left Shoulder", "Right Shoulder", "Right Trigger", "A" };

    public object SetStyle(string id, string style)
    {
        if (style != "guitar" && style != "controller") throw new ArgumentException("style: " + style);
        var (ini, enc) = OpenX360ForWrite();
        if (!ini.HasSection(id)) throw new InvalidOperationException(Lang.Pick("x360ce doesn't have that device.", "x360ce no tiene ese control."));
        if (ini.Get(id, "PassThrough") == "1")
            throw new InvalidOperationException(Lang.Pick("x360ce passes this Xbox controller through as it is: GH3 always gets it as a controller.", "x360ce pasa este control de Xbox tal cual: GH3 siempre lo recibe como control."));
        int.TryParse(ini.Get(id, "ControllerType"), out var ct);
        bool isGuitar = GuitarType(ct);
        if (isGuitar == (style == "guitar")) return State();
        // each physical button keeps its color: move the fret bindings to the other layout
        var from = isGuitar ? GuitarFrets : PadFrets;
        var to = isGuitar ? PadFrets : GuitarFrets;
        var values = from.Select(k => ini.Get(id, k) ?? "").ToArray();
        foreach (var k in from) ini.Set(id, k, "");
        for (int i = 0; i < 5; i++) ini.Set(id, to[i], values[i]);
        ini.Set(id, "ControllerType", style == "guitar" ? "7" : "1");
        SaveX360(ini, enc, "before-style");
        ctx.Log($"x360ce.ini: {id} is now a {style}");
        return State();
    }

    /* x360ce.ini to edit: the game and x360ce's app must be closed (the dll reads it when GH3
       starts; the app would write its own copy back when it saves). */
    (IniFile ini, Encoding enc) OpenX360ForWrite()
    {
        if (ctx.GameRunning()) throw new InvalidOperationException(Lang.Pick("Close Guitar Hero III first.", "Cierra Guitar Hero III primero."));
        if (X360Running()) throw new InvalidOperationException(Lang.Pick("Close x360ce first: when it saves, it would undo this change.", "Cierra x360ce primero: al guardar desharía este cambio."));
        if (!File.Exists(X360Path)) throw new InvalidOperationException(Lang.Pick("There is no x360ce.ini yet: set your controllers up in x360ce.", "Todavía no hay x360ce.ini: configura tus controles en x360ce."));
        using var r = new StreamReader(X360Path, new UTF8Encoding(false), true);
        var text = r.ReadToEnd();
        return (new IniFile(text), r.CurrentEncoding is UTF8Encoding ? new UTF8Encoding(false) : r.CurrentEncoding);   // keeps x360ce's UTF-16
    }

    void SaveX360(IniFile ini, Encoding enc, string backupTag)
    {
        Directory.CreateDirectory(ctx.Backups);
        var original = Path.Combine(ctx.Backups, "x360ce.ini.original");
        if (!File.Exists(original)) File.Copy(X360Path, original);
        File.Copy(X360Path, Path.Combine(ctx.Backups, "x360ce.ini." + backupTag), true);
        var tmp = X360Path + ".tmp";
        File.WriteAllText(tmp, ini.ToString(), enc);
        File.Move(tmp, X360Path, true);
    }

    public object SetKeyboard2(JsonObject kb)
    {
        if (Cfg["keyboard2"] is not JsonObject cur) Cfg["keyboard2"] = cur = DefaultKeyboard2();
        foreach (var k in new[] { "enabled", "style", "focusOnly", "keys", "labels" })
            if (kb.TryGetPropertyValue(k, out var v)) cur[k] = v?.DeepClone();
        ctx.SaveConfig();
        Apply();
        return State();
    }

    public void SetTuning(JsonObject d)
    {
        var cfg = Cfg;
        if (d["whammy"] is JsonObject w) cfg["whammy"] = w.DeepClone();
        foreach (var k in new[] { "chordWindowMs", "pulseMs" })
            if (d[k] is JsonValue v && v.TryGetValue<double>(out var n)) cfg[k] = n;
        ctx.SaveConfig();
        Apply();
    }

    /* ---- PLUGINS\InputPlus.ini: whammy, keyboard 2, and each PAD's strum as x360ce assigns them ---- */
    public void Apply()
    {
        var cfg = Cfg;
        var (pads, mappings) = ReadX360();
        var ini = new IniFile("; InputPlus: written by GH3 Launcher (Controls). The plugin applies changes by itself in ~1 s.\r\n; Use a decimal point: 0.10, not 0,10.");
        var w = cfg["whammy"] as JsonObject ?? ImportWhammy();
        ini.SetSection("Whammy", w.Select(kv => new KeyValuePair<string, string>(kv.Key, Num(kv.Value))));
        var kb = cfg["keyboard2"] as JsonObject ?? DefaultKeyboard2();
        int slot = (bool?)kb["enabled"] == true ? KeyboardSlot(mappings, pads, Connected()) : 0;
        var kbVals = new List<KeyValuePair<string, string>>
        {
            new("Enabled", slot > 0 ? "1" : "0"),
            new("Slot", slot.ToString()),
            new("FocusOnly", (bool?)kb["focusOnly"] == false ? "0" : "1"),
            new("BlockGameKeys", "1"),   // GH3's own keyboard never sees keyboard 2's keys
            // a guitar wants strumming; a plain gamepad (subtype 1) is played without it, frets on GH3's gamepad layout
            new("SubType", (string)kb["style"] == "controller" ? "1" : "7"),
            new("PadLayout", "LT,LB,RB,RT,A"),
            new("AutoStrum", "0")
        };
        var keys = kb["keys"] as JsonObject ?? new JsonObject();
        for (int i = 0; i < Actions.Length; i++)
        {
            var arr = keys[Actions[i]] as JsonArray;
            kbVals.Add(new(PlusNames[i], arr == null ? "" : string.Join(",", arr.Select(x => (int)x))));
        }
        ini.SetSection("Keyboard", kbVals);
        // the plugin's simulated strum (it doubled chords) is off: a controller is a real gamepad for GH3
        for (int i = 1; i <= 4; i++) ini.SetSection("Pad" + i, new Dictionary<string, string> { ["AutoStrum"] = "0" });
        ini.SetSection("AutoStrum", new Dictionary<string, string> { ["ChordWindowMs"] = Num(cfg["chordWindowMs"] ?? 12), ["PulseMs"] = Num(cfg["pulseMs"] ?? 35) });
        Directory.CreateDirectory(ctx.Plugins);
        ini.Save(PlusIniPath);
    }

    static string Num(JsonNode n)
    {
        if (n == null) return "0";
        if (n is JsonValue v && v.TryGetValue<double>(out var d)) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (n is JsonValue b && b.TryGetValue<bool>(out var bb)) return bb ? "1" : "0";
        return n.ToString();
    }

    /* ---- the page: x360ce's devices (and the connected ones x360ce doesn't have yet) ---- */
    public object State()
    {
        var connected = Connected();
        var (pads, mappings) = ReadX360();
        var devices = new List<object>();
        var known = new HashSet<Guid>();
        foreach (var p in pads)
        {
            var dev = connected.FirstOrDefault(c => c.Instance == p.Instance);
            if (dev != null) known.Add(dev.Instance);
            devices.Add(new
            {
                id = p.Id, instance = p.Instance.ToString("D"), product = p.Product, name = dev?.Name ?? p.Name, configured = true, connected = dev != null,
                pad = p.Slot, passThrough = p.PassThrough, controllerType = p.ControllerType, style = StyleOf(p),
                xinput = dev?.XInput ?? false, axes = dev?.Axes ?? 0, buttons = dev?.Buttons ?? 0, povs = dev?.Povs ?? 0, keys = p.Keys
            });
        }
        foreach (var c in connected.Where(c => !known.Contains(c.Instance)))
            devices.Add(new
            {
                id = "DI_" + c.Instance.ToString("N"), instance = c.Instance.ToString("D"), product = c.Product.ToString("D"), name = c.Name, configured = false, connected = true,
                pad = 0, passThrough = false, controllerType = 0, style = c.Name.Contains("guitar", StringComparison.OrdinalIgnoreCase) ? "guitar" : "controller",
                xinput = c.XInput, axes = c.Axes, buttons = c.Buttons, povs = c.Povs, keys = new Dictionary<string, string>()
            });
        var kb = (Cfg["keyboard2"] as JsonObject ?? DefaultKeyboard2()).DeepClone().AsObject();
        kb["slot"] = KeyboardSlot(mappings, pads, connected);
        return new
        {
            x360ce = new { app = File.Exists(Path.Combine(ctx.Root, "x360ce_x86.exe")), dll = DllState(ctx.Root), ini = File.Exists(X360Path), running = X360Running() },
            devices, keyboard2 = kb, whammy = Cfg["whammy"], chordWindowMs = Cfg["chordWindowMs"], pulseMs = Cfg["pulseMs"], gameRunning = ctx.GameRunning()
        };
    }

    /* Takes a device out of x360ce.ini: its IG_ section and the PAD it had. The file is copied to
       Launcher\backups first. x360ce's app must be closed, or its next save would write it back. */
    public object RemoveDevice(string id)
    {
        if (string.IsNullOrEmpty(id) || !id.StartsWith("IG_", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("id: " + id);
        var (ini, enc) = OpenX360ForWrite();
        if (!ini.HasSection(id)) return State();
        ini.RemoveSection(id);
        for (int i = 1; i <= 4; i++)
            if (string.Equals((ini.Get("Mappings", "PAD" + i) ?? "").Trim(), id, StringComparison.OrdinalIgnoreCase)) ini.Set("Mappings", "PAD" + i, "");
        SaveX360(ini, enc, "before-remove");
        ctx.Log("x360ce.ini: removed " + id);
        Apply();
        return State();
    }

    /* ---- live state for the page (while the Controls screen is open), by instance ---- */
    public void Watch(bool on)
    {
        if (on) { lastPost = null; DI(); timer.Start(); }
        else timer.Stop();
    }

    unsafe void Tick()
    {
        var devs = Connected();
        var list = new List<object>(devs.Count);
        foreach (var d in devs)
        {
            if (!di.Read(d, out var s)) continue;
            var axes = new[] { s.X, s.Y, s.Z, s.Rx, s.Ry, s.Rz, s.Slider[0], s.Slider[1] };
            var povs = new long[4];
            for (int i = 0; i < 4; i++) povs[i] = s.Pov[i] > 36000 ? -1 : s.Pov[i];
            var buttons = new int[Math.Clamp(d.Buttons, 0, 32)];
            for (int i = 0; i < buttons.Length; i++) buttons[i] = (s.Button[i] & 0x80) != 0 ? 1 : 0;
            list.Add(new { id = d.Instance.ToString("D"), axes, povs, buttons });
        }
        var json = System.Text.Json.JsonSerializer.Serialize(list);
        if (json == lastPost) return;
        lastPost = json;
        ctx.Post("input.state", list);
    }

    /* ---- the plugin: PLUGINS\InputPlus.dll from the exe; WhammyBoost (its predecessor) retired ---- */
    public void DeployPlugin()
    {
        if (ctx.GameRunning()) return;
        try
        {
            var target = Path.Combine(ctx.Plugins, "InputPlus.dll");
            using var res = Assembly.GetExecutingAssembly().GetManifestResourceStream("deploy/InputPlus.dll");
            if (res == null) return;
            var bytes = new byte[res.Length];
            res.ReadExactly(bytes);
            bool disabled = File.Exists(Path.Combine(ctx.Plugins, "Disabled", "InputPlus.dll"));
            var dest = disabled ? Path.Combine(ctx.Plugins, "Disabled", "InputPlus.dll") : target;
            if (!File.Exists(dest) || !SHA256.HashData(File.ReadAllBytes(dest)).AsSpan().SequenceEqual(SHA256.HashData(bytes)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.WriteAllBytes(dest, bytes);
                ctx.Log("InputPlus.dll deployed");
            }
            var wb = Path.Combine(ctx.Plugins, "WhammyBoost.dll");
            if (File.Exists(wb))
            {
                Cfg["whammy"] = ImportWhammy();
                ctx.SaveConfig();
                var off = Path.Combine(ctx.Plugins, "Disabled");
                Directory.CreateDirectory(off);
                foreach (var ext in new[] { ".dll", ".ini", ".log" })
                {
                    var f = Path.Combine(ctx.Plugins, "WhammyBoost" + ext);
                    if (File.Exists(f)) File.Move(f, Path.Combine(off, "WhammyBoost" + ext), true);
                }
                ctx.Log("WhammyBoost retired to PLUGINS\\Disabled (InputPlus replaces it)");
            }
            Apply();   // also undoes what an older launcher left there (its keyboard guitar, auto-strum by slot)
        }
        catch (Exception ex) { ctx.Log("deploy plugin: " + ex.Message); }
    }

    public void Dispose() { timer.Stop(); di?.Dispose(); di = null; }
}
