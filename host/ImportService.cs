using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace GH3Launcher;

/* Importing Clone Hero songs. Whatever is dropped (.sng, .zip/.7z/.rar, folders, a song.ini) is
   unpacked or copied into Launcher\cache\import\<id>\, read (SongMeta) and listed for the page,
   which can play it (https://gh3.import/<id>/<file>) and edit it before installing. Installing
   hands a job to the song tool and records each song in Launcher\library.json. */
sealed class ImportService
{
    readonly Ctx ctx;
    readonly SongService songs;
    readonly Backups backups;
    readonly SingerLookup singers;
    readonly List<JsonObject> items = new();
    readonly object gate = new();
    int seq;
    public bool Installing { get; private set; }

    public ImportService(Ctx ctx, SongService songs, Backups backups)
    {
        this.ctx = ctx; this.songs = songs; this.backups = backups;
        singers = new SingerLookup(ctx);
        try { foreach (var d in Directory.GetDirectories(ctx.ImportDir)) Directory.Delete(d, true); } catch { }
    }

    /* The Download division a pack's songs go into: the archive's (or pack folder's) name. */
    static string TierName(string path)
    {
        var n = Directory.Exists(path) ? Path.GetFileName(path.TrimEnd('\\')) : Path.GetFileNameWithoutExtension(path);
        n = Regex.Replace(n.Replace('_', ' '), @"\s+", " ").Trim();
        return n.Length > 48 ? n[..48].Trim() : n;
    }

    public object List() { lock (gate) return items.Select(i => i.DeepClone()).ToList(); }

    string NewId() => DateTime.Now.ToString("HHmmss") + "_" + Interlocked.Increment(ref seq);

    /* ---- adding ---- */
    public async Task<int> Add(IEnumerable<string> paths)
    {
        var list = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        int added = 0;
        ctx.Post("import.scan", new { busy = true, text = "Leyendo archivos…" });
        try
        {
            var installed = await InstalledIndex();
            await Task.Run(() =>
            {
                foreach (var p in list)
                {
                    try { added += AddPath(p, installed, null); }
                    catch (Exception ex)
                    {
                        ctx.Log("import " + p + ": " + ex);
                        AddItem(new JsonObject { ["id"] = NewId(), ["source"] = p, ["title"] = Path.GetFileName(p), ["status"] = "error", ["error"] = ex.Message });
                    }
                }
            });
        }
        finally { ctx.Post("import.scan", new { busy = false }); }
        return added;
    }

    /* tier: the division the songs go into ("" = none chosen yet: "Lost and Cursed Songs"). Archives
       and folders holding several songs name their own; one inside another keeps the outer name. */
    int AddPath(string path, Dictionary<string, string> installed, string tier)
    {
        if (Directory.Exists(path))
        {
            if (tier == null && !SongMeta.IsSongFolder(path)) tier = TierName(path);
            return AddFolderTree(path, path, installed, tier ?? "");
        }
        if (!File.Exists(path)) return 0;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (ext == ".sng")
        {
            ctx.Post("import.scan", new { busy = true, text = "Abriendo " + Path.GetFileName(path) });
            var id = NewId();
            var dir = Path.Combine(ctx.ImportDir, id);
            new SngFile(path).ExtractTo(dir);
            return AddSong(dir, id, path, installed, tier ?? "") ? 1 : 0;
        }
        if (ext is ".zip" or ".7z" or ".rar")
        {
            var dir = Path.Combine(ctx.ImportDir, "arc_" + NewId());
            Extract(path, dir);
            return AddFolderTree(dir, path, installed, string.IsNullOrEmpty(tier) ? TierName(path) : tier);
        }
        if (name is "song.ini" or "notes.mid" or "notes.chart") return AddFolderTree(Path.GetDirectoryName(path), path, installed, tier ?? "");
        throw new NotSupportedException("Formato no soportado: " + Path.GetFileName(path));
    }

