using System.Text;

namespace GH3Launcher;

/* Clone Hero's .sng container: "SNGPKG", version, a 16-byte XOR mask, the song.ini values as
   key/value pairs, then a file index and the masked file data. Each file is unmasked with
   data[i] ^= mask[i % 16] ^ (byte)i, i counting from the start of that file. */
sealed class SngFile
{
    public Dictionary<string, string> Metadata { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(string name, long length, long offset)> Files { get; } = new();
    readonly string path;
    byte[] mask;

    public SngFile(string path)
    {
        this.path = path;
        using var fs = File.OpenRead(path);
        using var r = new BinaryReader(fs);
        if (Encoding.ASCII.GetString(r.ReadBytes(6)) != "SNGPKG") throw new InvalidDataException("No es un archivo .sng");
        r.ReadUInt32();   // version
        mask = r.ReadBytes(16);

        r.ReadUInt64();   // metadata length
        ulong count = r.ReadUInt64();
        for (ulong i = 0; i < count; i++)
        {
            var key = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
            var value = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
            Metadata[key] = value;
        }

        r.ReadUInt64();   // file index length
        ulong files = r.ReadUInt64();
        for (ulong i = 0; i < files; i++)
        {
            var name = Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte()));
            long len = (long)r.ReadUInt64();
            long off = (long)r.ReadUInt64();
            Files.Add((name, len, off));
        }
    }

    /* Writes every file into `dir`, plus a song.ini made from the metadata. */
    public void ExtractTo(string dir)
    {
        Directory.CreateDirectory(dir);
        using var fs = File.OpenRead(path);
        var buf = new byte[1 << 16];
        foreach (var (name, length, offset) in Files)
        {
            var safe = Path.GetFileName(name.Replace('/', '\\'));
            if (string.IsNullOrEmpty(safe)) continue;
            using var outFile = File.Create(Path.Combine(dir, safe));
            fs.Position = offset;
            long done = 0;
            while (done < length)
            {
                int n = fs.Read(buf, 0, (int)Math.Min(buf.Length, length - done));
                if (n <= 0) break;
                for (int i = 0; i < n; i++)
                {
                    long k = done + i;
                    buf[i] ^= (byte)(mask[k & 15] ^ (byte)k);
                }
                outFile.Write(buf, 0, n);
                done += n;
            }
        }
        if (!Files.Any(f => f.name.Equals("song.ini", StringComparison.OrdinalIgnoreCase)))
        {
            var sb = new StringBuilder("[song]\r\n");
            foreach (var (k, v) in Metadata) sb.Append(k).Append(" = ").Append(v.Replace("\r", " ").Replace("\n", " ")).Append("\r\n");
            File.WriteAllText(Path.Combine(dir, "song.ini"), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
