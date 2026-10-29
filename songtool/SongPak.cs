using System.Reflection;
using System.Text;
using GH_Toolkit_Core.MIDI;
using GH_Toolkit_Core.PAK;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using static GH_Toolkit_Core.Methods.Exceptions;
using static GH_Toolkit_Core.MIDI.SongQbFile;

namespace SongTool;

/* <name>_song.pak for one song: the toolkit's own PAK.CreateSongPackage, step by step, so the drum
   animations can be put in. For GH3 the toolkit never reads PART DRUMS' gameplay notes (only Rock
   Band's animation notes 24-51, which Clone Hero charts almost never have), and its fallback
   generator writes World Tour note numbers: the drummer just sat there. DrumAnims writes them the
   way GH3's own songs have them. */
static class SongPak
{
    static readonly MethodInfo CreatePakForMidiData = typeof(PAK).GetMethod("CreatePakForMidiData", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException("GH-Toolkit-Core cambió: falta PAK.CreatePakForMidiData");

    /* lipsync: the game folder, the song's singer and its vocal stems, for borrowed facial clips
       (LipSync) when the song brings no .ska files of its own; null = none */
    public sealed record LipSource(string Game, string Singer, IEnumerable<string> VocalStems);

    public static (string pak, int drumNotes, int lipClips) Create(string notes, string work, string name, int hopo, string gender, bool gh3Plus, string skaPath, string skaSource, LipSource lips = null)
    {
        var midiPath = notes;
        var hopoType = MidiDefs.HopoType.RB;
        bool fromChart = false, easyOpens = false;
        if (Path.GetExtension(notes).Equals(".chart", StringComparison.OrdinalIgnoreCase))
        {
            var chart = new Chart(notes);
            chart.ConvertChartToMid();
            midiPath = chart.GetMidiPath();
            chart.WriteMidToFile();
            hopo = chart.GetHopoResolution();
            hopoType = MidiDefs.HopoType.GH3;
            fromChart = easyOpens = true;
        }
        gender = gender?.ToLowerInvariant() switch { "none" => "none", "female" => "female", _ => "Male" };
        skaPath ??= "";
        var q = new SongQbFile(midiPath, name, "GH3", "PC", hopo, "", "", "GH3", false, false, hopoType, easyOpens, skaPath, fromChart, gh3Plus);
        q.ParseMidiGH();
        int drums = q.DrumsNotes?.Count ?? 0;
        if (drums == 0)
        {
            try { q.DrumsNotes = DrumAnims.FromMidi(midiPath); drums = q.DrumsNotes.Count; }
            catch (Exception ex) { Console.WriteLine("drum anims: " + ex.Message); }
        }
        LipSync.Plan lipPlan = null;
        if (lips != null && skaPath == "" && gender != "none")
        {
            try
            {
                var sung = LipSync.Sung(notes, midiPath, lips.VocalStems);
                lipPlan = LipSync.Make(lips.Game, sung, lips.Singer, name);
                LipSync.Inject(q, lipPlan);
                Console.WriteLine($"lipsync: {sung.Count} sung stretches, {lipPlan.Events.Count} facial cues, {lipPlan.Clips.Count} clips");
            }
            catch (Exception ex) { Console.WriteLine("lipsync: " + ex); lipPlan = null; }
        }
        var midQb = q.ParseMidiToQb();
        var errors = q.GetErrorListAsString();
        if (!string.IsNullOrEmpty(errors)) throw new MidiCompileException(errors);
        var warnings = q.GetWarningListAsString();
        if (!string.IsNullOrEmpty(warnings)) Console.WriteLine("WARNINGS:\n" + warnings);
        if (lipPlan != null && lipPlan.Clips.Count > 0)
            return (WritePak(work, name, q, midQb, lipPlan.Clips.Select(c => LipSync.ClipFile(lips.Game, c))), drums, lipPlan.Clips.Count);
        try
        {
            var pak = (string)CreatePakForMidiData.Invoke(null, new object[] { work, name, "GH3", "PC", q, midQb, skaPath, skaSource ?? "GH3", gender, false, null });
            return (pak, drums, 0);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null) { throw ex.InnerException; }
    }

    /* PAK.CreatePakForMidiData's GH3 PC path, plus the borrowed facial clips copied byte for byte
       (the toolkit's .ska conversion is for other games' skeletons): songs\<name>.mid.qb,
       songs\<name>_song_scripts.qb, the .mid.qs.<lang> strings, the clips at the root. */
    static string WritePak(string savePath, string songName, GH_Toolkit_Core.MIDI.SongQbFile q, byte[] midQb, IEnumerable<string> skaFiles)
    {
        var root = Path.Combine(savePath, songName + "_PC");
        var songs = Path.Combine(root, "songs");
        Directory.CreateDirectory(songs);
        File.WriteAllBytes(Path.Combine(songs, songName + ".mid.qb.xen"), midQb);
        var scripts = q.MakeSongScripts();
        if (scripts != null) File.WriteAllBytes(Path.Combine(songs, songName + "_song_scripts.qb.xen"), scripts);
        foreach (var ska in skaFiles) File.Copy(ska, Path.Combine(root, Path.GetFileName(ska) + ".xen"), true);
        var qs = q.QsList;
        if (qs.Count > 0)
        {
            var keys = qs.OrderBy(e => e.Value).Select(e => e.Key).ToList();
            foreach (var lang in new[] { "de", "en", "es", "fr", "it" })
            {
                using var w = new StreamWriter(Path.Combine(songs, songName + ".mid.qs." + lang + ".xen"), false, Encoding.Unicode) { NewLine = "\n" };
                foreach (var k in keys) w.WriteLine(k[2..].PadLeft(8, '0') + " \"" + qs[k] + "\"");
                w.WriteLine();
                w.WriteLine();
            }
        }
        var data = new PAK.PakCompiler("GH3", "PC", null).CompilePAK(root).itemData;
        var pak = Path.Combine(savePath, songName + "_song.pak.xen");
        File.WriteAllBytes(pak, data);
        Directory.Delete(root, true);
        return pak;
    }
}

/* GH3's drummer, from Clone Hero's PART DRUMS. In GH3's own songs every hit is two notes at the same
   time: the hand/foot that plays it (36-45 left hand, 48-57 right hand/foot) and the drum it lands on
   (60 + the same 0-9 slot): 0 kick, 1 floor tom, 2 tom 2, 3 tom 1, 4 snare, 5 hi-hat, 7 ride,
   8 crash 1, 9 crash 2 (counted from Slow Ride's drums_notes and the toolkit's Rock Band map). */
static class DrumAnims
{
    const int Kick = 0, FloorTom = 1, Tom2 = 2, Tom1 = 3, Snare = 4, HiHat = 5, Ride = 7, Crash1 = 8, Crash2 = 9;
    const int Left = 36, Right = 48, Hit = 60, Len = 63;

