using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GH_Toolkit_Core.Checksum;
using GH_Toolkit_Core.PAK;
using GH_Toolkit_Core.QB;
using static GH_Toolkit_Core.Methods.CreateForGame;
using static GH_Toolkit_Core.QB.QB;
using static GH_Toolkit_Core.QB.QBConstants;
using static GH_Toolkit_Core.QB.QBStruct;
using static GH_Toolkit_Core.QB.QBArray;

namespace SongTool;

/* GH3's song lists, out of DATA\PAK\qb.pak.xen:
     scripts\guitar\songlist.qb          gh3_songlist [names] + permanent_songlist_props {name = {title ...}}
     scripts\guitar\guitar_progression.qb GH3_General_Songs  (Quickplay's setlist, 8 tiers)
     scripts\guitar\store_data.qb         GH3_Bonus_Songs
     scripts\guitar\guitar_download.qb    GH3_Download_Songs (DLC and customs: Honeycomb adds here) */
static class Setlist
{
    const string ProgressionRef = "scripts\\guitar\\guitar_progression.qb";
    const string StoreRef = "scripts\\guitar\\store_data.qb";

    public static string QbPak(string game) => Path.Combine(game, "DATA", "PAK", "qb.pak.xen");

    public static object List(string game)
    {
        var pak = PAK.PakEntryDictFromFile(QbPak(game));
        var (_, _, songArray, props) = GetSongListPak(pak);
        var musicDir = Path.Combine(game, "DATA", "MUSIC");

        var songs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in props.Items)
        {
            if (item is not QBStructItem si || si.Data is not QBStructData d) continue;
            var key = si.Props.ID;
            var name = Str(d, "name") ?? key;
            songs[key] = new
            {
                id = key,
                name,
                title = Str(d, "title") ?? name,
                artist = Str(d, "artist") ?? "",
                year = (Str(d, "year") ?? "").TrimStart(',', ' '),
                famousBy = Str(d, "original_artist") == "0",
                coveredBy = Str(d, "covered_by"),
                charter = Str(d, "charter"),
                album = Str(d, "album_title"),
                singer = Str(d, "singer"),
                audio = File.Exists(Path.Combine(musicDir, name + ".fsb.xen"))
            };
        }

