namespace GH3Launcher;

/* What the launcher needs from the game folder, checked at every start and shown on Inicio/Home
   when something is wrong. level: "error" (a main feature can't work), "warn" (an optional one),
   fix: what the page offers ("admin" = start again as administrator, "assets" = read the game's art
   again, "setup" = run the installer again). Texts in both languages; the page picks one. */
static class Health
{
    public sealed record Item(string Id, string Level, string En, string Es, string DetailEn, string DetailEs, string Fix);

    static readonly string[] ToolkitFiles =
    {
        "GH-Toolkit-Core.dll", "Melanchall.DryWetMidi.dll", "FFMpegCore.dll", "Instances.dll", "NAudio.Core.dll",
        "YamlDotNet.dll", "Newtonsoft.Json.dll", "INIFileParser.dll", "Ude.NetStandard.dll", "ICSharpCode.SharpZipLib.dll"
    };

    public static List<Item> Check(Ctx ctx, GameAssets assets)
    {
        var list = new List<Item>();
        string R(params string[] p) => Path.Combine(new[] { ctx.Root }.Concat(p).ToArray());

        // the game itself
        var missingGame = new[] { "DATA\\PAK\\qb.pak.xen", "DATA\\PAK\\qb.pab.xen", "DATA\\MUSIC", "DATA\\SONGS", "DATA\\ZONES\\global.pak.xen" }
            .Where(p => !File.Exists(R(p)) && !Directory.Exists(R(p))).ToList();
        if (missingGame.Count > 0)
            list.Add(new("game", "error", "Guitar Hero III's files are incomplete", "Los archivos de Guitar Hero III están incompletos",
                "Missing: " + string.Join(", ", missingGame) + ". Reinstall or repair the game.", "Faltan: " + string.Join(", ", missingGame) + ". Reinstala o repara el juego.", null));

        // it has to write into DATA (song lists, songs) and next to GH3.exe (x360ce.ini)
        if (!Writable(R("DATA", "PAK")) || !Writable(ctx.Root))
            list.Add(new("writable", "error", "The game folder is read-only for this user", "La carpeta del juego es de solo lectura para este usuario",
                "It is probably inside Program Files. Run the launcher as administrator, or move the game to a folder of yours (e.g. C:\\Games).",
                "Seguramente está dentro de Archivos de programa. Abre el launcher como administrador, o mueve el juego a una carpeta tuya (por ejemplo C:\\Juegos).", "admin"));

        // Honeycomb's toolkit + ffmpeg: importing songs, previews, the game's art
        var missingTk = ToolkitFiles.Where(f => !File.Exists(Path.Combine(ctx.Honeycomb, f))).ToList();
        if (!Directory.Exists(Path.Combine(ctx.Honeycomb, "Resources"))) missingTk.Add("Resources\\");
        if (missingTk.Count > 0)
            list.Add(new("toolkit", "error", "Honeycomb (GH Toolkit) is missing or incomplete", "Falta Honeycomb (GH Toolkit) o está incompleto",
                "Importing songs needs it. Missing in Honeycomb-GUI: " + string.Join(", ", missingTk.Take(5)) + ". Run \"GH3 Launcher Setup\" again.",
                "Importar canciones lo necesita. Falta en Honeycomb-GUI: " + string.Join(", ", missingTk.Take(5)) + ". Ejecuta \"GH3 Launcher Setup\" otra vez.", "setup"));
        var missingFf = new[] { "ffmpeg.exe", "ffprobe.exe" }.Where(f => !File.Exists(Path.Combine(ctx.Honeycomb, f))).ToList();
        if (missingFf.Count > 0)
            list.Add(new("ffmpeg", "error", "FFmpeg is missing", "Falta FFmpeg",
                "Honeycomb-GUI\\" + string.Join(", ", missingFf) + " converts the audio of imported songs and the game's art. Run \"GH3 Launcher Setup\" again.",
                "Honeycomb-GUI\\" + string.Join(", ", missingFf) + " convierte el audio de las canciones importadas y el arte del juego. Ejecuta \"GH3 Launcher Setup\" otra vez.", "setup"));

        // optional parts
        var dll = InputService.DllState(ctx.Root);
        if (!File.Exists(R("x360ce_x86.exe")))
            list.Add(new("x360ce", "warn", "x360ce is not installed", "x360ce no está instalado",
                "Guitars and controllers go through it (GH3's own keyboard works anyway). Run \"GH3 Launcher Setup\" to add it.",
                "Las guitarras y los controles pasan por él (el teclado propio de GH3 funciona igual). Ejecuta \"GH3 Launcher Setup\" para agregarlo.", "setup"));
        else if (dll != "ok")
            list.Add(new("x360ce-dll", "error", dll == "missing" ? "x360ce's xinput1_3.dll is missing" : "xinput1_3.dll is not x360ce's",
                dll == "missing" ? "Falta el xinput1_3.dll de x360ce" : "El xinput1_3.dll no es el de x360ce",
                "Without it GH3 doesn't get your guitar and controllers as x360ce maps them. Open x360ce: it offers to create it (answer yes), then close it.",
                "Sin él, GH3 no recibe tu guitarra ni tus controles como los mapea x360ce. Abre x360ce: ofrece crearlo (di que sí) y luego ciérralo.", "x360ce"));
        if (!File.Exists(R("dinput8.dll")) || !File.Exists(R("PLUGINS", "core.dll")))
            list.Add(new("gh3plus", "warn", "GH3+ (plugin loader) not found", "No se encontró GH3+ (cargador de plugins)",
                "Without it the InputPlus plugin (strum style, keyboard 2, whammy), FastGH3 and the other plugins do not load. BetterGH3 includes it.",
                "Sin él no cargan el plugin InputPlus (estilo de rasgueo, Teclado 2, whammy), FastGH3 ni los demás plugins. BetterGH3 lo incluye.", null));
        if (ctx.Root.Length >= 46)
            list.Add(new("path", "warn", "The game's folder path is long", "La ruta de la carpeta del juego es larga",
                "GH3 can fail to start from long paths. If it closes on start, move the game to a shorter folder (e.g. C:\\Games\\GH3).",
                "GH3 puede fallar al arrancar desde rutas largas. Si se cierra al abrir, mueve el juego a una carpeta más corta (por ejemplo C:\\Juegos\\GH3).", null));
        if (!assets.Ready && missingTk.Count == 0 && missingFf.Count == 0 && missingGame.Count == 0)
            list.Add(new("assets", "warn", "The game's art and sounds are not ready", "El arte y los sonidos del juego no están listos",
                assets.Error ?? "They are read from your copy of the game.", assets.Error ?? "Se leen de tu copia del juego.", "assets"));
        return list;
    }

    static bool Writable(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return true;
            var probe = Path.Combine(dir, ".gh3launcher_write_test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}
