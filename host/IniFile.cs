using System.Text;

namespace GH3Launcher;

/* An INI file kept as its lines, so a rewrite keeps the order, comments and keys it does not know
   (x360ce.ini, GH3+'s settings.ini). Section and key names compare case-insensitively. */
sealed class IniFile
{
    readonly List<string> lines = new();

    public IniFile() { }
    public IniFile(string text)
    {
        foreach (var l in (text ?? "").Replace("\r\n", "\n").Split('\n')) lines.Add(l.TrimEnd('\r'));
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
    }
    public static IniFile Load(string path) => new(File.Exists(path) ? File.ReadAllText(path) : "");

    static string SectionOf(string line)
    {
        var t = line.Trim();
        return t.StartsWith('[') && t.EndsWith(']') ? t[1..^1].Trim() : null;
    }

    static bool KeyOf(string line, out string key, out string value)
    {
        key = value = null;
        var t = line.TrimStart();
        if (t.Length == 0 || t[0] == ';' || t[0] == '#' || t[0] == '[') return false;
        int eq = t.IndexOf('=');
        if (eq <= 0) return false;
        key = t[..eq].Trim();
        value = t[(eq + 1)..].Trim();
        return true;
    }

    (int start, int end) Range(string section)
    {
        int start = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var s = SectionOf(lines[i]);
            if (s == null) continue;
            if (start >= 0) return (start, i);
            if (string.Equals(s, section, StringComparison.OrdinalIgnoreCase)) start = i;
        }
        return start >= 0 ? (start, lines.Count) : (-1, -1);
    }

    public IEnumerable<string> Sections() => lines.Select(SectionOf).Where(s => s != null);
    public bool HasSection(string section) => Range(section).start >= 0;

    public List<KeyValuePair<string, string>> Keys(string section)
    {
        var r = new List<KeyValuePair<string, string>>();
        var (s, e) = Range(section);
        for (int i = s + 1; s >= 0 && i < e; i++)
            if (KeyOf(lines[i], out var k, out var v)) r.Add(new(k, v));
        return r;
    }

    public string Get(string section, string key, string def = null)
    {
        var (s, e) = Range(section);
        for (int i = s + 1; s >= 0 && i < e; i++)
            if (KeyOf(lines[i], out var k, out var v) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return v;
        return def;
    }

    public void Set(string section, string key, string value)
    {
        var (s, e) = Range(section);
        if (s < 0)
        {
            if (lines.Count > 0 && lines[^1].Length > 0) lines.Add("");
            lines.Add($"[{section}]");
            lines.Add($"{key}={value}");
            return;
        }
        for (int i = s + 1; i < e; i++)
            if (KeyOf(lines[i], out var k, out _) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) { lines[i] = $"{k}={value}"; return; }
        int at = e;
        while (at > s + 1 && lines[at - 1].Trim().Length == 0) at--;
        lines.Insert(at, $"{key}={value}");
    }

    public void Remove(string section, string key)
    {
        var (s, e) = Range(section);
        for (int i = s + 1; s >= 0 && i < e; i++)
            if (KeyOf(lines[i], out var k, out _) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) { lines.RemoveAt(i); return; }
    }

    public void RemoveSection(string section)
    {
        var (s, e) = Range(section);
        if (s < 0) return;
        lines.RemoveRange(s, e - s);
    }

    /* Replaces a whole section's keys (keeps its place in the file). */
    public void SetSection(string section, IEnumerable<KeyValuePair<string, string>> values)
    {
        var (s, e) = Range(section);
        var body = values.Select(kv => $"{kv.Key}={kv.Value}").ToList();
        if (s < 0)
        {
            if (lines.Count > 0 && lines[^1].Length > 0) lines.Add("");
            lines.Add($"[{section}]");
            lines.AddRange(body);
            return;
        }
        lines.RemoveRange(s + 1, e - s - 1);
        if (s + 1 < lines.Count) body.Add("");
        lines.InsertRange(s + 1, body);
    }

    public override string ToString() => string.Join("\r\n", lines) + "\r\n";

    public void Save(string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, ToString(), new UTF8Encoding(false));
        File.Move(tmp, path, true);
    }
}
