using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GH_Toolkit_Core.PAK;
using GH_Toolkit_Core.QB;
using GH_Toolkit_Core.SKA;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using static GH_Toolkit_Core.MIDI.SongQbFile;

namespace SongTool;

/* A singer who moves his mouth when the song is sung. GH3's lip sync is a set of facial .ska clips
   per song (GH3_Singer_Male_SlowRide_1 ...) started by Band_PlayFacialAnim entries in the song's
   performance script; a clip plays to its end, then the face goes back to its idle loop, and a new
   Band_PlayFacialAnim cuts the running one. Customs have no clips of their own, so they borrow the
   game's: the singing clips of its own songs (for the singer's kind: male, also used by Bret, who
   shares the male animation set, or female), started where the custom is sung and cut with the
   singer's idle face where it stops. Where it is sung comes from PART VOCALS (.mid), the
   phrase_start/phrase_end events (.chart), or else the loudness of the vocals stem. The mouth then
   moves while there is singing and rests when there isn't; the shapes are another song's words. */
static class LipSync
{
    public sealed class Clip
    {
        public string Name { get; set; }
        public string Kind { get; set; }      // male, female
        public float Duration { get; set; }   // seconds
    }

    static readonly Dictionary<string, string> Idle = new()
    {
        ["male"] = "GH3_Singer_Male_Face_Idle_1",
        ["female"] = "GH3_Singer_Female_Face_Idle_1"
    };

    const int MaxClipsPerSong = 8;     // borrowed clips go into each song's pak
    const int MergeGapMs = 600;        // two sung notes closer than this are one stretch of singing
    const int RestGapMs = 1200;        // a pause at least this long closes the mouth (idle face) until the next one
    const int RestartMs = 5000;        // a new clip at least this often: a clip's start is its sung part
    const int MinSegmentMs = 300;

    static string CacheDir(string game) => Path.Combine(game, "Launcher", "cache", "lipsync");