        var sections = new List<object>
        {
            Section("setlist", "Setlist", pak, ProgressionRef, "GH3_General_Songs"),
            Section("bonus", "Bonus", pak, StoreRef, "GH3_Bonus_Songs"),
            Section("download", "Download", pak, downloadRef, gh3DownloadSongs)
        };
        return new { sections, songs, total = songArray.Items.Count };
    }

    static object Section(string id, string title, Dictionary<string, PAK.PakEntry> pak, string qbRef, string itemName)
    {
        var tiers = new List<object>();
        try
        {
            var entries = QbEntryDictFromBytes(pak[qbRef].EntryData, "big");
            var root = entries.First(e => string.Equals(e.Key, itemName, StringComparison.OrdinalIgnoreCase)).Value.Data as QBStructData;
            for (int t = 1; t <= 32; t++)
            {
                if (TryGet(root, $"tier{t}") is not QBStructData tier) continue;
                var names = (TryGet(tier, "songs") as QBArrayNode)?.Items.Select(x => x.ToString()).ToList() ?? new();
                tiers.Add(new { title = Str(tier, "title") ?? $"Tier {t}", songs = names });
            }
        }
        catch (Exception ex) { Console.WriteLine($"{itemName}: {ex.Message}"); }
        return new { id, title, tiers };
    }

    /* ---- the Download list's divisions (tiers). GH3 shows each tier with its title, like the career
       setlist; the launcher keeps them as GH3_Download_Songs = {num_tiers tier1 tier2 ...}. ---- */
    public sealed class Tier
    {
        public string Title { get; set; }
        public List<string> Songs { get; set; } = new();
    }

    public const string DefaultTier = "Lost and Cursed Songs";

    /* Two names for the same song: equal as QB keys (a name the toolkit doesn't know reads as 0x...). */
    static string Key(string s) => CRC.QBKey(s ?? "", false).ToLowerInvariant();
    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || Key(a) == Key(b);

    public static List<Tier> ReadTiers(Dictionary<string, PAK.PakEntry> pak)
    {
        var (_, _, dl) = GetDownloadPak(pak);
        var tiers = new List<Tier>();
        for (int t = 1; t <= 64; t++)
        {
            if (TryGet(dl, $"tier{t}") is not QBStructData tier) continue;
            tiers.Add(new Tier
            {
                Title = Str(tier, "title") ?? $"Tier {t}",
                Songs = (TryGet(tier, "songs") as QBArrayNode)?.Items.Select(x => x.ToString()).ToList() ?? new()
            });
        }
        return tiers;
    }

    static string CleanTitle(string s)
    {
        s = Regex.Replace(s ?? "", @"[""\\\r\n\t]", " ").Trim();
        s = Regex.Replace(s, @"\s+", " ");
        return s.Length > 48 ? s[..48].Trim() : s;
    }

    /* Rewrites GH3_Download_Songs as text (the rest of guitar_download.qb untouched). Empty tiers are
       left out: GH3 would show a heading with nothing under it. */
    public static void WriteTiers(Dictionary<string, PAK.PakEntry> pak, List<Tier> tiers)
    {
        var entry = pak[downloadRef];
        var text = QbToTextString(DecompileQb(entry.EntryData, "big", "", "GH3", "PC")).Replace("\r\n", "\n");
        var block = Regex.Match(text, @"^GH3_Download_Songs = \{\n.*?^\}\n?", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!block.Success) throw new InvalidOperationException("No se encontró GH3_Download_Songs en guitar_download.qb");
        var level = Regex.Match(block.Value, @"^\s*level = (\S+)", RegexOptions.Multiline);
        var levelName = level.Success ? level.Groups[1].Value : "load_z_artdeco";

        var keep = tiers.Where(t => t.Songs.Count > 0).ToList();
        if (keep.Count == 0) keep.Add(new Tier { Title = "Downloaded songs" });
        var s = new StringBuilder();
        s.Append("GH3_Download_Songs = {\n\tprefix = 'download'\n\tnum_tiers = ").Append(keep.Count).Append('\n');
        for (int i = 0; i < keep.Count; i++)
        {
            var t = keep[i];
            var title = CleanTitle(t.Title);
            s.Append("\ttier").Append(i + 1).Append(" = {\n\t\ttitle = \"").Append(title.Length > 0 ? title : DefaultTier).Append("\"\n\t\tsongs = [\n");
            foreach (var n in t.Songs) s.Append("\t\t\t").Append(n).Append('\n');
            s.Append("\t\t]\n\t\tdefaultunlocked = ").Append(t.Songs.Count).Append("\n\t\tlevel = ").Append(levelName).Append("\n\t}\n");
        }
        s.Append("}\n");
        text = text[..block.Index] + s + text[(block.Index + block.Length)..];

        var (items, _) = ParseQFile(text, "PC", "GH3");
        var bytes = CompileQbFile(items, downloadRef, "GH3", "PC");
        // read it back: the songs and titles must come out as they went in
        var back = QbEntryDictFromBytes(bytes, "big");
        var dl = back.First(e => string.Equals(e.Key, gh3DownloadSongs, StringComparison.OrdinalIgnoreCase)).Value.Data as QBStructData;
        for (int i = 0; i < keep.Count; i++)
        {
            var got = (TryGet(TryGet(dl, $"tier{i + 1}") as QBStructData, "songs") as QBArrayNode)?.Items.Select(x => x.ToString()).ToList() ?? new();
            if (!got.Select(Key).SequenceEqual(keep[i].Songs.Select(Key)))
                throw new InvalidOperationException(Program.T($"Division {i + 1} did not come out as expected; nothing was changed.", $"La división {i + 1} no quedó como se esperaba; no se cambió nada."));
        }
        entry.OverwriteData(bytes);
    }

    /* The whole Download list as the page edited it: its divisions (same songs, nothing added or lost)
       and each song's singer. */
    sealed class LayoutJob
    {
        public List<Tier> Tiers { get; set; } = new();
        public Dictionary<string, string> Singers { get; set; } = new();
    }

    public static object Layout(string game, string jobPath)
    {
        var job = JsonSerializer.Deserialize<LayoutJob>(File.ReadAllText(jobPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var qbPak = QbPak(game);
        var pak = PAK.PakEntryDictFromFile(qbPak);
        var current = ReadTiers(pak).SelectMany(t => t.Songs).ToList();
        var canon = current.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var want = job.Tiers.SelectMany(t => t.Songs).ToList();
        if (want.Count != current.Count || want.Any(w => !canon.ContainsKey(Key(w))) || want.Select(Key).Distinct().Count() != want.Count)
            throw new InvalidOperationException(Program.T("The list changed while you were editing it; open it again.", "La lista cambió mientras la editabas; vuelve a abrirla."));
        foreach (var t in job.Tiers) t.Songs = t.Songs.Select(n => canon[Key(n)]).ToList();
        WriteTiers(pak, job.Tiers);

        int singers = 0;
        if (job.Singers.Count > 0)
        {
            var (songList, songListEntries, _, props) = GetSongListPak(pak);
            foreach (var (name, value) in job.Singers)
            {
                var v = (value ?? "").ToLowerInvariant();
                if (v is not ("male" or "female" or "bret" or "none")) continue;
                foreach (var item in props.Items)
                    if (item is QBStructItem si && Same(si.Props.ID, name) && si.Data is QBStructData d)
                    {
                        try { d["singer"] = v; } catch (ArgumentException) { d.AddQbKeyToStruct("singer", v); }
                        singers++;
                        break;
                    }
            }
            songList.OverwriteData(CompileQbFromDict(songListEntries, songlistRef, GAME_GH3, CONSOLE_PC));
        }
        var (pakData, pabData) = new PAK.PakCompiler(GAME_GH3, CONSOLE_PC, split: true).CompilePakFromDictionary(pak);
        WritePak(qbPak, pakData, pabData);
        ShowFirstTitle(game);
        return new { tiers = job.Tiers.Count(t => t.Songs.Count > 0), singers };
    }

    /* the game's Download page: the first division's title instead of the fixed "DOWNLOADED SONGS" */
    static void ShowFirstTitle(string game)
    {
        try { DownloadHeader.Apply(game); }
        catch (Exception ex) { Console.WriteLine("download header: " + ex); }
    }

    /* Just-installed songs into the division they were imported for (a new one at the end when no
       division has that title). */
    public static void Place(string game, IEnumerable<(string name, string tier)> songs)
    {
        var list = songs.Where(s => !string.IsNullOrWhiteSpace(s.name)).ToList();
        if (list.Count == 0) return;
        var qbPak = QbPak(game);
        var pak = PAK.PakEntryDictFromFile(qbPak);
        var tiers = ReadTiers(pak);
        foreach (var (name, tierTitle) in list)
        {
            var title = CleanTitle(string.IsNullOrWhiteSpace(tierTitle) ? DefaultTier : tierTitle);
            if (title.Length == 0) title = DefaultTier;
            // a replaced song already in its division keeps its place there; only the copy the
            // toolkit appended to the first division goes
            var home = tiers.FirstOrDefault(t => string.Equals(CleanTitle(t.Title), title, StringComparison.OrdinalIgnoreCase));
            int at = home?.Songs.FindIndex(x => Same(x, name)) ?? -1;
            if (at >= 0)
            {
                foreach (var t in tiers) if (t != home) t.Songs.RemoveAll(x => Same(x, name));
                for (int i = home.Songs.Count - 1; i > at; i--) if (Same(home.Songs[i], name)) home.Songs.RemoveAt(i);
                continue;
            }
            string real = null;
            foreach (var t in tiers)
            {
                int i = t.Songs.FindIndex(x => Same(x, name));
                if (i >= 0) { real = t.Songs[i]; t.Songs.RemoveAt(i); }
            }
            if (real == null) continue;
            var target = tiers.FirstOrDefault(t => string.Equals(CleanTitle(t.Title), title, StringComparison.OrdinalIgnoreCase));
            if (target == null) tiers.Add(target = new Tier { Title = title });
            target.Songs.Add(real);
        }
        WriteTiers(pak, tiers);
        var (pakData, pabData) = new PAK.PakCompiler(GAME_GH3, CONSOLE_PC, split: true).CompilePakFromDictionary(pak);
        WritePak(qbPak, pakData, pabData);
        ShowFirstTitle(game);
    }

    /* Takes songs out of the Download list (and the global song list), and deletes their files. */
    public static object Delete(string game, string[] names, bool deleteFiles)
    {
        var qbPak = QbPak(game);
        var compiler = new PAK.PakCompiler(GAME_GH3, CONSOLE_PC, split: true);
        var pak = PAK.PakEntryDictFromFile(qbPak);
        var (songList, songListEntries, songArray, props) = GetSongListPak(pak);
        var tiers = ReadTiers(pak);

        var removed = new List<string>();
        foreach (var n in names)
        {
            var tier = tiers.FirstOrDefault(t => t.Songs.Any(x => Same(x, n)));
            if (tier == null) throw new InvalidOperationException(Program.T($"\"{n}\" is not in the Download list: only downloaded or imported songs can be deleted.",
                                                                            $"\"{n}\" no está en la lista Download: solo se pueden borrar canciones descargadas o importadas."));
            tier.Songs.RemoveAll(x => Same(x, n));
            int j = songArray.GetItemIndex(n, QBKEY);
            if (j >= 0) songArray.Items.RemoveAt(j);
            string fileName = n;
            for (int k = 0; k < props.Items.Count; k++)
                if (props.Items[k] is QBStructItem si && string.Equals(si.Props.ID, n, StringComparison.OrdinalIgnoreCase))
                {
                    if (si.Data is QBStructData d) fileName = Str(d, "name") ?? n;
                    props.Items.RemoveAt(k);
                    break;
                }
            removed.Add(fileName);
        }

        songList.OverwriteData(CompileQbFromDict(songListEntries, songlistRef, GAME_GH3, CONSOLE_PC));
        WriteTiers(pak, tiers);
        var (pakData, pabData) = compiler.CompilePakFromDictionary(pak);
        WritePak(qbPak, pakData, pabData);

        int files = 0;
        if (deleteFiles)
            foreach (var f in removed)
                foreach (var path in new[] { Path.Combine(game, "DATA", "MUSIC", f + ".fsb.xen"), Path.Combine(game, "DATA", "MUSIC", f + ".dat.xen"), Path.Combine(game, "DATA", "SONGS", f + "_song.pak.xen") })
                    if (File.Exists(path)) { File.Delete(path); files++; }
        return new { removed, files };
    }

    /* Both halves of the split pak, each through a temp file so a crash never leaves a half-written one. */
    public static void WritePak(string pakPath, byte[] pak, byte[] pab)
    {
        var pabPath = pakPath.Replace(".pak.xen", ".pab.xen", StringComparison.OrdinalIgnoreCase);
        File.WriteAllBytes(pakPath + ".tmp", pak);
        File.WriteAllBytes(pabPath + ".tmp", pab);
        File.Move(pabPath + ".tmp", pabPath, true);
        File.Move(pakPath + ".tmp", pakPath, true);
    }

    public static HashSet<string> ExistingNames(string game)
    {
        var pak = PAK.PakEntryDictFromFile(QbPak(game));
        var (_, _, songArray, props) = GetSongListPak(pak);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in songArray.Items) set.Add(x.ToString());
        foreach (var item in props.Items) if (item is QBStructItem si) set.Add(si.Props.ID);
        return set;
    }

    static object TryGet(QBStructData d, string key)
    {
        try { return d[key]; } catch { return null; }
    }

    static string Str(QBStructData d, string key)
    {
        var v = TryGet(d, key);
        return v switch { null => null, string s => s, _ => v.ToString() };
    }
}
