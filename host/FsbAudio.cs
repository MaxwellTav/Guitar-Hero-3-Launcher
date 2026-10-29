using System.Text;

namespace GH3Launcher;

/* GH3 PC's song audio, DATA\MUSIC\<song>.fsb.xen: an FSB3 bank, XORed with "5atu6w4zaw" and then
   bit-reversed byte by byte (Honeycomb's EncryptDecrypt). Inside, one MP3 per stem:
   <song>_guitar, _rhythm, _song, maybe _coop_*, _crowd and _preview, each a plain CBR MP3. */
static class FsbAudio
{
    static readonly byte[] Key = Encoding.ASCII.GetBytes("5atu6w4zaw");
    static readonly byte[] Reverse = BuildReverse();

    static byte[] BuildReverse()
    {
        var t = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            int v = i, r = 0;
            for (int b = 0; b < 8; b++) { r = (r << 1) | (v & 1); v >>= 1; }
            t[i] = (byte)r;
        }
        return t;
    }

    public static byte[] Decrypt(byte[] data)
    {
        var o = new byte[data.Length];
        for (int i = 0; i < data.Length; i++) o[i] = Reverse[data[i] ^ Key[i % Key.Length]];
        return o;
    }

    /* a piece of an encrypted bank that starts `offset` bytes into it (the key runs from the file's start) */
    public static byte[] DecryptAt(byte[] data, long offset)
    {
        var o = new byte[data.Length];
        for (int i = 0; i < data.Length; i++) o[i] = Reverse[data[i] ^ Key[(offset + i) % Key.Length]];
        return o;
    }

    public sealed record Stream(string Name, int Offset, int Length);

    /* The stems of a decrypted bank (offsets into it). */
    public static List<Stream> Parse(byte[] fsb)
    {
        if (fsb.Length < 24 || Encoding.ASCII.GetString(fsb, 0, 4) != "FSB3") throw new InvalidDataException("No es un FSB3");
        int count = BitConverter.ToInt32(fsb, 4);
        int dirLen = BitConverter.ToInt32(fsb, 8);
        int pos = 24, data = 24 + dirLen;
        var list = new List<Stream>();
        for (int i = 0; i < count && pos + 80 <= fsb.Length; i++)
        {
            int size = BitConverter.ToUInt16(fsb, pos);
            var name = Encoding.ASCII.GetString(fsb, pos + 2, 30).TrimEnd('\0');
            int len = BitConverter.ToInt32(fsb, pos + 36);
            list.Add(new Stream(name, data, len));
            data += len;
            pos += size > 0 ? size : 80;
        }
        return list;
    }
}