    /* ---- the game's own singing clips, taken once out of DATA\SONGS\*_song.pak.xen ---- */
    public static List<Clip> Library(string game)
    {
        var dir = CacheDir(game);
        var index = Path.Combine(dir, "index.json");
        if (File.Exists(index))
            try
            {
                var cached = JsonSerializer.Deserialize<List<Clip>>(File.ReadAllText(index));
                if (cached != null && cached.All(c => File.Exists(Path.Combine(dir, c.Name + ".ska")))) return cached;
            }
            catch { }
        Directory.CreateDirectory(dir);
        var clips = new Dictionary<string, Clip>(StringComparer.OrdinalIgnoreCase);
        foreach (var pakPath in Directory.GetFiles(Path.Combine(game, "DATA", "SONGS"), "*_song.pak.xen"))
        {
            Dictionary<string, PAK.PakEntry> pak;
            try { pak = PAK.PakEntryDictFromFile(pakPath); } catch { continue; }
            foreach (var (key, entry) in pak)
            {
                if (!key.EndsWith(".ska", StringComparison.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileNameWithoutExtension(key.Replace('\\', '/').Split('/').Last());
                string kind = name.StartsWith("GH3_Singer_Female_", StringComparison.OrdinalIgnoreCase) ? "female"
                            : name.StartsWith("GH3_Singer_Male_", StringComparison.OrdinalIgnoreCase) ? "male" : null;
                if (kind == null || clips.ContainsKey(name) || Regex.IsMatch(name, "idle|wink|woo|yeah|scream", RegexOptions.IgnoreCase)) continue;
                var path = Path.Combine(dir, name + ".ska");
                File.WriteAllBytes(path, entry.EntryData);
                float duration;
                try { duration = new SkaFile(path, "big").Duration; }
                catch { File.Delete(path); continue; }
                if (duration < 2.5f) { File.Delete(path); continue; }
                clips[name] = new Clip { Name = name, Kind = kind, Duration = duration };
            }
        }
        var list = clips.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        File.WriteAllText(index, JsonSerializer.Serialize(list));
        return list;
    }

    /* ---- where the song is sung: [start, end) in ms ---- */
    public static List<(int start, int end)> Sung(string notesPath, string midiPath, IEnumerable<string> vocalStems)
    {
        List<(int, int)> spans = null;
        try
        {
            spans = Path.GetExtension(notesPath).Equals(".chart", StringComparison.OrdinalIgnoreCase) ? FromChart(notesPath) : FromMidi(midiPath);
        }
        catch (Exception ex) { Console.WriteLine("lipsync notes: " + ex.Message); }
        if (spans == null || spans.Count == 0)
        {
            var stems = vocalStems?.Where(File.Exists).ToList() ?? new();
            if (stems.Count > 0)
                try { spans = FromAudio(stems); } catch (Exception ex) { Console.WriteLine("lipsync audio: " + ex.Message); }
        }
        return Merge(spans ?? new());
    }

    static List<(int, int)> Merge(List<(int start, int end)> spans)
    {
        var merged = new List<(int start, int end)>();
        foreach (var (s, e) in spans.Where(x => x.end > x.start).OrderBy(x => x.start))
        {
            if (merged.Count > 0 && s - merged[^1].end <= MergeGapMs) merged[^1] = (merged[^1].start, Math.Max(merged[^1].end, e));
            else merged.Add((s, e));
        }
        return merged.Select(x => (x.start, Math.Max(x.end, x.start + MinSegmentMs))).ToList();
    }

    /* PART VOCALS: every pitched note (36-84) is sung */
    static List<(int, int)> FromMidi(string midiPath)
    {
        var midi = MidiFile.Read(midiPath, new ReadingSettings { InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore, NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore });
        var tempo = midi.GetTempoMap();
        var track = midi.GetTrackChunks().FirstOrDefault(t =>
            t.Events.OfType<SequenceTrackNameEvent>().Any(e => string.Equals(e.Text?.Trim(), "PART VOCALS", StringComparison.OrdinalIgnoreCase)));
        if (track == null) return new();
        int Ms(long tick) => (int)Math.Round(TimeConverter.ConvertTo<MetricTimeSpan>(tick, tempo).TotalMilliseconds);
        return track.GetNotes().Where(n => n.NoteNumber >= 36 && n.NoteNumber <= 84).Select(n => (Ms(n.Time), Ms(n.EndTime))).ToList();
    }

    /* .chart: [Events] phrase_start ... phrase_end (the lyrics are dropped when the toolkit converts it) */
    static List<(int, int)> FromChart(string chartPath)
    {
        int resolution = 192;
        var bpm = new List<(long tick, double bpm)>();
        var events = new List<(long tick, string text)>();
        string section = "";
        foreach (var raw in File.ReadLines(chartPath))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) { section = line.Trim('[', ']'); continue; }
            var m = Regex.Match(line, @"^(\w+)\s*=\s*(.*)$");
            if (!m.Success) continue;
            if (section == "Song" && m.Groups[1].Value == "Resolution") int.TryParse(m.Groups[2].Value.Trim(), out resolution);
            var e = Regex.Match(line, @"^(\d+)\s*=\s*(\w+)\s+(.*)$");
            if (!e.Success) continue;
            long tick = long.Parse(e.Groups[1].Value, CultureInfo.InvariantCulture);
            if (section == "SyncTrack" && e.Groups[2].Value == "B" && long.TryParse(e.Groups[3].Value.Trim(), out var b)) bpm.Add((tick, b / 1000.0));
            else if (section == "Events" && e.Groups[2].Value == "E") events.Add((tick, e.Groups[3].Value.Trim().Trim('"')));
        }
        if (bpm.Count == 0) bpm.Add((0, 120));
        bpm.Sort((x, y) => x.tick.CompareTo(y.tick));
        int Ms(long tick)
        {
            double ms = 0; long at = 0; double cur = bpm[0].tick == 0 ? bpm[0].bpm : 120;
            foreach (var (t, v) in bpm)
            {
                if (t >= tick) break;
                ms += (t - at) * 60000.0 / (cur * resolution);
                at = t; cur = v;
            }
            return (int)Math.Round(ms + (tick - at) * 60000.0 / (cur * resolution));
        }
        var spans = new List<(int, int)>();
        long? open = null;
        foreach (var (tick, text) in events.OrderBy(x => x.tick))
        {
            if (text == "phrase_start") open = tick;
            else if (text == "phrase_end" && open != null) { spans.Add((Ms(open.Value), Ms(tick))); open = null; }
        }
        if (spans.Count == 0)   // lyrics without phrases: each syllable a short stretch
            spans = events.Where(x => x.text.StartsWith("lyric ")).Select(x => (Ms(x.tick), Ms(x.tick) + 350)).ToList();
        return spans;
    }

