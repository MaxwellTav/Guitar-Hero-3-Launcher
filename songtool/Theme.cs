using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.RegularExpressions;
using GH_Toolkit_Core.PAK;
using static GH_Toolkit_Core.QB.QB;

namespace SongTool;

/* Themes: the look of the notes, the highway and the HUD.
   A theme (Launcher\themes\<id>\) is what the community makes for FastGH3, kept as it came: gfx.tex
   (a texture dictionary like zones\global\global_gfx.tex) and/or a whole global.pak/.pab.
   Applying never swaps whole files: GH3's global zone also holds the menus, the characters and
   textures a FastGH3 theme doesn't have (its global_gfx.tex lacks 17 of the game's 107). It starts
   from the game's own global.pak/.pab (kept in Launcher\backups\theme\ before the first theme) and
   puts in only what the theme has: textures into global_gfx.tex by checksum, and the 2D images
   (.img) both zones have, by name. The materials (global_gfx.scn) stay the game's.
   A texture dictionary: "FACECAA7", u16 count at 6, records at the offset at 8: 40 bytes each
   (0A280200, checksum, sizes and format, data offset at +28, data size at +32), then the images
   (DDS or PNG, which GH3 PC also loads) back to back. */
static class Theme
{
    const uint ExtLast = 0x2cb3ef3b, ExtLast2 = 0xb524565f;
    static readonly uint GfxTex = Key(@"zones\global\global_gfx.tex");
    static readonly uint ExtImg = Key(".img");

    public static uint Key(string s)
    {
        uint c = 0xFFFFFFFF;
        foreach (var ch in s.ToLowerInvariant().Replace('/', '\\')) { c ^= (byte)ch; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320 : c >> 1; }
        return c;
    }

    /* ---- pak + pab: the data offsets count from each header, through the pak and on into the pab ---- */
    sealed class Entry { public int Pos; public uint Ext, FullName; public long DataPos; public byte[] Data; }

    static (List<Entry> entries, byte[] pak) ReadPak(string pakPath, string pabPath)
    {
        var pak = File.ReadAllBytes(pakPath);
        using var pab = pabPath != null && File.Exists(pabPath) ? File.OpenRead(pabPath) : null;
        var list = new List<Entry>();
        for (int pos = 0; pos + 32 <= pak.Length; pos += 32)
        {
            uint U(int at) => BinaryPrimitives.ReadUInt32BigEndian(pak.AsSpan(pos + at, 4));
            var e = new Entry { Pos = pos, Ext = U(0), FullName = U(16), DataPos = pos + (long)U(4) };
            int size = (int)U(8);
            list.Add(e);
            if (e.Ext is ExtLast or ExtLast2) { e.Data = Array.Empty<byte>(); break; }
            if (e.DataPos + size <= pak.Length) e.Data = pak.AsSpan((int)e.DataPos, size).ToArray();
            else
            {
                if (pab == null || e.DataPos < pak.Length || e.DataPos - pak.Length + size > pab.Length) throw new InvalidDataException(Path.GetFileName(pakPath) + ": entry outside the pak/pab");
                pab.Position = e.DataPos - pak.Length;
                e.Data = new byte[size];
                pab.ReadExactly(e.Data);
            }
            if (list.Count > 65536) throw new InvalidDataException(Path.GetFileName(pakPath) + ": no end of table");
        }
        return (list, pak);
    }

