using System.Net.Http;
using System.Text.Json.Nodes;

namespace GH3Launcher;

/* Temas: the look of the notes, the highway and the HUD, from what the FastGH3 community makes.
   Each theme lives in Launcher\themes\<id>\ (theme.json + what the song tool's theme-import kept);
   applying it (theme-apply) rewrites DATA\ZONES\global.pak/.pab from the game's own copy, taking
   only the textures and images the theme has. "" = the game's own look.
   For the page's preview each theme also gets preview\<slot>.png: the highway, the five notes and
   the five strike buttons (their texture checksums in global_gfx.tex), the game's own where a
   theme has none.
   config.launcher.theme = id of the theme on, "" for none. */
sealed class ThemeService
{
    readonly Ctx ctx;
    readonly SongService songs;
    readonly SemaphoreSlim gate = new(1, 1);
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    public ThemeService(Ctx ctx, SongService songs) { this.ctx = ctx; this.songs = songs; }

    public string Dir => Path.Combine(ctx.Data, "themes");
    string GameDir => Path.Combine(Dir, "_game");
    string Ffmpeg => Path.Combine(ctx.Honeycomb, "ffmpeg.exe");
    static string Q(string s) => "\"" + s + "\"";

    // slot -> texture checksum in zones\global\global_gfx.tex
    static readonly (string slot, string id)[] Slots =
    {
        ("highway", "2c0ed3fd"),
        ("gem_g", "151ee874"), ("gem_r", "27dd8dc4"), ("gem_y", "39e35d20"), ("gem_b", "c715d48d"), ("gem_o", "0243b96c"),
        ("btn_g", "31966e14"), ("btn_r", "e539f99e"), ("btn_y", "cb2dd2be"), ("btn_b", "06861f88"), ("btn_o", "86c8db75"),
        ("flame", "a38b2820")
    };

    /* the community's themes the launcher can bring in with a click (FastGH3's theme page) */
    sealed record Item(string Id, string Name, string Author, string Drive, string Size);
    static readonly Item[] Catalog =
    {
        new("warriorsofrock", "Guitar Hero: Warriors of Rock", "WOR Mod Team, donnaken15", "1-OJHUepRSOnFlGx_N8eQHLmVfzmRJkp9", "7.6 MB"),
        new("rockband1wiilite", "Rock Band 1 (Wii) (lite)", "donnaken15", "1A1PFl3ij102t29i27Zr1PS-Th8Th1227", "1.9 MB"),
        new("fretsonfire", "Frets on Fire", "donnaken15", "14yTJbB48RCYsAiTPCT_3rma4lDl6Sh22", "0.8 MB"),
        new("ghpced", "GHPCED", "Ateri", "1nGHM7UITPdzlLrDn3H7hriECikUbcr05", "0.5 MB"),
        new("8bit", "8-BIT", "3UP", "1A8nq4sBQEOd7aJQD9uViN_L7NSrLIU7n", "0.5 MB"),
        new("guitarhero2", "Guitar Hero 2", "donnaken15", "1UQhFV291yj6KbK7mckgTnrwTPkLEtj6F", "5.4 MB"),
        new("redstarpower", "Red Starpower", "Acai", "1xiUgH-X58lrYNyZK8cIimsqR1AF7954G", "7.3 MB"),
        new("rainbowgrybo", "Animated Rainbow (GRYBO)", "donnaken15", "1vUtDWAxj1QSLYwmWLm61g82c-stqiZXx", "4.8 MB"),
        new("feedback", "Feedback", "donnaken15", "1oDxOrQPL8nwbL0KxcwdbaG4tnO8JDjT0", "2 MB")
    };

    public string Current => (string)ctx.Section("launcher")["theme"] ?? "";

    public object State()
    {
        var list = new List<object> { new { id = "", name = "Guitar Hero III (original)", author = "Neversoft", kind = "game", preview = PreviewUrl("_game") } };
        var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(Dir))
            foreach (var d in Directory.GetDirectories(Dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var id = Path.GetFileName(d);
                if (id.StartsWith('_')) continue;
                var meta = Ctx.LoadJson(Path.Combine(d, "theme.json")) as JsonObject;
                if (meta == null) continue;
                have.Add(id);
                list.Add(new { id, name = (string)meta["name"] ?? id, author = (string)meta["author"] ?? "", kind = (string)meta["kind"] ?? "textures", preview = PreviewUrl(id) });
            }
        return new
        {
            current = Current, themes = list, gameRunning = ctx.GameRunning(),
            catalog = Catalog.Where(c => !have.Contains(c.Id)).Select(c => new { id = c.Id, name = c.Name, author = c.Author, size = c.Size })
        };
    }

    string PreviewUrl(string id) => File.Exists(Path.Combine(Dir, id, "preview", "highway.png")) ? $"https://gh3.themes/{Uri.EscapeDataString(id)}/preview/" : null;

