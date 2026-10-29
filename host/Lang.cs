using System.Text.Json.Nodes;

namespace GH3Launcher;

/* The launcher's language: English (the default) or Spanish, Launcher\config.json launcher.language.
   The page translates itself (web\js\i18n.js); this is for what the native side says itself. */
static class Lang
{
    public static string Current { get; private set; } = "en";
    public static bool Spanish => Current == "es";

    public static string Pick(string en, string es) => Spanish ? es : en;

    public static void Set(string lang) => Current = lang == "es" ? "es" : "en";

    /* before Ctx exists (Program.Main) */
    public static void Load(string root)
    {
        try
        {
            if (Ctx.LoadJson(Path.Combine(root, "Launcher", "config.json")) is JsonObject cfg && (string)cfg["launcher"]?["language"] is string l) Set(l);
        }
        catch { }
    }
}
