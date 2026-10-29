using System.Globalization;
using System.Text.Json.Nodes;

namespace GH3Launcher;

/* Time played (Launcher\config.json "stats"): total seconds, sessions, the longest one, the last
   play. A session the launcher saw start but not end (it was closed while GH3 ran) is closed on the
   next start, ending at the save folder's last write; one still running is picked up again. */
sealed class Stats
{
    readonly Ctx ctx;
    public Stats(Ctx ctx) { this.ctx = ctx; }

    JsonObject S => ctx.Section("stats");
    static double Num(JsonNode n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : 0;

    public void Started(DateTime at)
    {
        S["pending"] = at.ToString("o", CultureInfo.InvariantCulture);
        ctx.SaveConfig();
    }

    public void Ended(double seconds, DateTime? at = null)
    {
        var s = S;
        s.Remove("pending");
        if (seconds >= 5 && seconds < 7 * 86400)
        {
            s["seconds"] = Math.Round(Num(s["seconds"]) + seconds);
            s["sessions"] = Num(s["sessions"]) + 1;
            s["longest"] = Math.Round(Math.Max(Num(s["longest"]), seconds));
            s["last"] = (at ?? DateTime.Now).ToString("o", CultureInfo.InvariantCulture);
        }
        ctx.SaveConfig();
    }

    /* at start: a session left open by a launcher that was closed meanwhile */
    public void Recover(bool gameRunning, DateTime runningSince)
    {
        if (S["pending"] is not JsonValue p || !DateTime.TryParse((string)p, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start)) return;
        if (gameRunning)
        {
            if (Math.Abs((runningSince - start).TotalMinutes) > 2) Started(runningSince);   // another session than the one noted
            return;
        }
        DateTime end = start;
        try
        {
            if (Directory.Exists(ctx.SaveDir))
                foreach (var f in Directory.EnumerateFiles(ctx.SaveDir))
                {
                    var w = File.GetLastWriteTime(f);
                    if (w > end && w <= DateTime.Now) end = w;
                }
        }
        catch { }
        Ended((end - start).TotalSeconds, end);   // nothing written after it started: dropped (Ended ignores < 5 s)
    }

    public object State(double current) => new
    {
        seconds = Num(S["seconds"]),
        sessions = (int)Num(S["sessions"]),
        longest = Num(S["longest"]),
        last = (string)S["last"],
        current
    };
}
