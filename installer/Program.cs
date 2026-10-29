using System.Diagnostics;

namespace GH3Setup;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var o = Options.Parse(args);
        T.Lang = o.Lang == "es" ? "es" : "en";
        ApplicationConfiguration.Initialize();
        Payload payload;
        try { payload = Payload.Open(o.PayloadPath); }
        catch (Exception ex)
        {
            if (o.Silent) { Console.Error.WriteLine(ex.Message); return 2; }
            MessageBox.Show(ex.Message, "GH3 Launcher Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
        using (payload)
        {
            if (o.Silent) return Silent(o, payload);
            Application.Run(new SetupForm(o, payload));
        }
        return 0;
    }

    /* --silent --target <folder>: no window, for scripts and tests; the result goes to a log */
    static int Silent(Options o, Payload payload)
    {
        var log = Path.Combine(Path.GetTempPath(), "GH3LauncherSetup.log");
        try
        {
            if (!Detect.IsGame(o.Target)) throw new InvalidOperationException("GH3.exe not found in " + o.Target);
            new Installer(payload, o, (p, what) => { }).Run();
            File.WriteAllText(log, $"{DateTime.Now:s} installed into {o.Target}\r\n");
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(log, $"{DateTime.Now:s} failed: {ex}\r\n");
            return 1;
        }
    }

    /* the same installer again, as administrator, straight to installing */
    public static bool Elevate(Options o)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath, o.ToArgs() + " --elevated") { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch { return false; }   // the player said no
    }
}
