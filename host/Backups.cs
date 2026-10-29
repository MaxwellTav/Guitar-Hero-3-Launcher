namespace GH3Launcher;

/* Copies taken before the launcher changes the setlist:
     Launcher\backups\qb\original\   qb.pak.xen + qb.pab.xen as they were before the first change
     Launcher\backups\qb\<stamp>\    the last few, newest last
     Launcher\backups\save\<stamp>\  GH3's save folder (Documents\Aspyr\Guitar Hero III\Save)
   and, once, Honeycomb's own backup (Honeycomb-GUI\Backups\GH3\qb.*) so Honeycomb treats the game
   as already prepared and never re-patches a setlist that has customs in it. */
sealed class Backups
{
    readonly Ctx ctx;
    const int KeepQb = 8, KeepSave = 12;
    public Backups(Ctx ctx) { this.ctx = ctx; }

    string QbDir => Path.Combine(ctx.Backups, "qb");
    string SaveDir => Path.Combine(ctx.Backups, "save");
    string Pab => ctx.QbPak.Replace(".pak.xen", ".pab.xen");
    static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

    public void BeforeChange(string why)
    {
        var original = Path.Combine(QbDir, "original");
        if (!File.Exists(Path.Combine(original, "qb.pak.xen")))
        {
            Directory.CreateDirectory(original);
            File.Copy(ctx.QbPak, Path.Combine(original, "qb.pak.xen"), true);
            File.Copy(Pab, Path.Combine(original, "qb.pab.xen"), true);
        }
        var hc = Path.Combine(ctx.Honeycomb, "Backups", "GH3");
        if (Directory.Exists(ctx.Honeycomb) && !File.Exists(Path.Combine(hc, "qb.pak.xen")))
        {
            Directory.CreateDirectory(hc);
            File.Copy(Path.Combine(original, "qb.pak.xen"), Path.Combine(hc, "qb.pak.xen"), true);
            File.Copy(Path.Combine(original, "qb.pab.xen"), Path.Combine(hc, "qb.pab.xen"), true);
        }
        var stamp = Stamp();
        var qb = Path.Combine(QbDir, stamp);
        Directory.CreateDirectory(qb);
        File.Copy(ctx.QbPak, Path.Combine(qb, "qb.pak.xen"), true);
        File.Copy(Pab, Path.Combine(qb, "qb.pab.xen"), true);
        File.WriteAllText(Path.Combine(qb, "reason.txt"), why);
        BackupSave(stamp, why);
        Trim(QbDir, KeepQb);
    }

    /* Puts back the newest setlist backup (after a change that failed half way). */
    public void RestoreLatestQb()
    {
        var last = Directory.GetDirectories(QbDir).Where(d => Path.GetFileName(d) != "original").OrderBy(d => d).LastOrDefault();
        if (last == null) return;
        File.Copy(Path.Combine(last, "qb.pak.xen"), ctx.QbPak, true);
        File.Copy(Path.Combine(last, "qb.pab.xen"), Pab, true);
    }

    public string BackupSave(string stamp = null, string why = "manual")
    {
        if (!Directory.Exists(ctx.SaveDir)) return null;
        var dest = Path.Combine(SaveDir, stamp ?? Stamp());
        CopyDir(ctx.SaveDir, dest);
        File.WriteAllText(Path.Combine(dest, "reason.txt"), why);
        Trim(SaveDir, KeepSave);
        return dest;
    }

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, d)));
        foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(f, Path.Combine(to, Path.GetRelativePath(from, f)), true);
    }

    static void Trim(string dir, int keep)
    {
        var all = Directory.GetDirectories(dir).Where(d => Path.GetFileName(d) != "original").OrderBy(d => d).ToList();
        foreach (var d in all.Take(Math.Max(0, all.Count - keep))) { try { Directory.Delete(d, true); } catch { } }
    }

    public object List()
    {
        object Entries(string dir) => !Directory.Exists(dir) ? Array.Empty<object>() :
            Directory.GetDirectories(dir).OrderByDescending(d => d).Select(d => new
            {
                id = Path.GetFileName(d),
                reason = File.Exists(Path.Combine(d, "reason.txt")) ? File.ReadAllText(Path.Combine(d, "reason.txt")) : "",
                size = Directory.GetFiles(d, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
            }).ToArray();
        return new { qb = Entries(QbDir), save = Entries(SaveDir) };
    }

    public void Restore(string kind, string id)
    {
        if (ctx.GameRunning()) throw new InvalidOperationException("Cierra Guitar Hero III antes de restaurar.");
        if (id.Contains("..") || id.Contains('\\') || id.Contains('/')) throw new ArgumentException("id");
        if (kind == "qb")
        {
            var src = Path.Combine(QbDir, id);
            if (!File.Exists(Path.Combine(src, "qb.pak.xen"))) throw new FileNotFoundException("No existe ese respaldo");
            BeforeChange("antes de restaurar " + id);
            File.Copy(Path.Combine(src, "qb.pak.xen"), ctx.QbPak, true);
            File.Copy(Path.Combine(src, "qb.pab.xen"), Pab, true);
        }
        else if (kind == "save")
        {
            var src = Path.Combine(SaveDir, id);
            if (!Directory.Exists(src)) throw new FileNotFoundException("No existe ese respaldo");
            BackupSave(null, "antes de restaurar " + id);
            if (Directory.Exists(ctx.SaveDir)) Directory.Delete(ctx.SaveDir, true);
            CopyDir(src, ctx.SaveDir);
            var reason = Path.Combine(ctx.SaveDir, "reason.txt");
            if (File.Exists(reason)) File.Delete(reason);
        }
        else throw new ArgumentException("kind");
    }
}
