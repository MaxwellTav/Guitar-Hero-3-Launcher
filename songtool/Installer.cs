using System.Text.Json;
using GH_Toolkit_Core.Audio;
using GH_Toolkit_Core.PAK;
using GH_Toolkit_Core.QB;
using static GH_Toolkit_Core.Methods.CreateForGame;
using static GH_Toolkit_Core.QB.QBConstants;

namespace SongTool;

/* One song of an install job, as the launcher writes it (camelCase JSON). Paths are absolute. */
sealed class JobSong
{
    public string Id { get; set; }
    public string Checksum { get; set; }        // GH3's internal name; the tool makes it unique
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Album { get; set; }
    public int Year { get; set; }
    public string Charter { get; set; }
    public string Genre { get; set; }
    public bool IsCover { get; set; }
    public string CoverArtist { get; set; }
    public string Notes { get; set; }           // notes.mid or notes.chart
    public string Guitar { get; set; }
    public string Rhythm { get; set; }
    public List<string> Backing { get; set; } = new();
    public string Crowd { get; set; }
    public string Preview { get; set; }         // a ready-made preview clip, if the song has one
    public int PreviewStart { get; set; } = 30000;
    public int PreviewLength { get; set; } = 30000;
    public int HopoFrequency { get; set; } = 170;
    public string Countoff { get; set; } = "HiHat01";
    public string Singer { get; set; } = "Male";   // Male, Female, Bret (Bret Michaels), None
    public string Tier { get; set; }              // the Download division it goes into
    public string Lipsync { get; set; }           // a "lipsync" folder of .ska files, when the song brings one
    public string LipsyncSource { get; set; }
    public List<string> Vocals { get; set; } = new();   // the vocal stems (also in Backing): where it is sung, without PART VOCALS
    public string Replace { get; set; }           // an installed custom's name: rebuilt in its place instead of a copy
    public float GuitarVolume { get; set; }
    public float BandVolume { get; set; }
    public bool Gh3Plus { get; set; }            // tap/open notes for GH3+'s TapHopoChord plugin
}

sealed class Job
{
    public List<JobSong> Songs { get; set; } = new();
    public string Work { get; set; }             // scratch folder for the compiler's intermediate files
}

