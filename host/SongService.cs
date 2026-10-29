using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace GH3Launcher;

/* The game's songs: the setlists (through the song tool, cached until qb.pak changes), the
   launcher's notes on the songs it installed (Launcher\library.json), previews out of the FSB
   banks, and deleting. */
sealed class SongService
{
    readonly Ctx ctx;
    readonly Backups backups;
    readonly SemaphoreSlim toolLock = new(1, 1);
    JsonNode listCache;
    string listStamp;
    readonly LinkedList<(string name, byte[] data, List<FsbAudio.Stream> streams)> banks = new();

    public SongService(Ctx ctx, Backups backups) { this.ctx = ctx; this.backups = backups; }

    public bool Busy => toolLock.CurrentCount == 0;

    /* ---- the song tool: this same exe in "songtool" mode (Program.RunSongTool), on Honeycomb's
       toolkit. One at a time: two toolkit processes would fight over QBDebug\keys_user.txt. ---- */
    public void CheckToolkit()
    {
        if (!File.Exists(Path.Combine(ctx.Honeycomb, "GH-Toolkit-Core.dll")))
            throw new DirectoryNotFoundException(Lang.Pick("The Honeycomb-GUI folder (GH Toolkit) is missing: it compiles the songs. Reinstall with \"GH3 Launcher Setup\".",
                                                           "Falta la carpeta Honeycomb-GUI (GH Toolkit): compila las canciones. Reinstala con \"GH3 Launcher Setup\"."));
    }

    /* Runs the tool; every JSON line it prints goes to onEvent, the "done" one is returned. */
    public async Task<JsonNode> RunTool(string args, Action<JsonObject> onEvent = null)
    {
        await toolLock.WaitAsync();
        try
        {
            CheckToolkit();
            var psi = new ProcessStartInfo(Environment.ProcessPath, "songtool " + args)
            {
                WorkingDirectory = ctx.Honeycomb, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            psi.Environment["GH3L_HONEYCOMB"] = ctx.Honeycomb;
            psi.Environment["GH3L_LANG"] = Lang.Current;
            psi.Environment["GH3_LAUNCHER_GAME"] = ctx.Root;
            using var p = Process.Start(psi);
            var errLog = new StringBuilder();
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (errLog) errLog.AppendLine(e.Data); };
            p.BeginErrorReadLine();
            JsonNode result = null;
            string error = null;
            string line;
            while ((line = await p.StandardOutput.ReadLineAsync()) != null)
            {
                JsonObject o;
                try { o = JsonNode.Parse(line) as JsonObject; } catch { continue; }
                if (o == null) continue;
                switch ((string)o["event"])
                {
                    case "done": result = o["result"]; break;
                    case "error": error = (string)o["message"]; break;
                    default: try { onEvent?.Invoke(o); } catch (Exception ex) { ctx.Log("tool event: " + ex.Message); } break;
                }
            }
            await p.WaitForExitAsync();
            var log = errLog.ToString();
            if (log.Length > 0) File.AppendAllText(Path.Combine(ctx.Data, "songtool.log"), $"==== {DateTime.Now} {args.Split(' ')[0]}\r\n{log}");
            if (error != null) throw new InvalidOperationException(error);
            if (result == null) throw new InvalidOperationException(Lang.Pick($"The song compiler stopped without an answer (code {p.ExitCode}). See Launcher\\songtool.log.",
                                                                               $"El compilador de canciones terminó sin respuesta (código {p.ExitCode}). Revisa Launcher\\songtool.log."));
            return result;
        }
        finally { toolLock.Release(); }
    }

    static string Q(string s) => "\"" + s.TrimEnd('\\') + "\"";

    /* ---- the launcher's own notes on installed customs ---- */
    public JsonObject Library() => Ctx.LoadJson(ctx.LibraryPath) ?? new JsonObject();
    public void SaveLibrary(JsonObject lib) => Ctx.SaveJson(ctx.LibraryPath, lib);

    string QbStamp()
    {
        var f = new FileInfo(ctx.QbPak);
        var b = new FileInfo(ctx.QbPak.Replace(".pak.xen", ".pab.xen"));
        return $"{f.LastWriteTimeUtc.Ticks}:{f.Length}:{(b.Exists ? b.Length : 0)}";
    }

