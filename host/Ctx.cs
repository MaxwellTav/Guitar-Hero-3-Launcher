using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GH3Launcher;

/* Where everything lives, the launcher's own settings (Launcher\config.json), its log, and the way
   services tell the page about things (Post). */
sealed class Ctx
{
    public readonly string Root;          // the game folder: GH3.exe, DATA\, PLUGINS\, x360ce.ini ...
    public readonly string Data;          // <Root>\Launcher
    public readonly string Cache, Backups, Covers, ImportDir;
    public readonly string Honeycomb, Plugins, Music, Songs, QbPak;
    public readonly string SaveDir, AspyrConfig;
    public readonly string ConfigPath, LibraryPath;
    public JsonObject Config;
    public Action<string, object> Post = (_, _) => { };
    readonly StreamWriter log;
    readonly object configLock = new();

    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public Ctx(string root)
    {
        Root = root.TrimEnd('\\');
        Data = Path.Combine(Root, "Launcher");
        Cache = Path.Combine(Data, "cache");
        Backups = Path.Combine(Data, "backups");
        Covers = Path.Combine(Data, "covers");
        ImportDir = Path.Combine(Cache, "import");
        Honeycomb = Path.Combine(Root, "Honeycomb-GUI");
        Plugins = Path.Combine(Root, "PLUGINS");
        Music = Path.Combine(Root, "DATA", "MUSIC");
        Songs = Path.Combine(Root, "DATA", "SONGS");
        QbPak = Path.Combine(Root, "DATA", "PAK", "qb.pak.xen");
        SaveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Aspyr", "Guitar Hero III", "Save");
        AspyrConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aspyr", "Guitar Hero III", "AspyrConfig.xml");
        ConfigPath = Path.Combine(Data, "config.json");
        LibraryPath = Path.Combine(Data, "library.json");
        foreach (var d in new[] { Data, Cache, Backups, Covers, ImportDir }) Directory.CreateDirectory(d);
        try { log = new StreamWriter(Path.Combine(Data, "launcher.log"), false, new UTF8Encoding(false)) { AutoFlush = true }; } catch { }
        Config = LoadJson(ConfigPath) ?? new JsonObject();
    }

    public void Log(string text)
    {
        lock (configLock) { try { log?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {text}"); } catch { } }
    }

    public static JsonObject LoadJson(string path)
    {
        try { return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null; }
        catch { return null; }
    }

    public static void SaveJson(string path, JsonNode node)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, node.ToJsonString(Json), new UTF8Encoding(false));
        File.Move(tmp, path, true);
    }

    public void SaveConfig() { lock (configLock) SaveJson(ConfigPath, Config); }

    /* Config["a"]["b"]..., made on the way if missing. */
    public JsonObject Section(params string[] path)
    {
        lock (configLock)
        {
            JsonObject o = Config;
            foreach (var p in path)
            {
                if (o[p] is not JsonObject next) { next = new JsonObject(); o[p] = next; }
                o = next;
            }
            return o;
        }
    }

    public bool GameRunning() => Native.FindGame(Root) != 0;
}
