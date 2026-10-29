using System.Buffers.Binary;
using System.Text.RegularExpressions;
using GH_Toolkit_Core.PAK;
using static GH_Toolkit_Core.QB.QB;
using static GH_Toolkit_Core.QB.QBConstants;

namespace SongTool;

/* GH3's Download page draws every division's title except the first one, which is the fixed text
   "DOWNLOADED SONGS" (create_sl_assets; BetterGH3 keeps its own copy in DATA\patch.pak.xen, which
   wins over qb.pak). This makes it the first division's title, like the others. Done once: a patched
   script no longer has the fixed text. The untouched pak is kept in Launcher\backups.
   patch.pak's files are known to the game only by checksums the toolkit can't name, and its pak
   compiler would recompute them: so that pak is rewritten here byte for byte, only the one file's
   data (and the offsets after it) changing. */
static class DownloadHeader
{
    static readonly Regex Fixed = new(
        @"displayText \\\{parent = setlist_menu(\s+)id = sl_text_1\s+text = ""DOWNLOADED SONGS""(\s+)font = text_a10\s+scale = 2\b",
        RegexOptions.Compiled);

    const uint ExtLast = 0x2cb3ef3b, ExtLast2 = 0xb524565f;

    /* Also setlist_scroll (qb.pak; BetterGH3 keeps the game's): moving onto the next division, the
       page jumps past its title only on the Setlist tab, so on Download the highlight bar slid one
       title further from the song at every division. Download now jumps like Setlist. */
    public static bool Apply(string game)
    {
        bool? headerInPatch = null;
        var patch = Path.Combine(game, "DATA", "patch.pak.xen");
        if (File.Exists(patch)) headerInPatch = PatchRaw(game, patch);
        return PatchQbPak(game, header: headerInPatch == null) && headerInPatch != false;
    }

    static string EditScroll(string text)
    {
        var block = Regex.Match(text, @"^script setlist_scroll\b.*?^endscript", RegexOptions.Multiline | RegexOptions.Singleline);
        if (!block.Success) return null;
        var fixedBlock = Regex.Replace(block.Value, @"^(\t+)if \(\$current_tab = tab_setlist\)(\n\1\tif \(<jump_tier> = 1\))",
            m => m.Groups[1].Value + "if (($current_tab = tab_setlist) || ($current_tab = tab_downloads))" + m.Groups[2].Value,
            RegexOptions.Multiline);
        return fixedBlock == block.Value ? null : text[..block.Index] + fixedBlock + text[(block.Index + block.Length)..];
    }

    /* The script text with the header taken from tier 1, or null when there is nothing to change. */
    static string Edit(string text)
    {
        var m = Fixed.Match(text);
        if (!m.Success) return null;
        var indent = m.Groups[1].Value;
        return text[..m.Index]
            + "GetUpperCaseString ($g_gh3_setlist.tier1.title)" + indent[..^1]
            + "displayText {parent = setlist_menu" + indent + "id = sl_text_1" + indent + "text = <UpperCaseString>" + m.Groups[2].Value + "font = text_a10" + indent + "scale = 1.6"
            + text[(m.Index + m.Length)..];
    }

