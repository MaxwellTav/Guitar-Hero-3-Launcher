using System.Text;
using GH_Toolkit_Core.PAK;
using static GH_Toolkit_Core.QB.QB;

namespace SongTool;

/* Development helpers: dump the setlist scripts as text, pull textures out of a PAK. */
static class Explore
{
    public static object Run(string game, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var pak = PAK.PakEntryDictFromFile(Path.Combine(game, "DATA", "PAK", "qb.pak.xen"));
        File.WriteAllLines(Path.Combine(outDir, "entries.txt"), pak.Keys.OrderBy(k => k));
        int dumped = 0;
        foreach (var (name, entry) in pak)
        {
            if (!name.EndsWith(".qb", StringComparison.OrdinalIgnoreCase)) continue;
            var lower = name.ToLowerInvariant();
            if (!(lower.Contains("songlist") || lower.Contains("download") || lower.Contains("progression") || lower.Contains("menu_setlist") || lower.Contains("memcard"))) continue;
            try
            {
                var items = DecompileQb(entry.EntryData, "big", "", "GH3", "PC");
                File.WriteAllText(Path.Combine(outDir, lower.Replace('\\', '_') + ".q"), QbToTextString(items));
                dumped++;
            }
            catch (Exception ex) { Console.WriteLine($"{name}: {ex}"); }
        }
        return new { entries = pak.Count, dumped };
    }

    public static object DumpAll(string game, string outDir, string pakPath = null)
    {
        Directory.CreateDirectory(outDir);
        var pak = PAK.PakEntryDictFromFile(pakPath ?? Path.Combine(game, "DATA", "PAK", "qb.pak.xen"));
        File.WriteAllLines(Path.Combine(outDir, "_entries.txt"), pak.Keys.OrderBy(k => k));
        int n = 0;
        foreach (var (name, entry) in pak)
        {
            if (!name.EndsWith(".qb", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var items = DecompileQb(entry.EntryData, "big", "", "GH3", "PC");
                File.WriteAllText(Path.Combine(outDir, name.ToLowerInvariant().Replace('\\', '_') + ".q"), QbToTextString(items));
                n++;
            }
            catch (Exception ex) { Console.WriteLine($"{name}: {ex.Message}"); }
        }
        return new { dumped = n };
    }

    /* Does text -> QB give back the game's own bytes? (decompile, recompile, compare) */
    public static object RoundTrip(string game, string filter)
    {
        var pak = PAK.PakEntryDictFromFile(Path.Combine(game, "DATA", "PAK", "qb.pak.xen"));
        int same = 0, total = 0;
        var diff = new List<string>();
        foreach (var (name, entry) in pak)
        {
            if (!name.EndsWith(".qb", StringComparison.OrdinalIgnoreCase)) continue;
            if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            total++;
            try
            {
                var text = QbToTextString(DecompileQb(entry.EntryData, "big", "", "GH3", "PC"));
                var (items, _) = ParseQFile(text, "PC", "GH3");
                var bytes = CompileQbFile(items, name, "GH3", "PC");
                if (bytes.AsSpan().SequenceEqual(entry.EntryData)) same++;
                else
                {
                    int i = 0; while (i < Math.Min(bytes.Length, entry.EntryData.Length) && bytes[i] == entry.EntryData[i]) i++;
                    diff.Add($"{name}: {entry.EntryData.Length} vs {bytes.Length}, first diff @{i}");
                }
            }
            catch (Exception ex) { diff.Add($"{name}: {ex.GetType().Name} {ex.Message}"); }
        }
        return new { total, same, diff = diff.Take(60) };
    }

    public static object Find(string game, string itemName, string outDir)
    {
        var pak = PAK.PakEntryDictFromFile(Path.Combine(game, "DATA", "PAK", "qb.pak.xen"));
        var hits = new List<string>();
        foreach (var (name, entry) in pak)
        {
            if (!name.EndsWith(".qb", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var items = DecompileQb(entry.EntryData, "big", "", "GH3", "PC");
                if (items.Any(i => string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase)))
                {
                    hits.Add(name);
                    File.WriteAllText(Path.Combine(outDir, name.ToLowerInvariant().Replace('\\', '_') + ".q"), QbToTextString(items));
                }
            }
            catch { }
        }
        return hits;
    }

    public static object Art(string pakPath, string outDir, string filter)
    {
        Directory.CreateDirectory(outDir);
        var pak = PAK.PakEntryDictFromFile(pakPath);
        int n = 0;
        var names = new List<string>();
        foreach (var (name, entry) in pak)
        {
            names.Add(name);
            if (!name.Contains(".img", StringComparison.OrdinalIgnoreCase)) continue;
            if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            var data = entry.EntryData;
            int at = IndexOf(data, "DDS "u8.ToArray(), 128);
            if (at < 0) continue;
            var file = Path.Combine(outDir, name.Replace('\\', '_').Replace(".img", "").Replace(".xen", "") + ".dds");
            File.WriteAllBytes(file, data[at..]);
            n++;
        }
        File.WriteAllLines(Path.Combine(outDir, "_entries.txt"), names.OrderBy(x => x));
        return new { textures = n, entries = names.Count };
    }

    /* Sounds out of a pak (DATA\ZONES\global_sfx.pak.xen): "<name>.wav" entries, as they are. */
    public static object Sfx(string pakPath, string outDir, string names)
    {
        Directory.CreateDirectory(outDir);
        var want = names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<object>();
        foreach (var (name, entry) in PAK.PakEntryDictFromFile(pakPath))
        {
            var file = Path.GetFileName(name.Replace('\\', '/'));
            var bare = Path.GetFileNameWithoutExtension(file);
            if (!file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || !want.Contains(bare)) continue;
            var data = entry.EntryData;
            File.WriteAllBytes(Path.Combine(outDir, bare + ".wav"), data);
            found.Add(new { name = bare, size = data.Length, magic = System.Text.Encoding.ASCII.GetString(data, 0, Math.Min(4, data.Length)) });
        }
        return found;
    }

    static int IndexOf(byte[] data, byte[] magic, int limit)
    {
        for (int i = 0; i + magic.Length <= Math.Min(data.Length, limit); i++)
            if (data.AsSpan(i, magic.Length).SequenceEqual(magic)) return i;
        return -1;
    }
}