    public static List<AnimNote> FromMidi(string midiPath)
    {
        var midi = MidiFile.Read(midiPath, new ReadingSettings
        {
            InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
            NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore
        });
        var tempo = midi.GetTempoMap();
        var track = midi.GetTrackChunks().FirstOrDefault(t =>
            t.Events.OfType<SequenceTrackNameEvent>().Any(e => string.Equals(e.Text?.Trim(), "PART DRUMS", StringComparison.OrdinalIgnoreCase)));
        if (track == null) return new List<AnimNote>();
        var notes = track.GetNotes().ToList();

        // the hardest difficulty that has notes (expert 96-101, hard 84-89 ...)
        int lane0 = new[] { 96, 84, 72, 60 }.FirstOrDefault(b => notes.Any(n => n.NoteNumber >= b && n.NoteNumber <= b + 5), -1);
        if (lane0 < 0) return new List<AnimNote>();
        // pro drums: 110/111/112 mark yellow/blue/green as toms instead of cymbals
        var toms = new[] { 110, 111, 112 }.ToDictionary(n => n, n => notes.Where(x => x.NoteNumber == n).Select(x => (x.Time, x.EndTime)).ToList());
        bool IsTom(int marker, long t) => toms[marker].Any(r => t >= r.Time && t < r.EndTime);

        var result = new List<AnimNote>();
        foreach (var chord in notes.Where(n => n.NoteNumber >= lane0 && n.NoteNumber <= lane0 + 5).GroupBy(n => n.Time).OrderBy(g => g.Key))
        {
            long tick = chord.Key;
            int ms = (int)Math.Round(TimeConverter.ConvertTo<MetricTimeSpan>(tick, tempo).TotalMilliseconds);
            var lanes = chord.Select(n => n.NoteNumber - lane0).ToHashSet();
            // each lane -> the drum it is (lane 0 kick, 1 red, 2 yellow, 3 blue, 4 green/orange, 5 five-lane green)
            var drums = new List<int>();
            foreach (var l in lanes.OrderBy(x => x))
                drums.Add(l switch
                {
                    0 => Kick,
                    1 => Snare,
                    2 => IsTom(110, tick) ? Tom1 : HiHat,
                    3 => IsTom(111, tick) ? Tom2 : Ride,
                    4 => IsTom(112, tick) ? FloorTom : Crash1,
                    _ => Crash2
                });

            // the right foot plays the kick; the hands share the rest: a drummer leads with the right
            // hand on cymbals and hi-hat, the left takes the snare, the second hit of a pair crosses over
            bool rightBusy = false, leftBusy = false;
            foreach (var d in drums.OrderBy(d => d == Kick ? 0 : d is HiHat or Ride or Crash1 or Crash2 ? 1 : 2))
            {
                int hand;
                if (d == Kick) hand = Right;
                else if (d == Snare) hand = !leftBusy ? Left : Right;
                else hand = !rightBusy ? Right : Left;
                if (d != Kick) { if (hand == Right) rightBusy = true; else leftBusy = true; }
                result.Add(new AnimNote(ms, hand + d, Len, 100));
                result.Add(new AnimNote(ms, Hit + d, Len, 100));
            }
        }
        return result;
    }
}