    static void Backup(string game, string path)
    {
        var backup = Path.Combine(game, "Launcher", "backups", Path.GetFileName(path) + ".original");
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup)) File.Copy(path, backup);
    }

    sealed class Entry
    {
        public int Pos;          // where its 32-byte header is
        public uint Ext, FullName;
        public int DataPos;
        public byte[] Data;
    }

    /* true: patched now or before; false: it has create_sl_assets but in a form we don't know; null: not in this pak */
    static bool? PatchRaw(string game, string path)
    {
        var file = File.ReadAllBytes(path);
        var entries = new List<Entry>();
        for (int pos = 0; pos + 32 <= file.Length; pos += 32)
        {
            uint U(int at) => BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(pos + at, 4));
            var e = new Entry { Pos = pos, Ext = U(0), FullName = U(16), DataPos = pos + (int)U(4) };
            int size = (int)U(8);
            if (e.DataPos < 0 || e.DataPos + size > file.Length) throw new InvalidDataException("patch.pak: entrada fuera del archivo");
            e.Data = file.AsSpan(e.DataPos, size).ToArray();
            entries.Add(e);
            if (e.Ext is ExtLast or ExtLast2) break;
            if (entries.Count > 4096) throw new InvalidDataException("patch.pak: sin fin de tabla");
        }

        foreach (var e in entries)
        {
            if (e.Ext is ExtLast or ExtLast2) continue;
            string text;
            try { text = QbToTextString(DecompileQb(e.Data, "big", "", "GH3", "PC")).Replace("\r\n", "\n"); }
            catch { continue; }
            if (!text.Contains("script create_sl_assets")) continue;
            var edited = Edit(text);
            if (edited == null) return !text.Contains("\"DOWNLOADED SONGS\"");

            // the toolkit must give back this very file from its own text, or we don't touch it
            var qbName = "0x" + e.FullName.ToString("x8");
            var (same, _) = ParseQFile(text, "PC", "GH3");
            if (!CompileQbFile(same, qbName, "GH3", "PC").AsSpan().SequenceEqual(e.Data))
            {
                Console.WriteLine("download header: patch.pak no se recompila igual; no se toca");
                return false;
            }
            var (items, _) = ParseQFile(edited, "PC", "GH3");
            var bytes = CompileQbFile(items, qbName, "GH3", "PC");
            if (!QbToTextString(DecompileQb(bytes, "big", "", "GH3", "PC")).Contains("UpperCaseString"))
                throw new InvalidOperationException("El parche del título de Download no quedó bien.");
            e.Data = bytes;
            Backup(game, path);
            File.WriteAllBytes(path + ".tmp", Rebuild(file, entries));
            File.Move(path + ".tmp", path, true);
            return true;
        }
        return null;
    }

    /* The same pak with each file's data laid out again in its old order, 32-byte aligned; every
       header keeps its checksums and flags, only offset and size follow the data. */
    static byte[] Rebuild(byte[] old, List<Entry> entries)
    {
        static int Align(int n) => (n + 31) & ~31;
        var byData = entries.OrderBy(e => e.DataPos).ToList();
        int start = byData[0].DataPos;
        int oldEnd = Align(byData.Max(e => e.DataPos + (int)BinaryPrimitives.ReadUInt32BigEndian(old.AsSpan(e.Pos + 8, 4))));
        int tail = Math.Max(0, old.Length - oldEnd);
        int size = start;
        foreach (var e in byData) size = Align(size + e.Data.Length);
        var buf = new byte[size + tail];
        old.AsSpan(0, start).CopyTo(buf);
        int at = start;
        foreach (var e in byData)
        {
            e.Data.CopyTo(buf, at);
            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(e.Pos + 4, 4), (uint)(at - e.Pos));
            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(e.Pos + 8, 4), (uint)e.Data.Length);
            at = Align(at + e.Data.Length);
        }
        if (tail > 0) old.AsSpan(oldEnd, tail).CopyTo(buf.AsSpan(at));
        return buf;
    }

    /* qb.pak's scripts\guitar\menu\menu_setlist.qb: setlist_scroll always, create_sl_assets when
       patch.pak doesn't replace it (named files: the toolkit's compiler is fine there) */
    static bool PatchQbPak(string game, bool header)
    {
        var qbPak = Setlist.QbPak(game);
        var pak = PAK.PakEntryDictFromFile(qbPak);
        foreach (var (name, entry) in pak)
        {
            if (!name.EndsWith(".qb", StringComparison.OrdinalIgnoreCase)) continue;
            string text;
            try { text = QbToTextString(DecompileQb(entry.EntryData, "big", "", "GH3", "PC")).Replace("\r\n", "\n"); }
            catch { continue; }
            if (!text.Contains("script setlist_scroll") && !(header && text.Contains("script create_sl_assets"))) continue;
            var edited = text;
            if (header) edited = Edit(edited) ?? edited;
            edited = EditScroll(edited) ?? edited;
            if (edited == text) return true;   // done before

            var (same, _) = ParseQFile(text, "PC", "GH3");
            if (!CompileQbFile(same, name, "GH3", "PC").AsSpan().SequenceEqual(entry.EntryData))
            {
                Console.WriteLine("download menu: " + name + " no se recompila igual; no se toca");
                return false;
            }
            var (items, _) = ParseQFile(edited, "PC", "GH3");
            var bytes = CompileQbFile(items, name, "GH3", "PC");
            var back = QbToTextString(DecompileQb(bytes, "big", "", "GH3", "PC"));
            if (!back.Contains("tab_setlist) || ($current_tab = tab_downloads)") || header && !back.Contains("UpperCaseString"))
                throw new InvalidOperationException("El parche del menú de Download no quedó bien.");
            entry.OverwriteData(bytes);
            var (pakData, pabData) = new PAK.PakCompiler(GAME_GH3, CONSOLE_PC, split: true).CompilePakFromDictionary(pak);
            Setlist.WritePak(qbPak, pakData, pabData);
            return true;
        }
        return false;
    }
}