static class Installer
{
    public static async Task<object> Run(string game, string jobPath)
    {
        var job = JsonSerializer.Deserialize<Job>(File.ReadAllText(jobPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var taken = Setlist.ExistingNames(game);
        var installed = new List<object>();
        var failed = new List<object>();
        var placed = new List<(string, string)>();
        int index = 0;
        try
        {
            foreach (var s in job.Songs)
            {
                index++;
                try
                {
                    var (name, drums) = await InstallOne(game, job.Work ?? Path.GetTempPath(), s, taken, index, job.Songs.Count);
                    taken.Add(name);
                    placed.Add((name, s.Tier));
                    installed.Add(new { id = s.Id, checksum = name });
                    Program.Emit(new { @event = "song", id = s.Id, ok = true, checksum = name, drums });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    failed.Add(new { id = s.Id, message = ex.Message });
                    Program.Emit(new { @event = "song", id = s.Id, ok = false, message = Clean(ex) });
                }
            }
        }
        finally
        {
            // into their divisions; if this fails they stay at the end of the first one, still playable
            string placeError = null;
            try { Setlist.Place(game, placed); }
            catch (Exception ex) { Console.WriteLine(ex); placeError = Clean(ex); }
            if (placeError != null) Program.Emit(new { @event = "warning", message = Program.T("Could not put them in their division: ", "No se pudieron acomodar en su división: ") + placeError });
        }
        return new { installed, failed };
    }

    static string Clean(Exception ex)
    {
        var m = ex.Message;
        if (ex is AggregateException a && a.InnerException != null) m = a.InnerException.Message;
        return m.Length > 600 ? m[..600] + "…" : m;
    }

    static void Step(JobSong s, string stage, double pct, int index, int count) =>
        Program.Emit(new { @event = "progress", id = s.Id, stage, pct, index, count });

    static async Task<(string name, int drums)> InstallOne(string game, string workRoot, JobSong s, HashSet<string> taken, int index, int count)
    {
        if (string.IsNullOrEmpty(s.Notes) || !File.Exists(s.Notes)) throw new FileNotFoundException(Program.T("The song has no notes.mid or notes.chart", "La canción no tiene notes.mid ni notes.chart"));
        var name = !string.IsNullOrEmpty(s.Replace) && taken.Contains(s.Replace)
            ? s.Replace
            : UniqueName(string.IsNullOrWhiteSpace(s.Checksum) ? CreateChecksum(s.Title ?? "") : CreateChecksum(s.Checksum), taken);
        var work = Path.Combine(workRoot, name + "_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(work);
        var singer = (s.Singer ?? "male").ToLowerInvariant() switch { "female" => "female", "none" => "none", "bret" => "bret", _ => "male" };
        try
        {
            // 1. the chart: notes -> <name>_song.pak (with GH3 drum animations; lip sync when the song has .ska files)
            Step(s, "chart", 0.05, index, count);
            var ska = !string.IsNullOrEmpty(s.Lipsync) && Directory.Exists(s.Lipsync) ? s.Lipsync : "";
            var (pakFile, drums, _) = SongPak.Create(s.Notes, work, name, s.HopoFrequency > 0 ? s.HopoFrequency : 170,
                singer == "female" ? "female" : singer == "none" ? "none" : "Male", s.Gh3Plus, ska,
                string.IsNullOrEmpty(s.LipsyncSource) ? "GHWT" : s.LipsyncSource,
                new SongPak.LipSource(game, singer, s.Vocals ?? new()));

            // 2. the audio: stems -> encrypted FSB3 + its .dat index. A missing stem becomes silence.
            Step(s, "audio", 0.25, index, count);
            string Missing(string what) => Path.Combine(work, "missing_" + what + ".ogg");
            var guitar = Existing(s.Guitar) ?? Missing("guitar");
            var rhythm = Existing(s.Rhythm) ?? Missing("rhythm");
            var backing = s.Backing.Where(File.Exists).ToArray();
            var preview = Existing(s.Preview);
            var audio = AudioCompiler.CreateGh3Compiler(
                name, work, "Compile", GAME_GH3,
                Math.Max(0, s.PreviewStart), s.PreviewLength > 1000 ? s.PreviewLength : 30000,
                -7.0m, 1.0m, 1.0m,
                preview != null, false,
                guitar, rhythm, backing, Existing(s.Crowd) ?? "", preview ?? "",
                "", "", new[] { "" });
            await audio.GH3AudioCompile();
            var (fsb, dat) = audio.getFsbDat();

            // 3. into the game: files first, then the setlist (so a listed song always has its files)
            Step(s, "install", 0.85, index, count);
            var music = Path.Combine(game, "DATA", "MUSIC");
            var songs = Path.Combine(game, "DATA", "SONGS");
            Directory.CreateDirectory(music);
            Directory.CreateDirectory(songs);
            File.Copy(fsb, Path.Combine(music, name + ".fsb.xen"), true);
            File.Copy(dat, Path.Combine(music, name + ".dat.xen"), true);
            File.Copy(pakFile, Path.Combine(songs, name + "_song.pak.xen"), true);

            var meta = new GhMetadata
            {
                Checksum = name,
                Title = s.Title ?? name,
                Artist = s.Artist ?? "",
                AlbumTitle = s.Album ?? "",
                Year = s.Year > 0 ? s.Year : null,
                ArtistTextSelect = s.IsCover ? "As Made Famous By" : "By",
                IsArtistFamousBy = s.IsCover,
                CoverArtist = s.CoverArtist ?? "",
                Genre = string.IsNullOrEmpty(s.Genre) ? "Rock" : s.Genre,
                ChartAuthor = s.Charter ?? "",
                Singer = singer,
                Bassist = "Default",
                BandVol = s.BandVolume,
                GtrVol = s.GuitarVolume,
                Countoff = string.IsNullOrEmpty(s.Countoff) ? "HiHat01" : s.Countoff,
                HopoThreshold = 500f,
                HmxHopoThreshold = s.HopoFrequency > 0 ? s.HopoFrequency : 170,
                Game = GAME_GH3
            };
            var entry = meta.GenerateGh3SongListEntry(GAME_GH3, CONSOLE_PC);
            var (pakData, pabData) = AddToDownloadList(Setlist.QbPak(game), CONSOLE_PC, new List<QBStruct.QBStructData> { entry });
            Setlist.WritePak(Setlist.QbPak(game), pakData, pabData);
            Step(s, "done", 1.0, index, count);
            return (name, drums);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { }
        }
    }

    static string Existing(string p) => !string.IsNullOrEmpty(p) && File.Exists(p) ? p : null;

    /* GH3 names are lowercase letters only (Honeycomb's rule); short, so paths stay short. */
    static string UniqueName(string baseName, HashSet<string> taken)
    {
        if (string.IsNullOrEmpty(baseName)) baseName = "custom";
        if (baseName.Length > 18) baseName = baseName[..18];
        var name = baseName;
        for (int i = 0; taken.Contains(name); i++)
            name = baseName + (char)('a' + i % 26) + (i >= 26 ? ((char)('a' + i / 26 % 26)).ToString() : "");
        return name;
    }
}
