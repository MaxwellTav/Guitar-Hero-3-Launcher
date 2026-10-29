using System.Diagnostics;
using System.Text.Json.Nodes;

namespace GH3Launcher;

/* The game's own look and sound, read from the player's copy on the first start (nothing of the game
   ships with the launcher): menu textures out of DATA\ZONES\global.pak (the song tool's "art"), the
   flying-skeletons loading movie out of DATA\MOVIES, the menu sounds out of
   DATA\ZONES\global_sfx.pak ("sfx"; MP3s with a short header, made into plain WAVs). Converted with
   Honeycomb's ffmpeg into Launcher\game\, served to the page as assets\art\ and assets\sfx\. */
sealed class GameAssets
{
    const int Version = 1;   // bump to extract again after a change here
    readonly Ctx ctx;
    readonly SongService songs;
    readonly SemaphoreSlim gate = new(1, 1);

    public GameAssets(Ctx ctx, SongService songs) { this.ctx = ctx; this.songs = songs; }

    public string Dir => Path.Combine(ctx.Data, "game");
    public string ArtDir => Path.Combine(Dir, "art");
    public string SfxDir => Path.Combine(Dir, "sfx");
    string Marker => Path.Combine(Dir, "ready.json");
    string Ffmpeg => Path.Combine(ctx.Honeycomb, "ffmpeg.exe");
    public string Error { get; private set; }

    // texture in global.pak -> assets\art\<file>.webp, at most this wide (and a crop first, if any)
    static readonly (string tex, string file, int width, string crop)[] Textures =
    {
        ("GH3_Main_Menu_BG", "menu_bg", 1024, null), ("main_menu_illustrations", "menu_art", 1024, null), ("logo_GH3_LoR_256", "logo", 256, null),
        ("Setlist_BG_Head", "wood_head", 1024, null), ("Setlist_BG_Loop", "wood_loop", 1024, null),
        ("Setlist_Page1_Head", "page1_head", 512, null), ("Setlist_Page1_Loop", "page1_loop", 512, null), ("Setlist_Page1_Title", "page1_title", 512, null),
        ("Setlist_Page1_Head_Lines", "page1_lines", 512, null),
        ("Setlist_Page2_Head", "page2_head", 512, null), ("Setlist_Page2_Loop", "page2_loop", 512, null),
        ("Setlist_Page3_Head", "page3_head", 512, null), ("Setlist_Page3_Loop", "page3_loop", 1024, "crop=708:512:172:0"),
        ("Setlist_Goldstar", "goldstar", 32, null), ("Setlist_Clip", "clip", 128, null), ("Setlist_Gum", "gum", 128, null), ("Setlist_Shoeprint", "shoeprint", 256, null),
        ("setlist_icon_BACKYARD", "icon_backyard", 128, null), ("setlist_icon_BAR", "icon_bar", 128, null), ("setlist_icon_VIDEOSHOOT", "icon_videoshoot", 128, null),
        ("setlist_icon_ODEON", "icon_odeon", 128, null), ("setlist_icon_PRISON", "icon_prison", 128, null), ("setlist_icon_DESERT", "icon_desert", 128, null),
        ("setlist_icon_MEGADOME", "icon_megadome", 128, null), ("setlist_icon_HELL", "icon_hell", 128, null), ("setlist_icon_download", "icon_download", 128, null),
        ("setlist_icon_generic", "icon_generic", 128, null),
        ("Venue_BG", "venue_bg", 1024, null), ("Options_Audio_BG", "options_bg", 1024, null), ("Controller_2p_BG", "controller_bg", 1024, null),
        ("brick_bg", "brick_bg", 1024, null), ("store_song_record_frame", "record_frame", 256, null), ("Newspaper_BG", "newspaper_bg", 1024, null),
        ("Options_Calibrate_Paper", "paper_note", 256, null), ("Setlist_Overshadow", "overshadow", 512, null)
    };

    // the menu sounds: ui_sfx_select, Generic_Menu_Back_SFX, ui_sfx_scroll (+ _add), Menu_Warning_SFX
    public static readonly string[] Sounds = { "UI_Sound_01", "ui_sound_05", "UI_Sound_06", "UI_Sound_09", "UI_Sound_10", "scroll", "UI_Scroll_Add2", "Menu_Warning_01" };

    public bool Ready
    {
        get
        {
            try { return Ctx.LoadJson(Marker) is JsonObject m && (int?)m["version"] == Version && File.Exists(Path.Combine(ArtDir, "menu_bg.webp")); }
            catch { return false; }
        }
    }