    /* Same headers (checksums, flags), the data laid out again in its old order in the pab, 32-byte aligned. */
    static void WritePak(string pakPath, string pabPath, byte[] oldPak, List<Entry> entries)
    {
        static long Align(long n) => (n + 31) & ~31L;
        var pak = (byte[])oldPak.Clone();
        var data = entries.Where(e => e.Ext is not (ExtLast or ExtLast2)).OrderBy(e => e.DataPos).ToList();
        long size = 0;
        foreach (var e in data) size = Align(size + e.Data.Length);
        size = (size + 4095) & ~4095L;
        var pab = new byte[size];
        long at = 0;
        foreach (var e in data)
        {
            e.Data.CopyTo(pab, at);
            BinaryPrimitives.WriteUInt32BigEndian(pak.AsSpan(e.Pos + 4, 4), (uint)(pak.Length + at - e.Pos));
            BinaryPrimitives.WriteUInt32BigEndian(pak.AsSpan(e.Pos + 8, 4), (uint)e.Data.Length);
            at = Align(at + e.Data.Length);
        }
        File.WriteAllBytes(pabPath + ".tmp", pab);
        File.WriteAllBytes(pakPath + ".tmp", pak);
        File.Move(pabPath + ".tmp", pabPath, true);
        File.Move(pakPath + ".tmp", pakPath, true);
    }

    /* ---- texture dictionaries ---- */
    sealed class Tex { public uint Id; public int Rec; public byte[] Data; }