    public async Task<JsonNode> List(bool refresh)
    {
        var stamp = QbStamp();
        var cacheFile = Path.Combine(ctx.Cache, "songlist.json");
        if (!refresh && listCache != null && listStamp == stamp) return Merge(listCache);
        if (!refresh && Ctx.LoadJson(cacheFile) is JsonObject cached && (string)cached["stamp"] == stamp)
        {
            listCache = cached["list"]; listStamp = stamp;
            return Merge(listCache);
        }
        var list = await RunTool($"list --game {Q(ctx.Root)}");
        listCache = list; listStamp = stamp;
        try { Ctx.SaveJson(cacheFile, new JsonObject { ["stamp"] = stamp, ["list"] = list.DeepClone() }); } catch { }
        return Merge(list);
    }

    JsonNode Merge(JsonNode list)
    {
        var o = list.DeepClone().AsObject();
        o["library"] = Library();
        return o;
    }

    public void Invalidate() { listCache = null; listStamp = null; }

    public async Task<JsonNode> Delete(string[] names)
    {
        if (ctx.GameRunning()) throw new InvalidOperationException("Cierra Guitar Hero III antes de eliminar canciones.");
        if (names.Length == 0) throw new ArgumentException("Nada que eliminar");
        backups.BeforeChange("eliminar: " + string.Join(", ", names));
        var r = await RunTool($"delete --game {Q(ctx.Root)} --songs {string.Join(",", names.Select(n => n.Replace(",", "")))}");
        var lib = Library();
        foreach (var n in names)
        {
            if (lib[n] is JsonObject e && (string)e["cover"] is string c)
                try { File.Delete(Path.Combine(ctx.Covers, Path.GetFileName(c))); } catch { }
            lib.Remove(n);
        }
        SaveLibrary(lib);
        Invalidate();
        lock (banks) banks.Clear();
        return r;
    }

    /* The Download list as the page edited it: {tiers: [{title, songs}], singers: {name: male|female|bret}}
       (the same songs, in divisions). */
    public async Task<JsonNode> Layout(JsonObject layout)
    {
        if (ctx.GameRunning()) throw new InvalidOperationException("Cierra Guitar Hero III antes de cambiar la lista.");
        if (layout["tiers"] is not JsonArray { Count: > 0 }) throw new ArgumentException("Lista vacía");
        backups.BeforeChange("editar lista Download");
        var path = Path.Combine(ctx.Cache, "layout.json");
        Ctx.SaveJson(path, layout);
        try { return await RunTool($"layout --game {Q(ctx.Root)} --job {Q(path)}"); }
        finally { Invalidate(); }
    }

    /* ---- cheats and unlocks (scripts\guitar\guitar_memcard.qb gets gh3launcher_unlocks) ---- */
    public async Task<object> Cheats()
    {
        var r = await RunTool($"cheats --game {Q(ctx.Root)}");
        return new { list = r, options = ctx.Section("unlocks") };
    }

    public async Task<object> ApplyUnlocks(JsonObject options)
    {
        if (ctx.GameRunning()) throw new InvalidOperationException("Cierra Guitar Hero III antes de aplicar desbloqueos.");
        backups.BeforeChange("trucos y desbloqueos");
        var path = Path.Combine(ctx.Cache, "unlocks.json");
        Ctx.SaveJson(path, options);
        try
        {
            var r = await RunTool($"unlocks --game {Q(ctx.Root)} --options {Q(path)}");
            var cfg = ctx.Section("unlocks");
            foreach (var (k, v) in options) cfg[k] = v?.DeepClone();
            ctx.SaveConfig();
            Invalidate();
            return new { result = r, options = cfg };
        }
        catch
        {
            backups.RestoreLatestQb();   // a half-applied patch never stays in the game
            Invalidate();
            throw;
        }
    }

