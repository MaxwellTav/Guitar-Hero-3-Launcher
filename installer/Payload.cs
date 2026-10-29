using System.IO.Compression;
using System.Text;

namespace GH3Setup;

/* What the installer installs: a zip appended to the end of its own exe, found by a trailer of
   24 bytes: "GH3LPAY1" + the zip's offset + its length (little-endian Int64s). A development run
   can name a zip instead (--payload). Layout inside the zip:
     GH3 Launcher.exe
     Honeycomb-GUI\...                      (GH Toolkit + ffmpeg, ffprobe, ffplay)
     x360ce\x360ce_x86.exe, xinput1_3.dll, x360ce.ini   (to the game folder itself)
     Licenses\...                           (to Launcher\Licenses) */
sealed class Payload : IDisposable
{
    public const string Magic = "GH3LPAY1";
    readonly Stream stream;
    public ZipArchive Zip { get; }

    Payload(Stream s)
    {
        stream = s;
        Zip = new ZipArchive(s, ZipArchiveMode.Read);
    }

    public static Payload Open(string explicitZip)
    {
        if (!string.IsNullOrEmpty(explicitZip)) return new Payload(File.OpenRead(explicitZip));
        var fs = File.OpenRead(Environment.ProcessPath);
        try
        {
            if (fs.Length > 24)
            {
                var tail = new byte[24];
                fs.Seek(-24, SeekOrigin.End);
                fs.ReadExactly(tail);
                if (Encoding.ASCII.GetString(tail, 0, 8) == Magic)
                {
                    long offset = BitConverter.ToInt64(tail, 8), length = BitConverter.ToInt64(tail, 16);
                    if (offset > 0 && length > 0 && offset + length <= fs.Length - 24)
                        return new Payload(new SubStream(fs, offset, length));
                }
            }
        }
        catch { fs.Dispose(); throw; }
        fs.Dispose();
        var beside = Path.Combine(AppContext.BaseDirectory, "payload.zip");
        if (File.Exists(beside)) return new Payload(File.OpenRead(beside));
        throw new InvalidDataException(T.S("This installer is incomplete (its files are missing). Download it again.", "Este instalador está incompleto (le faltan sus archivos). Descárgalo de nuevo."));
    }

    public IEnumerable<ZipArchiveEntry> Files(string prefix) =>
        Zip.Entries.Where(e => e.Name.Length > 0 && e.FullName.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public string Text(string name)
    {
        var e = Zip.Entries.FirstOrDefault(x => x.FullName.Replace('\\', '/').Equals(name, StringComparison.OrdinalIgnoreCase));
        if (e == null) return null;
        using var r = new StreamReader(e.Open(), Encoding.UTF8);
        return r.ReadToEnd();
    }

    public void Dispose() { Zip.Dispose(); stream.Dispose(); }
}

/* A window onto part of a file, seekable (ZipArchive needs that). */
sealed class SubStream : Stream
{
    readonly Stream inner;
    readonly long start, length;
    long pos;

    public SubStream(Stream inner, long start, long length) { this.inner = inner; this.start = start; this.length = length; }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => pos; set => pos = Math.Clamp(value, 0, length); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (pos >= length) return 0;
        inner.Position = start + pos;
        int n = inner.Read(buffer, offset, (int)Math.Min(count, length - pos));
        pos += n;
        return n;
    }

    public override long Seek(long offset, SeekOrigin origin) => Position = origin switch
    {
        SeekOrigin.Begin => offset,
        SeekOrigin.Current => pos + offset,
        _ => length + offset
    };

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
}