    static (byte[] head, List<Tex> list, byte[] tail) ReadDict(byte[] b)
    {
        if (b.Length < 16 || BinaryPrimitives.ReadUInt32BigEndian(b) != 0xFACECAA7) throw new InvalidDataException("not a texture dictionary");
        int n = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(6, 2));
        int recs = (int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(8, 4));
        var list = new List<Tex>(n);
        int first = int.MaxValue, end = 0;
        for (int i = 0; i < n; i++)
        {
            int o = recs + 40 * i;
            if (o + 40 > b.Length || b[o] != 0x0A || b[o + 1] != 0x28) throw new InvalidDataException("texture dictionary: unknown record");
            int off = (int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o + 28, 4));
            int size = (int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o + 32, 4));
            if (off < 0 || size < 0 || off + size > b.Length) throw new InvalidDataException("texture dictionary: data outside the file");
            list.Add(new Tex { Id = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o + 4, 4)), Rec = o, Data = b.AsSpan(off, size).ToArray() });
            first = Math.Min(first, off);
            end = Math.Max(end, off + size);
        }
        if (n == 0) return (b, list, Array.Empty<byte>());
        return (b[..first], list, b[end..]);
    }

    /* The game's dictionary with the theme's version of every texture both have: the record keeps its
       place (the lookup table before the records stays valid) and takes the theme's sizes and format. */
    static byte[] MergeDict(byte[] game, byte[] theme, out int replaced)
    {
        var (head, list, tail) = ReadDict(game);
        var (themeHead, themeList, _) = ReadDict(theme);
        var byId = themeList.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
        head = (byte[])head.Clone();
        replaced = 0;
        foreach (var t in list)
        {
            if (!byId.TryGetValue(t.Id, out var th)) continue;
            Array.Copy(themeHead, th.Rec + 8, head, t.Rec + 8, 20);   // sizes, mipmaps, format
            t.Data = th.Data;
            replaced++;
        }
        using var ms = new MemoryStream();
        ms.Write(head);
        foreach (var t in list)
        {
            var off = (uint)ms.Position;
            ms.Write(t.Data);
            BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(t.Rec + 28, 4), off);
            BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(t.Rec + 32, 4), (uint)t.Data.Length);
        }
        ms.Write(tail);
        var bytes = ms.ToArray();
        head.CopyTo(bytes, 0);
        return bytes;
    }

    /* ---- the commands ---- */
    static string Originals(string game) => Path.Combine(game, "Launcher", "backups", "theme");

    /* themeDir empty: the game's own look back. */
    public static object Apply(string game, string themeDir)
    {
        var zones = Path.Combine(game, "DATA", "ZONES");
        string pak = Path.Combine(zones, "global.pak.xen"), pab = Path.Combine(zones, "global.pab.xen");
        var keep = Originals(game);
        string opak = Path.Combine(keep, "global.pak.xen"), opab = Path.Combine(keep, "global.pab.xen");
        if (!File.Exists(opak) || !File.Exists(opab))
        {
            Directory.CreateDirectory(keep);
            File.Copy(pab, opab + ".tmp", true); File.Move(opab + ".tmp", opab, true);
            File.Copy(pak, opak + ".tmp", true); File.Move(opak + ".tmp", opak, true);
        }
        if (string.IsNullOrEmpty(themeDir))
        {
            File.Copy(opab, pab + ".tmp", true); File.Move(pab + ".tmp", pab, true);
            File.Copy(opak, pak + ".tmp", true); File.Move(pak + ".tmp", pak, true);
            return new { theme = "", textures = 0, images = 0 };
        }
        var (texture, images) = Load(themeDir);
        var (entries, oldPak) = ReadPak(opak, opab);
        int textures = 0, imgs = 0;
        foreach (var e in entries)
        {
            if (e.FullName == GfxTex && texture != null) e.Data = MergeDict(e.Data, texture, out textures);
            else if (e.Ext == ExtImg && images.TryGetValue(e.FullName, out var img)) { e.Data = img; imgs++; }
        }
        if (textures + imgs == 0) throw new InvalidOperationException(Program.T("This theme has nothing the game uses.", "Este tema no trae nada que use el juego."));
        WritePak(pak, pab, oldPak, entries);
        return new { theme = Path.GetFileName(themeDir.TrimEnd('\\')), textures, images = imgs };
    }

    /* A theme folder: its texture dictionary (gfx.tex, else the global_gfx.tex in its global pak) and its .img by checksum. */
    static (byte[] texture, Dictionary<uint, byte[]> images) Load(string dir)
    {
        byte[] tex = null;
        var images = new Dictionary<uint, byte[]>();
        var gfx = Path.Combine(dir, "gfx.tex");
        if (File.Exists(gfx)) tex = File.ReadAllBytes(gfx);
        var pak = Path.Combine(dir, "global.pak.xen");
        if (File.Exists(pak))
            foreach (var e in ReadPak(pak, Path.Combine(dir, "global.pab.xen")).entries)
            {
                if (e.FullName == GfxTex) tex ??= e.Data;
                else if (e.Ext == ExtImg) images[e.FullName] = e.Data;
            }
        return (tex, images);
    }

    /* Each texture of a theme (or of the game: dir = "game") as its own file, for the launcher's preview. */
    public static object Textures(string game, string dir, string outDir)
    {
        byte[] tex;
        if (dir == "game")
        {
            var keep = Originals(game);
            bool orig = File.Exists(Path.Combine(keep, "global.pak.xen"));
            var zones = Path.Combine(game, "DATA", "ZONES");
            var (entries, _) = orig ? ReadPak(Path.Combine(keep, "global.pak.xen"), Path.Combine(keep, "global.pab.xen"))
                                    : ReadPak(Path.Combine(zones, "global.pak.xen"), Path.Combine(zones, "global.pab.xen"));
            tex = entries.FirstOrDefault(e => e.FullName == GfxTex)?.Data;
        }
        else tex = Load(dir).texture;
        if (tex == null) return new { textures = Array.Empty<object>() };
        Directory.CreateDirectory(outDir);
        var (head, list, _) = ReadDict(tex);
        var outList = new List<object>();
        foreach (var t in list)
        {
            bool png = t.Data.Length > 8 && t.Data[0] == 0x89 && t.Data[1] == (byte)'P';
            int at = png ? 0 : IndexOf(t.Data, "DDS "u8);
            if (at < 0) continue;
            var file = $"{t.Id:x8}.{(png ? "png" : "dds")}";
            File.WriteAllBytes(Path.Combine(outDir, file), t.Data[at..]);
            outList.Add(new { id = t.Id.ToString("x8"), w = BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(t.Rec + 8, 2)), h = BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(t.Rec + 10, 2)), file });
        }
        return new { textures = outList };
    }

    /* Brings a downloaded theme into a theme folder: a FastGH3 theme zip (global.pak/pab, or
       *.gfx / theme.tex with their .scn), or a bare .gfx/.tex file. Only those files are taken. */
    public static object Import(string src, string outDir)
    {
        Directory.CreateDirectory(outDir);
        bool full = false, dict = false;
        byte[] Magic(byte[] b) => b.Length >= 4 ? b[..4] : b;
        bool IsDict(byte[] b) => Magic(b).AsSpan().SequenceEqual(new byte[] { 0xFA, 0xCE, 0xCA, 0xA7 });
        var head = new byte[4];
        using (var f = File.OpenRead(src)) f.ReadAtLeast(head, 4, false);
        if (head[0] == (byte)'P' && head[1] == (byte)'K')
        {
            using var zip = ZipFile.OpenRead(src);
            byte[] Read(ZipArchiveEntry e) { using var s = e.Open(); using var m = new MemoryStream(); s.CopyTo(m); return m.ToArray(); }
            ZipArchiveEntry Find(Func<string, bool> pick) => zip.Entries.FirstOrDefault(e => e.Length > 0 && pick(Path.GetFileName(e.FullName).ToLowerInvariant()));
            var gpak = Find(n => n is "global.pak.xen" or "global.pak");
            var gpab = Find(n => n is "global.pab.xen" or "global.pab");
            if (gpak != null)
            {
                File.WriteAllBytes(Path.Combine(outDir, "global.pak.xen"), Read(gpak));
                if (gpab != null) File.WriteAllBytes(Path.Combine(outDir, "global.pab.xen"), Read(gpab));
                full = true;
            }
            // newer themes call it <name>.gfx, older ones theme.tex
            var gfx = Find(n => n.EndsWith(".gfx.xen") || n.EndsWith(".gfx")) ?? Find(n => n is "theme.tex.xen" or "theme.tex");
            if (gfx != null)
            {
                var b = Read(gfx);
                if (IsDict(b)) { File.WriteAllBytes(Path.Combine(outDir, "gfx.tex"), b); dict = true; }
            }
        }
        else
        {
            var b = File.ReadAllBytes(src);
            if (IsDict(b)) { File.WriteAllBytes(Path.Combine(outDir, "gfx.tex"), b); dict = true; }
        }
        if (!full && !dict) throw new InvalidOperationException(Program.T("That's not a GH3/FastGH3 theme (a zip with global.pak or .gfx/.tex, or a .gfx/.tex file).", "Eso no es un tema de GH3/FastGH3 (un zip con global.pak o .gfx/.tex, o un archivo .gfx/.tex)."));
        var (texture, images) = Load(outDir);
        int textures = texture != null ? ReadDict(texture).list.Count : 0;
        return new { kind = full ? "full" : "textures", textures, images = images.Count };
    }

    /* A song's expert guitar notes, for the theme preview: [time ms, length ms, fret mask] each. */
    public static object Notes(string game, string song)
    {
        var path = Path.Combine(game, "DATA", "SONGS", song + "_song.pak.xen");
        if (!File.Exists(path)) throw new FileNotFoundException(Program.T("That song's notes were not found.", "No se encontraron las notas de esa canción."), path);
        var pattern = new Regex(@"\b" + Regex.Escape(song) + @"_song_expert = \[(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (var (key, entry) in PAK.PakEntryDictFromFile(path))
        {
            if (!key.EndsWith(".qb", StringComparison.OrdinalIgnoreCase) && !key.StartsWith("0x")) continue;
            string text;
            try { text = QbToTextString(DecompileQb(entry.EntryData, "big", "", "GH3", "PC")); } catch { continue; }
            var m = pattern.Match(text);
            if (!m.Success) continue;
            var nums = Regex.Matches(m.Groups[1].Value, @"-?\d+").Select(x => int.Parse(x.Value)).ToList();
            var notes = new List<int[]>();
            for (int i = 0; i + 2 < nums.Count; i += 3) notes.Add(new[] { nums[i], nums[i + 1], nums[i + 2] });
            return new { notes };
        }
        return new { notes = new List<int[]>() };
    }

    static int IndexOf(byte[] data, ReadOnlySpan<byte> magic)
    {
        int i = data.AsSpan(0, Math.Min(data.Length, 256)).IndexOf(magic);
        return i;
    }
}
