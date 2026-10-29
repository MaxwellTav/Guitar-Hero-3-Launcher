using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GH3Launcher;

/* Who sings a song, so GH3 puts the right singer on stage: "male", "female" or "bret" (Poison's Bret
   Michaels, GH3's own character). MusicBrainz is asked once per artist (and per song when a band has
   lead singers of both kinds), one request a second as its rules ask; answers are kept in
   Launcher\singers.json. Nothing found = null (the game's default, the male singer). */
sealed class SingerLookup
{
    readonly Ctx ctx;
    readonly HttpClient http;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly string cachePath;
    readonly JsonObject cache;
    DateTime last = DateTime.MinValue;

    static readonly string[] Bret = { "poison", "bretmichaels" };

    public SingerLookup(Ctx ctx)
    {
        this.ctx = ctx;
        cachePath = Path.Combine(ctx.Data, "singers.json");
        cache = Ctx.LoadJson(cachePath) ?? new JsonObject();
        http = new HttpClient { BaseAddress = new Uri("https://musicbrainz.org/ws/2/"), Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GH3Launcher", "1.0"));
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Guitar Hero III song importer)"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    static string Norm(string s) => Regex.Replace((s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD), @"[^a-z0-9]", "");

    /* Without asking anyone: Bret's band, or what the song itself says. */
    public static string Offline(string artist, string vocalGender)
    {
        if (Bret.Contains(Norm(artist))) return "bret";
        return vocalGender switch { "female" => "female", "male" => "male", _ => null };
    }

    public async Task<string> Find(string artist, string title, CancellationToken ct = default)
    {
        var a = Norm(artist);
        if (a.Length == 0) return null;
        if (Bret.Contains(a)) return "bret";
        var songKey = "song:" + a + "|" + Norm(title);
        lock (cache)
        {
            if (cache.TryGetPropertyValue(songKey, out var s)) return (string)s;
            if (cache.TryGetPropertyValue("artist:" + a, out var v) && (string)v != "mixed") return (string)v;
        }
        string result;
        try
        {
            var byArtist = await ArtistSinger(artist, ct);
            if (byArtist == "mixed")
            {
                result = await RecordingSinger(artist, title, ct);
                Remember(songKey, result);
            }
            else result = byArtist;
            Remember("artist:" + a, byArtist);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            ctx.Log("singer " + artist + ": " + ex.Message);
            return null;   // offline: not remembered, asked again next time
        }
        return result == "mixed" ? null : result;
    }

    void Remember(string key, string value)
    {
        lock (cache)
        {
            cache[key] = value;
            try { Ctx.SaveJson(cachePath, cache); } catch { }
        }
    }

    /* One request at a time, at most one a second; "busy" (503) is MusicBrainz asking to slow down:
       wait longer and try again. */
    async Task<JsonObject> Get(string url, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                var wait = last.AddMilliseconds(attempt == 0 ? 1200 : 1500 << attempt) - DateTime.UtcNow;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                last = DateTime.UtcNow;
                using var r = await http.GetAsync(url, ct);
                if ((int)r.StatusCode == 503 && attempt < 4) continue;
                if ((int)r.StatusCode == 503) throw new HttpRequestException("MusicBrainz ocupado");
                r.EnsureSuccessStatusCode();
                return JsonNode.Parse(await r.Content.ReadAsStringAsync(ct)) as JsonObject;
            }
        }
        finally { gate.Release(); }
    }

    static string Q(string s) => Uri.EscapeDataString("\"" + (s ?? "").Replace("\"", "") + "\"");

    static string Gender(JsonNode person) => ((string)person?["gender"])?.ToLowerInvariant() switch { "female" => "female", "male" => "male", _ => null };

    /* "male"/"female", "mixed" (lead singers of both), or null (unknown). */
    async Task<string> ArtistSinger(string artist, CancellationToken ct)
    {
        var found = await Get($"artist/?query=artist:{Q(artist)}&limit=3&fmt=json", ct);
        var best = (found?["artists"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(x => ((int?)x["score"] ?? 0) >= 90 && Norm((string)x["name"]) == Norm(artist))
            ?? (found?["artists"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x => ((int?)x["score"] ?? 0) >= 95);
        if (best == null) return null;
        if ((string)best["type"] == "Person") return Gender(best);

        // a band: its lead singers (current ones first)
        var band = await Get($"artist/{best["id"]}?inc=artist-rels&fmt=json", ct);
        var leads = (band?["relations"] as JsonArray)?.OfType<JsonObject>()
            .Where(r => (string)r["type"] == "member of band" && (string)r["direction"] == "backward"
                        && (r["attributes"] as JsonArray)?.Any(t => ((string)t)?.Contains("lead vocals", StringComparison.OrdinalIgnoreCase) == true) == true)
            .OrderBy(r => (bool?)r["ended"] == true ? 1 : 0)
            .Select(r => (string)r["artist"]?["id"]).Where(id => id != null).Distinct().Take(3).ToList() ?? new();
        var genders = new HashSet<string>();
        foreach (var id in leads)
        {
            var g = Gender(await Get($"artist/{id}?fmt=json", ct));
            if (g != null) genders.Add(g);
        }
        return genders.Count switch { 0 => null, 1 => genders.First(), _ => "mixed" };
    }

    /* The song's own credits: who sings lead on that recording. */
    async Task<string> RecordingSinger(string artist, string title, CancellationToken ct)
    {
        var found = await Get($"recording/?query=recording:{Q(title)}%20AND%20artist:{Q(artist)}&limit=3&fmt=json", ct);
        foreach (var rec in (found?["recordings"] as JsonArray)?.OfType<JsonObject>().Where(x => ((int?)x["score"] ?? 0) >= 90).Take(2) ?? Enumerable.Empty<JsonObject>())
        {
            var full = await Get($"recording/{rec["id"]}?inc=artist-rels&fmt=json", ct);
            var lead = (full?["relations"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(r => (string)r["type"] == "vocal" && (r["attributes"] as JsonArray)?.Any(t => ((string)t)?.Contains("lead", StringComparison.OrdinalIgnoreCase) == true) == true);
            var id = (string)lead?["artist"]?["id"];
            if (id == null) continue;
            var g = Gender(await Get($"artist/{id}?fmt=json", ct));
            if (g != null) return g;
        }
        return null;
    }
}