    /* ---- bringing a theme in ---- */
    public async Task<object> Import(string file, string name = null, string author = null, string id = null)
    {
        await gate.WaitAsync();
        try
        {
            name ??= Path.GetFileNameWithoutExtension(file);
            id ??= Slug(name);
            var dir = Path.Combine(Dir, id);   // the same theme again replaces it
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var r = await songs.RunTool($"theme-import --src {Q(file)} --out {Q(dir)}");
            Ctx.SaveJson(Path.Combine(dir, "theme.json"), new JsonObject { ["name"] = name, ["author"] = author ?? "", ["kind"] = (string)r?["kind"] ?? "textures", ["source"] = Path.GetFileName(file), ["added"] = DateTime.Now.ToString("s") });
            await Previews(id, dir);
            return State();
        }
        finally { gate.Release(); }
    }

    public async Task<object> Download(string id)
    {
        var item = Catalog.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException("theme: " + id);
        var tmp = Path.Combine(ctx.Cache, "theme_dl");
        Directory.CreateDirectory(tmp);
        var file = Path.Combine(tmp, item.Id);
        using (var res = await Http.GetAsync("https://drive.google.com/uc?export=download&id=" + item.Drive, HttpCompletionOption.ResponseHeadersRead))
        {
            res.EnsureSuccessStatusCode();
            await using var fs = File.Create(file);
            await res.Content.CopyToAsync(fs);
        }
        try { return await Import(file, item.Name, item.Author, item.Id); }
        finally { try { File.Delete(file); } catch { } }
    }

    public object Remove(string id)
    {
        if (string.IsNullOrEmpty(id) || id.StartsWith('_') || id.Contains('\\') || id.Contains('/') || id.Contains("..")) throw new ArgumentException("theme: " + id);
        if (Current == id) throw new InvalidOperationException(Lang.Pick("That theme is on: switch to another one first.", "Ese tema está puesto: cambia a otro primero."));
        var dir = Path.Combine(Dir, id);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        return State();
    }

    /* ---- putting it on the game ---- */
    public async Task<object> Apply(string id)
    {
        if (ctx.GameRunning()) throw new InvalidOperationException(Lang.Pick("Close Guitar Hero III first.", "Cierra Guitar Hero III primero."));
        id ??= "";
        var dir = id == "" ? "" : Path.Combine(Dir, id);
        if (id != "" && !File.Exists(Path.Combine(dir, "theme.json"))) throw new ArgumentException("theme: " + id);
        await gate.WaitAsync();
        try
        {
            await songs.RunTool($"theme-apply --game {Q(ctx.Root)} --theme {Q(dir)}");
            ctx.Section("launcher")["theme"] = id;
            ctx.SaveConfig();
            ctx.Log("theme: " + (id == "" ? "(game's own)" : id));
            return State();
        }
        finally { gate.Release(); }
    }

    /* ---- the preview's textures ---- */
    /* at start: the game's own preview textures, and those of themes brought in without them */
    public async Task EnsureGamePreview()
    {
        if (!File.Exists(Ffmpeg)) return;
        await gate.WaitAsync();
        try
        {
            if (!File.Exists(Path.Combine(GameDir, "preview", "highway.png"))) await Previews("game", GameDir);
            if (Directory.Exists(Dir))
                foreach (var d in Directory.GetDirectories(Dir))
                {
                    var id = Path.GetFileName(d);
                    if (id.StartsWith('_') || !File.Exists(Path.Combine(d, "theme.json")) || File.Exists(Path.Combine(d, "preview", "highway.png"))) continue;
                    try { await Previews(id, d); } catch (Exception ex) { ctx.Log($"theme preview ({id}): {ex.Message}"); }
                }
        }
        catch (Exception ex) { ctx.Log("theme preview (game): " + ex.Message); }
        finally { gate.Release(); }
    }

    async Task Previews(string id, string dir)
    {
        var raw = Path.Combine(dir, "preview", "raw");
        var outDir = Path.Combine(dir, "preview");
        Directory.CreateDirectory(outDir);
        var r = await songs.RunTool($"theme-textures --game {Q(ctx.Root)} --theme {Q(id == "game" ? "game" : dir)} --out {Q(raw)}");
        var files = new Dictionary<string, string>();
        if (r?["textures"] is JsonArray arr)
            foreach (var t in arr.OfType<JsonObject>()) files[(string)t["id"]] = (string)t["file"];
        foreach (var (slot, tex) in Slots)
        {
            var dest = Path.Combine(outDir, slot + ".png");
            if (files.TryGetValue(tex, out var f))
            {
                try { await GameAssets.Run(Ffmpeg, $"-hide_banner -loglevel error -y -i {Q(Path.Combine(raw, f))} -frames:v 1 -update 1 {Q(dest)}"); continue; }
                catch (Exception ex) { ctx.Log($"theme preview {id}/{slot}: {ex.Message}"); }
            }
            // the theme doesn't change it: the game's own
            var game = Path.Combine(GameDir, "preview", slot + ".png");
            if (id != "game" && File.Exists(game)) File.Copy(game, dest, true);
        }
        try { Directory.Delete(raw, true); } catch { }
    }

    public async Task<object> Notes(string song) => await songs.RunTool($"notes --game {Q(ctx.Root)} --song {Q(song)}");

    static string Slug(string s)
    {
        var chars = s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        var slug = new string(chars);
        return slug.Length == 0 ? "theme" : slug[..Math.Min(slug.Length, 40)];
    }
}
