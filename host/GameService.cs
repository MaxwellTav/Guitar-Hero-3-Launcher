using System.Diagnostics;

namespace GH3Launcher;

/* Starting GH3.exe from its folder (after the launcher has written InputPlus.ini) and watching it
   until it exits. */
sealed class GameService
{
    readonly Ctx ctx;
    Process proc;
    public DateTime Started { get; private set; }
    public event Action<double, int> Exited;

    public GameService(Ctx ctx) { this.ctx = ctx; }

    public bool Running
    {
        get
        {
            try { if (proc != null && !proc.HasExited) return true; } catch { }
            return Attach();
        }
    }

    /* Picks up a GH3 that was already running (started before the launcher). */
    public bool Attach()
    {
        int pid = Native.FindGame(ctx.Root);
        if (pid == 0) return false;
        if (proc != null && !proc.HasExited && proc.Id == pid) return true;
        try
        {
            proc = Process.GetProcessById(pid);
            proc.EnableRaisingEvents = true;
            proc.Exited += OnExit;
            Started = proc.StartTime;
            return true;
        }
        catch { return false; }
    }

    public int Launch(bool highPriority)
    {
        if (Running) throw new InvalidOperationException("Guitar Hero III ya está abierto.");
        var exe = Path.Combine(ctx.Root, "GH3.exe");
        var psi = new ProcessStartInfo(exe) { WorkingDirectory = ctx.Root, UseShellExecute = false };
        proc = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar GH3.exe");
        proc.EnableRaisingEvents = true;
        proc.Exited += OnExit;
        Started = DateTime.Now;
        if (highPriority) { try { proc.PriorityClass = ProcessPriorityClass.High; } catch { } }
        ctx.Log($"GH3 started (pid {proc.Id})");
        return proc.Id;
    }

    void OnExit(object sender, EventArgs e)
    {
        var p = sender as Process;
        int code = 0;
        try { code = p.ExitCode; } catch { }
        var secs = (DateTime.Now - Started).TotalSeconds;
        ctx.Log($"GH3 exited ({code}) after {secs:F0}s");
        Exited?.Invoke(secs, code);
    }

    public void Kill() { try { if (Running) proc.Kill(); } catch { } }

    public void Front()
    {
        try { if (Running) Native.Front(proc.MainWindowHandle); } catch { }
    }
}
