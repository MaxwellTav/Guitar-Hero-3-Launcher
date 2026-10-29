using System.Text;
using System.Text.RegularExpressions;

namespace GH3Launcher;

/* What a Clone Hero song folder says about itself: song.ini (or the [Song] block of notes.chart),
   its stems, its art, its length, and whether it has a guitar part GH3 can play. */
sealed class SongMeta
{
    public string Folder, Notes, NotesType;
    public string Title = "", Artist = "", Album = "", Genre = "", Charter = "", LoadingPhrase = "";
    public int Year, DiffGuitar = -1, PreviewStart = -1, HopoFrequency, LengthMs;
    public bool HasGuitar;
    public bool HasVocals;                            // a vocals stem, lyrics or a vocals chart: someone sings it
    public string Art;
    public string VocalGender = "";                   // song.ini vocal_gender (Clone Hero: male / female)
    public string Lipsync, LipsyncSource = "";       // a "lipsync" folder of .ska files (GH conversions bring one)
    public string Guitar, Rhythm, Preview, Crowd;
    public List<string> Backing = new();
    public List<string> Vocals = new();               // vocals*.ogg (also in Backing): where it is sung, for lip sync
    public List<string> Warnings = new();

    static readonly string[] AudioExt = { ".ogg", ".opus", ".mp3", ".wav", ".flac" };
    static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);

    public static string CleanText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("<br>", " ", StringComparison.OrdinalIgnoreCase);
        s = Tags.Replace(s, "");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    public static bool IsSongFolder(string dir) =>
        File.Exists(Path.Combine(dir, "notes.mid")) || File.Exists(Path.Combine(dir, "notes.chart"));

    public static SongMeta Read(string dir)
    {
        var m = new SongMeta { Folder = dir };
        var mid = Path.Combine(dir, "notes.mid");
        var chart = Path.Combine(dir, "notes.chart");
        if (File.Exists(mid)) { m.Notes = mid; m.NotesType = "mid"; }
        else if (File.Exists(chart)) { m.Notes = chart; m.NotesType = "chart"; }

        var ini = ReadIni(Path.Combine(dir, "song.ini"));
        if (m.NotesType == "chart") foreach (var (k, v) in ReadChartSong(chart)) ini.TryAdd(k, v);
        string G(params string[] keys) { foreach (var k in keys) if (ini.TryGetValue(k, out var v) && v.Length > 0) return v; return ""; }
        int I(string key, int def) => int.TryParse(G(key).Split('.')[0], out var v) ? v : def;

        m.Title = CleanText(G("name", "Name"));
        m.Artist = CleanText(G("artist", "Artist"));
        m.Album = CleanText(G("album", "Album"));
        m.Genre = CleanText(G("genre", "Genre"));
        m.Charter = CleanText(G("charter", "frets", "Charter"));
        m.LoadingPhrase = CleanText(G("loading_phrase"));
        var year = Regex.Match(G("year", "Year"), @"\d{4}");
        m.Year = year.Success ? int.Parse(year.Value) : 0;
        m.DiffGuitar = I("diff_guitar", -1);
        m.HopoFrequency = I("hopo_frequency", 0);
        m.PreviewStart = I("preview_start_time", -1);
        if (m.PreviewStart <= 0 && double.TryParse(G("preview_start_time_s"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ps) && ps > 0)
            m.PreviewStart = (int)(ps * 1000);
        m.LengthMs = I("song_length", 0);
        m.VocalGender = G("vocal_gender").ToLowerInvariant();
        m.LipsyncSource = G("lipsync_source");
        var lip = Path.Combine(dir, "lipsync");
        if (Directory.Exists(lip) && Directory.EnumerateFiles(lip, "*.ska*", SearchOption.AllDirectories).Any()) m.Lipsync = lip;
        if (string.IsNullOrEmpty(m.Title)) m.Title = Path.GetFileName(dir);

        foreach (var f in Directory.GetFiles(dir))
        {
            var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" && name == "album") m.Art = f;
            if (!AudioExt.Contains(ext)) continue;
            switch (name)
            {
                case "guitar": m.Guitar = f; break;
                case "bass": if (m.Rhythm != null && Path.GetFileNameWithoutExtension(m.Rhythm).Equals("rhythm", StringComparison.OrdinalIgnoreCase)) m.Backing.Add(m.Rhythm); m.Rhythm = f; break;
                case "rhythm": if (m.Rhythm == null) m.Rhythm = f; else m.Backing.Add(f); break;
                case "crowd": m.Crowd = f; break;
                case "preview": m.Preview = f; break;
                default:
                    m.Backing.Add(f);   // song, drums*, vocals*, keys ...
                    if (name.StartsWith("vocals")) m.Vocals.Add(f);
                    break;
            }
        }

        m.HasGuitar = m.Notes != null && HasGuitarPart(m.Notes, m.NotesType);
        m.HasVocals = m.Vocals.Count > 0 || m.VocalGender is "male" or "female" || (m.Notes != null && HasVocalPart(m.Notes, m.NotesType));
        if (m.Notes == null) m.Warnings.Add("No tiene notes.mid ni notes.chart");
        else if (!m.HasGuitar) m.Warnings.Add("No tiene pista de guitarra (GH3 solo toca guitarra)");
        if (m.Guitar == null && m.Backing.Count + (m.Rhythm != null ? 1 : 0) > 0) m.Warnings.Add("Sin pista de guitarra separada: los fallos no silenciarán la guitarra");
        if (m.Guitar == null && m.Rhythm == null && m.Backing.Count == 0) m.Warnings.Add("No tiene audio");
        if (m.LengthMs <= 0) m.LengthMs = AudioLength(m.Guitar ?? m.Backing.FirstOrDefault() ?? m.Rhythm);
        return m;
    }

    static Dictionary<string, string> ReadIni(string path)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return d;
        bool inSong = false;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) { inSong = line.Trim('[', ']').Trim().Equals("song", StringComparison.OrdinalIgnoreCase); continue; }
            if (!inSong) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            d[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return d;
    }

    /* notes.chart: [Song] { Name = "..." Artist = "..." Year = ", 2001" ... } */
    static Dictionary<string, string> ReadChartSong(string path)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var r = new StreamReader(path, Encoding.UTF8, true);
            bool inSong = false;
            string line;
            while ((line = r.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.StartsWith('[')) { if (inSong) break; inSong = line.Equals("[Song]", StringComparison.OrdinalIgnoreCase); continue; }
                if (!inSong || line == "{") continue;
                if (line == "}") break;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim();
                var val = line[(eq + 1)..].Trim().Trim('"').TrimStart(',', ' ');
                d[key switch { "PreviewStart" => "preview_start_time_s", _ => key }] = val;
            }
        }
        catch { }
        return d;
    }

    static bool HasGuitarPart(string path, string type)
    {
        try
        {
            if (type == "chart")
            {
                var text = File.ReadAllText(path);
                return Regex.IsMatch(text, @"\[(Expert|Hard|Medium|Easy)Single\]");
            }
            var bytes = File.ReadAllBytes(path);
            var s = Encoding.ASCII.GetString(bytes);
            return s.Contains("PART GUITAR", StringComparison.Ordinal) || s.Contains("T1 GEMS", StringComparison.Ordinal);
        }
        catch { return false; }
    }

    /* Lyrics or a vocals part in the chart. Clone Hero charts of sung songs nearly always have them,
       so a song with neither (and no vocals stem) is taken as instrumental. */
    static bool HasVocalPart(string path, string type)
    {
        try
        {
            if (type == "chart")
            {
                var text = File.ReadAllText(path);
                return text.Contains("\"lyric ", StringComparison.OrdinalIgnoreCase) || text.Contains("phrase_start", StringComparison.OrdinalIgnoreCase);
            }
            var s = Encoding.ASCII.GetString(File.ReadAllBytes(path));
            return s.Contains("PART VOCALS", StringComparison.Ordinal) || s.Contains("HARM1", StringComparison.Ordinal);
        }
        catch { return true; }   // unreadable: don't call it instrumental
    }

    /* Length of an Ogg (Vorbis/Opus), WAV or (roughly) MP3 file, in ms. */
    public static int AudioLength(string path)
    {
        if (path == null || !File.Exists(path)) return 0;
        try
        {
            using var fs = File.OpenRead(path);
            var head = new byte[Math.Min(4096, fs.Length)];
            fs.ReadExactly(head);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".ogg" or ".opus")
            {
                long rate = 48000, preSkip = 0;
                int op = IndexOf(head, "OpusHead"u8);
                int vb = IndexOf(head, "\u0001vorbis"u8);
                if (op >= 0) preSkip = BitConverter.ToUInt16(head, op + 10);
                else if (vb >= 0) rate = BitConverter.ToUInt32(head, vb + 12);
                int tail = (int)Math.Min(65536, fs.Length);
                fs.Position = fs.Length - tail;
                var end = new byte[tail];
                fs.ReadExactly(end);
                for (int i = tail - 14; i >= 0; i--)
                    if (end[i] == 'O' && end[i + 1] == 'g' && end[i + 2] == 'g' && end[i + 3] == 'S')
                    {
                        long granule = BitConverter.ToInt64(end, i + 6);
                        return granule > 0 ? (int)((granule - preSkip) * 1000 / rate) : 0;
                    }
            }
            else if (ext == ".wav")
            {
                int byteRate = BitConverter.ToInt32(head, 28);
                return byteRate > 0 ? (int)((fs.Length - 44) * 1000 / byteRate) : 0;
            }
            else if (ext == ".mp3") return (int)(fs.Length * 8 / 192);   // ~192 kbps
        }
        catch { }
        return 0;
    }

    static int IndexOf(byte[] data, ReadOnlySpan<byte> magic) => data.AsSpan().IndexOf(magic);
}