    /* The vocals stems' loudness, 40 ms at a time (ffmpeg, next to the toolkit): loud = sung */
    static List<(int, int)> FromAudio(List<string> stems)
    {
        var ffmpeg = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (!File.Exists(ffmpeg)) return new();
        var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "-v", "error" }) psi.ArgumentList.Add(a);
        foreach (var s in stems) { psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(s); }
        if (stems.Count > 1) { psi.ArgumentList.Add("-filter_complex"); psi.ArgumentList.Add($"amix=inputs={stems.Count}:normalize=0"); }
        foreach (var a in new[] { "-ac", "1", "-ar", "8000", "-f", "s16le", "-" }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        p.ErrorDataReceived += (_, _) => { };
        p.BeginErrorReadLine();
        using var ms = new MemoryStream();
        p.StandardOutput.BaseStream.CopyTo(ms);
        p.WaitForExit();
        var pcm = ms.ToArray();
        const int win = 320;   // 40 ms at 8 kHz
        int n = pcm.Length / 2 / win;
        if (n < 10) return new();
        var rms = new double[n];
        for (int w = 0; w < n; w++)
        {
            double sum = 0;
            for (int i = 0; i < win; i++) { double v = BitConverter.ToInt16(pcm, (w * win + i) * 2); sum += v * v; }
            rms[w] = Math.Sqrt(sum / win);
        }
        var sorted = rms.OrderBy(x => x).ToArray();
        double loud = sorted[(int)(sorted.Length * 0.95)];
        if (loud < 300) return new();   // a silent stem
        double threshold = Math.Max(loud * 0.2, sorted[(int)(sorted.Length * 0.3)] * 2.5);
        var spans = new List<(int, int)>();
        int startW = -1;
        for (int w = 0; w <= n; w++)
        {
            bool on = w < n && rms[w] > threshold;
            if (on && startW < 0) startW = w;
            else if (!on && startW >= 0) { if (w - startW >= 4) spans.Add((startW * 40, w * 40)); startW = -1; }
        }
        return spans;
    }

    /* ---- the clips and their start times ---- */
    public sealed class Plan
    {
        public List<(int time, string anim)> Events { get; } = new();
        public List<Clip> Clips { get; } = new();
    }

    public static Plan Make(string game, List<(int start, int end)> sung, string singer, string seed)
    {
        var plan = new Plan();
        var kind = singer?.ToLowerInvariant() switch { "female" => "female", "male" or "bret" or "" or null => "male", _ => null };
        if (kind == null || sung.Count == 0) return plan;
        var lib = Library(game).Where(c => c.Kind == kind).ToList();
        if (lib.Count == 0) return plan;
        // short clips keep the pak small; their beginnings (where a verse starts) are what gets used
        var pool = lib.Where(c => c.Duration >= 8 && c.Duration <= 30).ToList();
        if (pool.Count < 4) pool = lib;
        var rng = new Random(StableHash(seed));
        var chosen = pool.OrderBy(_ => rng.Next()).Take(MaxClipsPerSong).ToList();
        plan.Clips.AddRange(chosen);
        int k = 0;
        for (int i = 0; i < sung.Count; i++)
        {
            var (start, end) = sung[i];
            int t = start;
            while (t < end - 200)
            {
                var clip = chosen[k++ % chosen.Count];
                plan.Events.Add((t, clip.Name));
                t += Math.Min(RestartMs, (int)(clip.Duration * 1000) - 100);
            }
            if (i == sung.Count - 1 || sung[i + 1].start - end >= RestGapMs) plan.Events.Add((end, Idle[kind]));
        }
        return plan;
    }

    static int StableHash(string s)
    {
        unchecked { int h = 23; foreach (var ch in s ?? "") h = h * 31 + ch; return h; }
    }

    /* into the song's performance script, as Band_PlayFacialAnim {name = vocalist anim = ...} */
    public static void Inject(GH_Toolkit_Core.MIDI.SongQbFile q, Plan plan)
    {
        foreach (var (time, anim) in plan.Events)
        {
            var p = new QBStruct.QBStructData();
            p.MakeTwoParams("vocalist", anim, "anim");
            q.Vocals.PerformanceScript.Add((time, new ScriptStruct(time, "band_playfacialanim", p).ToStruct()));
        }
    }

    public static string ClipFile(string game, Clip c) => Path.Combine(CacheDir(game), c.Name + ".ska");

    public static object Scan(string game)
    {
        var lib = Library(game);
        return lib.GroupBy(c => c.Kind).Select(g => new
        {
            kind = g.Key,
            count = g.Count(),
            seconds = Math.Round(g.Sum(c => c.Duration)),
            kb = g.Sum(c => new FileInfo(Path.Combine(CacheDir(game), c.Name + ".ska")).Length) / 1024,
            sample = g.Take(6).Select(c => $"{c.Name} {c.Duration:0.0}s")
        });
    }
}
