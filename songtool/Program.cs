using System.Text;
using System.Text.Json;

namespace SongTool;

/* GH3LauncherSongTool <command> [--key value ...]
   Every line it prints on stdout is one JSON object: {"event": ..., ...}. The last one is
   {"event":"done", ...} or {"event":"error","message":...}. The toolkit's own Console output is
   redirected to stderr so it never mixes with the protocol. */
static class Program
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    static TextWriter Out;

    /* the launcher's language (GH3L_LANG): English unless "es" */
    public static string T(string en, string es) => Environment.GetEnvironmentVariable("GH3L_LANG") == "es" ? es : en;

    public static void Emit(object o)
    {
        lock (Json) { Out.WriteLine(JsonSerializer.Serialize(o, Json)); Out.Flush(); }
    }

    internal static async Task<int> Main(string[] args)
    {
        Out = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        Console.SetOut(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });
        var opts = ParseArgs(args, out var command);
        try
        {
            object result = command switch
            {
                "list" => Setlist.List(Req(opts, "game")),
                "install" => await Installer.Run(Req(opts, "game"), Req(opts, "job")),
                "delete" => Setlist.Delete(Req(opts, "game"), Req(opts, "songs").Split(',', StringSplitOptions.RemoveEmptyEntries), opts.GetValueOrDefault("files", "1") == "1"),
                "layout" => Setlist.Layout(Req(opts, "game"), Req(opts, "job")),
                "cheats" => Unlocks.List(Req(opts, "game")),
                "unlocks" => Unlocks.Apply(Req(opts, "game"), Req(opts, "options")),
                "explore" => Explore.Run(Req(opts, "game"), Req(opts, "out")),
                "find" => Explore.Find(Req(opts, "game"), Req(opts, "name"), Req(opts, "out")),
                "header" => DownloadHeader.Apply(Req(opts, "game")),
                "lipscan" => LipSync.Scan(Req(opts, "game")),   // development: the game's singing clips
                "songpak" => SongPakTest(Req(opts, "notes"), Req(opts, "out"), opts.GetValueOrDefault("game"), opts.GetValueOrDefault("singer", "male"), opts.GetValueOrDefault("vocals")),   // development: one chart -> _song.pak
                "dumpall" => Explore.DumpAll(opts.GetValueOrDefault("game", ""), Req(opts, "out"), opts.GetValueOrDefault("pak")),
                "roundtrip" => Explore.RoundTrip(Req(opts, "game"), opts.GetValueOrDefault("filter", "")),
                "art" => Explore.Art(Req(opts, "pak"), Req(opts, "out"), opts.GetValueOrDefault("filter", "")),
                "sfx" => Explore.Sfx(Req(opts, "pak"), Req(opts, "out"), Req(opts, "names")),
                "theme-apply" => Theme.Apply(Req(opts, "game"), opts.GetValueOrDefault("theme", "")),
                "theme-import" => Theme.Import(Req(opts, "src"), Req(opts, "out")),
                "theme-textures" => Theme.Textures(Req(opts, "game"), Req(opts, "theme"), Req(opts, "out")),
                "notes" => Theme.Notes(Req(opts, "game"), Req(opts, "song")),
                _ => throw new ArgumentException("Comando desconocido: " + command)
            };
            Emit(new { @event = "done", result });
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Emit(new { @event = "error", message = ex.Message, detail = ex.ToString() });
            return 1;
        }
    }

    static object SongPakTest(string notes, string outDir, string game, string singer, string vocals)
    {
        Directory.CreateDirectory(outDir);
        var lips = game == null ? null : new SongPak.LipSource(game, singer, string.IsNullOrEmpty(vocals) ? Array.Empty<string>() : vocals.Split('|'));
        var (pak, drums, clips) = SongPak.Create(notes, outDir, "testsong", 170, singer == "female" ? "female" : "Male", false, "", "GH3", lips);
        return new { pak, drums, clips };
    }

    static string Req(Dictionary<string, string> o, string key) =>
        o.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : throw new ArgumentException("Falta --" + key);

    static Dictionary<string, string> ParseArgs(string[] args, out string command)
    {
        command = args.Length > 0 ? args[0] : "";
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length; i++)
            if (args[i].StartsWith("--")) o[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "1";
        return o;
    }
}