    /* ---- previews: a stem out of DATA\MUSIC\<name>.fsb.xen ("preview", "guitar", "rhythm", "song") ---- */
    public byte[] Stem(string name, string stem)
    {
        name = Path.GetFileName(name);
        (string name, byte[] data, List<FsbAudio.Stream> streams) bank = default;
        lock (banks)
        {
            var hit = banks.FirstOrDefault(b => b.name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (hit.data != null) { banks.Remove(hit); banks.AddFirst(hit); bank = hit; }
        }
        if (bank.data == null)
        {
            var path = Path.Combine(ctx.Music, name + ".fsb.xen");
            if (!File.Exists(path)) return null;
            var data = FsbAudio.Decrypt(File.ReadAllBytes(path));
            bank = (name, data, FsbAudio.Parse(data));
            lock (banks)
            {
                banks.AddFirst(bank);
                while (banks.Count > 3) banks.RemoveLast();
            }
        }
        var s = bank.streams.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x.Name).EndsWith("_" + stem, StringComparison.OrdinalIgnoreCase))
                ?? (stem == "preview" ? bank.streams.LastOrDefault() : null);
        if (s == null) return null;
        return bank.data.AsSpan(s.Offset, Math.Min(s.Length, bank.data.Length - s.Offset)).ToArray();
    }

    /* The stems as files under Launcher\cache\audio (served as https://gh3.cache/audio/...: a
       folder mapping, which answers the range requests <audio> makes). */
    public List<string> Prepare(string name, IEnumerable<string> stems)
    {
        var dir = Path.Combine(ctx.Cache, "audio");
        Directory.CreateDirectory(dir);
        var urls = new List<string>();
        name = Path.GetFileName(name);
        foreach (var stem in stems)
        {
            var safe = new string(stem.Where(char.IsLetterOrDigit).ToArray());
            var file = $"{name}_{safe}.mp3";
            var path = Path.Combine(dir, file);
            if (!File.Exists(path))
            {
                var bytes = Stem(name, safe);
                if (bytes == null) continue;
                File.WriteAllBytes(path + ".tmp", bytes);
                File.Move(path + ".tmp", path, true);
            }
            urls.Add("https://gh3.cache/audio/" + Uri.EscapeDataString(file));
        }
        return urls;
    }

    /* ---- the game's own menu music: the six tracks of Menu_Music_SE_container (global_sound_events.qb),
       menu_music_<id>.mp3 in DATA\STREAMS\streamall.fsb.xen (a plain FSB3; XORed like the song
       banks if a copy ever is). Served from Launcher\cache\audio like the previews. ---- */
    static readonly (string id, string title, string artist)[] MenuTracks =
    {
        ("stones", "Paint It Black", "The Rolling Stones"),
        ("rage", "Bulls on Parade", "Rage Against the Machine"),
        ("gnr", "Welcome to the Jungle", "Guns N' Roses"),
        ("evenflow", "Even Flow", "Pearl Jam"),
        ("monsters", "Monsters", "Matchbook Romance"),
        ("beforeiforget", "Before I Forget", "Slipknot")
    };

    public List<object> MenuMusic()
    {
        var dir = Path.Combine(ctx.Cache, "audio");
        Directory.CreateDirectory(dir);
        var bank = Path.Combine(ctx.Root, "DATA", "STREAMS", "streamall.fsb.xen");
        List<FsbAudio.Stream> streams = null;
        bool encrypted = false;
        var list = new List<object>();
        foreach (var (id, title, artist) in MenuTracks)
        {
            var file = Path.Combine(dir, "menu_" + id + ".mp3");
            if (!File.Exists(file))
            {
                if (!File.Exists(bank)) continue;
                using var fs = File.OpenRead(bank);
                if (streams == null)
                {
                    var head = new byte[24];
                    fs.ReadExactly(head);
                    encrypted = Encoding.ASCII.GetString(head, 0, 4) != "FSB3";
                    if (encrypted) head = FsbAudio.Decrypt(head);
                    int dirLen = BitConverter.ToInt32(head, 8);
                    if (dirLen <= 0 || dirLen > 16 << 20) continue;
                    var all = new byte[24 + dirLen];
                    fs.Position = 0;
                    fs.ReadExactly(all);
                    streams = FsbAudio.Parse(encrypted ? FsbAudio.Decrypt(all) : all);
                }
                var s = streams.FirstOrDefault(x => x.Name.Equals("menu_music_" + id + ".mp3", StringComparison.OrdinalIgnoreCase));
                if (s == null || s.Offset + (long)s.Length > fs.Length) continue;
                var data = new byte[s.Length];
                fs.Position = s.Offset;
                fs.ReadExactly(data);
                if (encrypted) data = FsbAudio.DecryptAt(data, s.Offset);
                File.WriteAllBytes(file + ".tmp", data);
                File.Move(file + ".tmp", file, true);
            }
            list.Add(new { id = "menu:" + id, title, artist, url = "https://gh3.cache/audio/" + Uri.EscapeDataString(Path.GetFileName(file)) });
        }
        return list;
    }

    public void ClearAudioCache()
    {
        try { var d = Path.Combine(ctx.Cache, "audio"); if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
    }

    public List<string> Stems(string name)
    {
        Stem(name, "preview");
        lock (banks)
        {
            var b = banks.FirstOrDefault(x => x.name.Equals(Path.GetFileName(name), StringComparison.OrdinalIgnoreCase));
            return b.streams?.Select(s => Path.GetFileNameWithoutExtension(s.Name)).Select(n => n[(n.LastIndexOf('_') + 1)..]).ToList() ?? new();
        }
    }
}