    void Extract(string archive, string dir)
    {
        Directory.CreateDirectory(dir);
        ctx.Post("import.scan", new { busy = true, text = "Descomprimiendo " + Path.GetFileName(archive) + "…" });
        using var arc = ArchiveFactory.OpenArchive(archive, new ReaderOptions());
        int n = 0;
        var full = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        string Target(string key)
        {
            var target = Path.GetFullPath(Path.Combine(dir, key ?? ""));
            if (!target.StartsWith(full, StringComparison.OrdinalIgnoreCase)) return null;   // a path that climbs out of the folder
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            return target;
        }
        void Progress() { if (++n % 20 == 0) ctx.Post("import.scan", new { busy = true, text = $"Descomprimiendo {Path.GetFileName(archive)}… ({n} archivos)" }); }
        // solid archives and 7z go through in one pass; zip and ordinary rar only one entry at a time
        if (arc.IsSolid || arc.Type == ArchiveType.SevenZip)
        {
            using var reader = arc.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory || Target(reader.Entry.Key) is not string target) continue;
                using (var fs = File.Create(target)) reader.WriteEntryTo(fs);
                Progress();
            }
        }
        else
        {
            foreach (var entry in arc.Entries)
            {
                if (entry.IsDirectory || Target(entry.Key) is not string target) continue;
                using (var fs = File.Create(target)) entry.WriteTo(fs);
                Progress();
            }
        }
    }

    /* Every song folder and .sng under `root` (archives inside are opened too). */
    int AddFolderTree(string root, string source, Dictionary<string, string> installed, string tier)
    {
        int n = 0;
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            if (SongMeta.IsSongFolder(dir))
            {
                var inCache = Path.GetFullPath(dir).StartsWith(Path.GetFullPath(ctx.ImportDir), StringComparison.OrdinalIgnoreCase);
                var id = NewId();
                var target = inCache ? dir : Path.Combine(ctx.ImportDir, id);
                if (!inCache) CopyFolder(dir, target);
                if (AddSong(target, id, dir == root ? source : dir, installed, tier)) n++;
                continue;
            }
            foreach (var f in Directory.GetFiles(dir))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is ".sng" or ".zip" or ".7z" or ".rar")
                    try { n += AddPath(f, installed, tier); } catch (Exception ex) { ctx.Log("import " + f + ": " + ex.Message); }
            }
            foreach (var d in Directory.GetDirectories(dir)) stack.Push(d);
        }
        return n;
    }

    static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        var lip = Path.Combine(from, "lipsync");   // the singer's .ska files, one level down
        if (Directory.Exists(lip))
            foreach (var f in Directory.GetFiles(lip, "*", SearchOption.AllDirectories))
            {
                var dest = Path.Combine(to, "lipsync", Path.GetRelativePath(lip, f));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(f, dest, true);
            }
    }

    string Url(string file)
    {
        var rel = Path.GetRelativePath(ctx.ImportDir, file).Replace('\\', '/');
        return "https://gh3.import/" + string.Join("/", rel.Split('/').Select(Uri.EscapeDataString));
    }

    bool AddSong(string dir, string id, string source, Dictionary<string, string> installed, string tier)
    {
        var m = SongMeta.Read(dir);
        // nobody sings it (no vocals stem, lyrics or vocals chart): no singer on stage, like Cliffs of Dover
        var singer = m.HasVocals ? SingerLookup.Offline(m.Artist, m.VocalGender) : "none";
        var stems = new JsonArray();
        void Stem(string kind, string f) { if (f != null) stems.Add(new JsonObject { ["kind"] = kind, ["name"] = Path.GetFileName(f), ["url"] = Url(f) }); }
        Stem("guitar", m.Guitar); Stem("rhythm", m.Rhythm);
        foreach (var b in m.Backing) Stem("backing", b);
        Stem("crowd", m.Crowd);
        installed.TryGetValue(Key(m.Title, m.Artist), out var dup);
        var item = new JsonObject
        {
            ["id"] = id, ["folder"] = dir, ["source"] = source, ["sourceName"] = Path.GetFileName(source.TrimEnd('\\')),
            ["title"] = m.Title, ["artist"] = m.Artist, ["album"] = m.Album, ["genre"] = m.Genre, ["charter"] = m.Charter,
            ["year"] = m.Year, ["diffGuitar"] = m.DiffGuitar, ["lengthMs"] = m.LengthMs, ["loadingPhrase"] = m.LoadingPhrase,
            ["previewStart"] = m.PreviewStart >= 0 ? m.PreviewStart : Math.Max(0, m.LengthMs / 3),
            ["hopoFrequency"] = m.HopoFrequency > 0 ? m.HopoFrequency : 170,
            ["notes"] = m.Notes, ["notesType"] = m.NotesType, ["hasGuitar"] = m.HasGuitar,
            ["guitar"] = m.Guitar, ["rhythm"] = m.Rhythm, ["crowd"] = m.Crowd, ["preview"] = m.Preview,
            ["backing"] = new JsonArray(m.Backing.Select(b => (JsonNode)b).ToArray()),
            ["vocals"] = new JsonArray(m.Vocals.Select(b => (JsonNode)b).ToArray()),
            ["stems"] = stems,
            ["art"] = m.Art != null ? Url(m.Art) : null, ["artPath"] = m.Art,
            ["warnings"] = new JsonArray(m.Warnings.Select(w => (JsonNode)w).ToArray()),
            ["duplicateOf"] = dup,
            ["isCover"] = false,   // Clone Hero's artist is who plays it; "as made famous by" is the user's call
            ["coverArtist"] = "",
            ["countoff"] = "HiHat01",
            ["singer"] = singer switch { "female" => "Female", "bret" => "Bret", "none" => "None", _ => "Male" },
            ["singerFrom"] = singer == "none" ? "novocals" : singer != null ? "song" : m.HasGuitar ? "looking" : "default",   // novocals, song, web, looking, default, user
            ["tier"] = tier ?? "",
            ["lipsync"] = m.Lipsync, ["lipsyncSource"] = m.LipsyncSource,
            ["selected"] = m.HasGuitar && dup == null,
            ["status"] = m.HasGuitar ? "ready" : "invalid"
        };
        AddItem(item);
        if ((string)item["singerFrom"] == "looking") _ = LookUpSinger(id, m.Artist, m.Title);
        return true;
    }

    /* Asks the internet who sings it; the answer only lands if the user hasn't picked one meanwhile. */
    async Task LookUpSinger(string id, string artist, string title)
    {
        string found = null;
        try { found = await singers.Find(artist, title); } catch (Exception ex) { ctx.Log("singer: " + ex.Message); }
        JsonObject it;
        lock (gate)
        {
            it = items.FirstOrDefault(i => (string)i["id"] == id);
            if (it == null || (string)it["singerFrom"] != "looking") return;
            it["singerFrom"] = found != null ? "web" : "default";
            if (found != null) it["singer"] = found switch { "female" => "Female", "bret" => "Bret", _ => "Male" };
            it = (JsonObject)it.DeepClone();
        }
        ctx.Post("import.item", it);
    }

    void AddItem(JsonObject item)
    {
        lock (gate) items.Add(item);
        ctx.Post("import.item", item.DeepClone());
    }

    static string Key(string title, string artist)
    {
        string N(string s) => Regex.Replace((s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD), @"[^a-z0-9]", "");
        return N(title) + "|" + N(artist);
    }

    async Task<Dictionary<string, string>> InstalledIndex()
    {
        var d = new Dictionary<string, string>();
        try
        {
            var list = await songs.List(false);
            if (list["songs"] is JsonObject all)
                foreach (var (id, s) in all) d.TryAdd(Key((string)s["title"], (string)s["artist"]), id);
        }
        catch (Exception ex) { ctx.Log("installed index: " + ex.Message); }
        return d;
    }

    /* ---- editing and removing queue entries ---- */
    static readonly string[] Editable = { "title", "artist", "album", "year", "charter", "genre", "previewStart", "hopoFrequency", "isCover", "coverArtist", "countoff", "singer", "selected", "tier" };

    public object Update(string id, JsonObject fields)
    {
        lock (gate)
        {
            var it = items.FirstOrDefault(i => (string)i["id"] == id) ?? throw new KeyNotFoundException(id);
            foreach (var k in Editable) if (fields.TryGetPropertyValue(k, out var v)) it[k] = v?.DeepClone();
            if (fields.ContainsKey("singer")) it["singerFrom"] = "user";
            return it.DeepClone();
        }
    }

    /* The same fields on several songs at once (moving them to a division, ticking them). */
    public object UpdateMany(string[] ids, JsonObject fields)
    {
        var set = ids.ToHashSet();
        List<JsonObject> changed;
        lock (gate) changed = items.Where(i => set.Contains((string)i["id"])).ToList();
        return changed.Select(i => Update((string)i["id"], fields)).ToList();
    }

    public void Remove(IEnumerable<string> ids)
    {
        List<JsonObject> gone;
        lock (gate)
        {
            var set = ids.ToHashSet();
            gone = items.Where(i => set.Contains((string)i["id"])).ToList();
            items.RemoveAll(i => set.Contains((string)i["id"]));
        }
        foreach (var it in gone) DeleteStaging((string)it["folder"]);
    }

    void DeleteStaging(string folder)
    {
        try
        {
            if (string.IsNullOrEmpty(folder)) return;
            var full = Path.GetFullPath(folder);
            if (full.StartsWith(Path.GetFullPath(ctx.ImportDir) + "\\", StringComparison.OrdinalIgnoreCase) && Directory.Exists(full)) Directory.Delete(full, true);
        }
        catch { }
    }

    /* ---- installing ---- */
    /* replace: a song already installed by the launcher (same title and artist) is rebuilt in its
       place; the game's own songs and DLC are never replaced, those get a copy. */
    public async Task<object> Install(string[] ids, bool replace = false)
    {
        if (Installing) throw new InvalidOperationException("Ya hay una instalación en curso.");
        if (ctx.GameRunning()) throw new InvalidOperationException("Cierra Guitar Hero III antes de instalar canciones.");
        List<JsonObject> todo;
        lock (gate) todo = items.Where(i => ids.Contains((string)i["id"]) && (string)i["status"] is "ready" or "error").Select(i => i).ToList();
        if (todo.Count == 0) throw new InvalidOperationException("No hay canciones listas para instalar.");
        Installing = true;
        try
        {
            backups.BeforeChange($"instalar {todo.Count} canción(es)");
            bool gh3Plus = File.Exists(Path.Combine(ctx.Plugins, "TapHopoChord.dll"));
            var mine = songs.Library();
            var replacing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string ReplaceOf(JsonObject it)
            {
                var dup = (string)it["duplicateOf"];
                var key = dup == null ? null : mine.Select(kv => kv.Key).FirstOrDefault(k => string.Equals(k, dup, StringComparison.OrdinalIgnoreCase));
                if (!replace || key == null || !replacing.Add(key)) return null;
                return key;
            }
            // a replaced song keeps what was chosen for it: its singer and its division
            var listed = replace ? await songs.List(false) : null;
            JsonObject Installed(string key) => (listed?["songs"] as JsonObject)?.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value as JsonObject;
            string TierOf(string key) => (listed?["sections"] as JsonArray)?.OfType<JsonObject>().Where(s => (string)s["id"] == "download")
                .SelectMany(s => (s["tiers"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
                .FirstOrDefault(t => (t["songs"] as JsonArray)?.Any(n => string.Equals((string)n, key, StringComparison.OrdinalIgnoreCase)) == true)?["title"]?.GetValue<string>();
            static string SingerValue(string v) => (v ?? "").ToLowerInvariant() switch { "female" => "Female", "bret" => "Bret", "none" => "None", _ => "Male" };
            var job = new JsonObject { ["work"] = Path.Combine(ctx.Cache, "work"), ["songs"] = new JsonArray() };
            Directory.CreateDirectory((string)job["work"]);
            foreach (var it in todo)
            {
                it["status"] = "queued";
                int prevStart = (int?)it["previewStart"] ?? 30000;
                var rep = ReplaceOf(it);
                var old = rep != null ? Installed(rep) : null;
                var singer = old != null && !string.IsNullOrEmpty((string)old["singer"]) ? SingerValue((string)old["singer"]) : (string)it["singer"] ?? "Male";
                var tier = (rep != null ? TierOf(rep) : null) ?? (string)it["tier"] ?? "";
                ((JsonArray)job["songs"]).Add(new JsonObject
                {
                    ["id"] = (string)it["id"],
                    ["checksum"] = (string)it["title"],
                    ["title"] = (string)it["title"], ["artist"] = (string)it["artist"], ["album"] = (string)it["album"],
                    ["year"] = (int?)it["year"] ?? 0, ["charter"] = (string)it["charter"], ["genre"] = (string)it["genre"],
                    ["isCover"] = (bool?)it["isCover"] ?? false, ["coverArtist"] = (string)it["coverArtist"] ?? "",
                    ["notes"] = (string)it["notes"], ["guitar"] = (string)it["guitar"], ["rhythm"] = (string)it["rhythm"],
                    ["backing"] = it["backing"]?.DeepClone(), ["crowd"] = (string)it["crowd"], ["preview"] = (string)it["preview"],
                    ["previewStart"] = Math.Max(0, prevStart), ["previewLength"] = 30000,
                    ["hopoFrequency"] = (int?)it["hopoFrequency"] ?? 170,
                    ["countoff"] = (string)it["countoff"] ?? "HiHat01", ["singer"] = singer,
                    ["tier"] = tier,
                    ["lipsync"] = (string)it["lipsync"], ["lipsyncSource"] = (string)it["lipsyncSource"],
                    ["vocals"] = it["vocals"]?.DeepClone(),
                    ["replace"] = rep,
                    ["gh3Plus"] = gh3Plus
                });
            }
            var jobPath = Path.Combine(ctx.Cache, "job.json");
            Ctx.SaveJson(jobPath, job);
            ctx.Post("import.progress", new { phase = "start", count = todo.Count });

            var lib = songs.Library();
            var done = new List<string>();
            var failed = new List<object>();
            await songs.RunTool($"install --game \"{ctx.Root}\" --job \"{jobPath}\"", e =>
            {
                if ((string)e["event"] == "warning") { ctx.Post("import.warning", new { message = (string)e["message"] }); return; }
                var id = (string)e["id"];
                var it = todo.FirstOrDefault(x => (string)x["id"] == id);
                if (it == null) return;
                if ((string)e["event"] == "progress")
                {
                    it["status"] = "installing";
                    ctx.Post("import.progress", new { phase = "song", id, stage = (string)e["stage"], pct = (double?)e["pct"] ?? 0, index = (int?)e["index"] ?? 0, count = (int?)e["count"] ?? 0, title = (string)it["title"] });
                }
                else if ((string)e["event"] == "song")
                {
                    if ((bool?)e["ok"] == true)
                    {
                        var name = (string)e["checksum"];
                        it["status"] = "done";
                        it["checksum"] = name;
                        lib[name] = LibraryEntry(it, name);
                        done.Add(id);
                    }
                    else
                    {
                        it["status"] = "error";
                        it["error"] = (string)e["message"];
                        failed.Add(new { id, title = (string)it["title"], message = (string)e["message"] });
                    }
                    ctx.Post("import.item", it.DeepClone());
                }
            });
            songs.SaveLibrary(lib);
            songs.Invalidate();
            ctx.Post("songs.changed", new { });
            var summary = new { installed = done.Count, failed };
            ctx.Post("import.progress", new { phase = "end", installed = done.Count, failed = failed.Count });
            return summary;
        }
        finally
        {
            Installing = false;
            try { Directory.Delete(Path.Combine(ctx.Cache, "work"), true); } catch { }
        }
    }

    JsonObject LibraryEntry(JsonObject it, string name)
    {
        string cover = null;
        var art = (string)it["artPath"];
        if (art != null && File.Exists(art))
        {
            cover = name + Path.GetExtension(art).ToLowerInvariant();
            try { File.Copy(art, Path.Combine(ctx.Covers, cover), true); } catch { cover = null; }
        }
        return new JsonObject
        {
            ["title"] = (string)it["title"], ["artist"] = (string)it["artist"], ["album"] = (string)it["album"],
            ["year"] = (int?)it["year"] ?? 0, ["charter"] = (string)it["charter"], ["genre"] = (string)it["genre"],
            ["lengthMs"] = (int?)it["lengthMs"] ?? 0, ["diffGuitar"] = (int?)it["diffGuitar"] ?? -1,
            ["source"] = (string)it["sourceName"], ["installedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            ["cover"] = cover
        };
    }

    /* Clears what was installed from the queue. */
    public void ClearDone()
    {
        List<string> ids;
        lock (gate) ids = items.Where(i => (string)i["status"] == "done").Select(i => (string)i["id"]).ToList();
        Remove(ids);
    }
}