    /* Extracts what is missing; progress goes to the page as "assets.progress". */
    public async Task Ensure()
    {
        await gate.WaitAsync();
        try
        {
            if (Ready) return;
            Error = null;
            // the game's own look, even with a theme on (Temas keeps the untouched global zone)
            var global = Path.Combine(ctx.Data, "backups", "theme", "global.pak.xen");
            if (!File.Exists(global)) global = Path.Combine(ctx.Root, "DATA", "ZONES", "global.pak.xen");
            if (!File.Exists(global)) throw new FileNotFoundException(Lang.Pick("The game's DATA\\ZONES\\global.pak.xen is missing.", "Falta DATA\\ZONES\\global.pak.xen del juego."));
            if (!File.Exists(Ffmpeg)) throw new FileNotFoundException(Lang.Pick("Honeycomb-GUI\\ffmpeg.exe is missing.", "Falta Honeycomb-GUI\\ffmpeg.exe."));
            var tmp = Path.Combine(ctx.Cache, "assets_tmp");
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(SfxDir);
            try
            {
                // 1. textures
                Progress(0.05, "art");
                await songs.RunTool($"art --pak \"{global}\" --out \"{Path.Combine(tmp, "art")}\"");
                int i = 0;
                foreach (var (tex, file, width, crop) in Textures)
                {
                    var src = Path.Combine(tmp, "art", tex + ".dds");
                    if (File.Exists(src))
                    {
                        var vf = $"scale='min({width},iw)':-1";
                        if (crop != null) vf = crop + "," + vf;
                        await Run(Ffmpeg, $"-hide_banner -loglevel error -y -i \"{src}\" -vf \"{vf}\" -c:v libwebp -quality 86 -lossless 0 \"{Path.Combine(ArtDir, file + ".webp")}\"");
                    }
                    else ctx.Log("assets: no texture " + tex);
                    Progress(0.1 + 0.6 * ++i / Textures.Length, "art");
                }

                // 2. the loading movie (letterboxed 1280x720: the band in the middle)
                Progress(0.72, "movie");
                var bik = Path.Combine(ctx.Root, "DATA", "MOVIES", "BIK", "loading_flying.bik.xen");
                if (File.Exists(bik))
                    await Run(Ffmpeg, $"-hide_banner -loglevel error -y -f bink -i \"{bik}\" -an -vf \"crop=1280:570:0:75,scale=960:-2\" -c:v libvpx -deadline realtime -cpu-used 8 -b:v 900k \"{Path.Combine(ArtDir, "loading.webm")}\"", 180000);

                // 3. the menu sounds
                Progress(0.88, "sfx");
                var sfxPak = Path.Combine(ctx.Root, "DATA", "ZONES", "global_sfx.pak.xen");
                if (File.Exists(sfxPak))
                {
                    await songs.RunTool($"sfx --pak \"{sfxPak}\" --out \"{Path.Combine(tmp, "sfx")}\" --names {string.Join(",", Sounds)}");
                    foreach (var name in Sounds)
                    {
                        var src = Path.Combine(tmp, "sfx", name + ".wav");
                        if (File.Exists(src))
                            await Run(Ffmpeg, $"-hide_banner -loglevel error -y -i \"{src}\" -c:a pcm_s16le -ar 44100 \"{Path.Combine(SfxDir, name.ToLowerInvariant() + ".wav")}\"");
                    }
                }
                Ctx.SaveJson(Marker, new JsonObject { ["version"] = Version, ["made"] = DateTime.Now.ToString("s") });
                Progress(1, "done");
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            ctx.Log("assets: " + ex);
            ctx.Post("assets.progress", new { stage = "error", pct = 1.0, error = ex.Message });
            throw;
        }
        finally { gate.Release(); }
    }

    void Progress(double pct, string stage) => ctx.Post("assets.progress", new { stage, pct });

    internal static async Task Run(string exe, string args, int timeoutMs = 60000)
    {
        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        using var p = Process.Start(psi);
        var err = p.StandardError.ReadToEndAsync();
        _ = p.StandardOutput.ReadToEndAsync();
        using var cts = new CancellationTokenSource(timeoutMs);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } throw new TimeoutException(Path.GetFileName(exe) + " " + args); }
        if (p.ExitCode != 0) throw new InvalidOperationException($"{Path.GetFileName(exe)}: {(await err).Trim()}");
    }

    public void Clear()
    {
        try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); } catch { }
    }
}
