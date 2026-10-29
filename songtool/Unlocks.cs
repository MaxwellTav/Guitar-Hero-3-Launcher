using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GH_Toolkit_Core.Checksum;
using GH_Toolkit_Core.PAK;
using static GH_Toolkit_Core.QB.QB;
using static GH_Toolkit_Core.QB.QBConstants;

namespace SongTool;

/* Cheats and unlocks.
   The cheat list (names, fret patterns) comes from BetterGH3's DATA\patch.pak.xen, which overrides
   the game's own scripts\guitar\menu\menu_cheats.qb. Fret masks: 65536 green, 4096 red, 256 yellow,
   16 blue, 1 orange (several bits = a chord).
   Unlocks are a script, gh3launcher_unlocks, added to scripts\guitar\guitar_memcard.qb and called at
   the end of memcard_post_load_progress (after a save loads) and of setup_globaltags (a new save):
   it sets the same global tags the game's own "unlock" cheats set, so the game saves them itself. */
sealed class UnlockOptions
{
    public bool Songs { get; set; }       // Quickplay/Bonus songs (the "unlocked all songs" cheat, without fake stars)
    public bool Items { get; set; }       // store: guitars, basses, finishes, secret characters, outfits, styles, videos
    public bool Cheats { get; set; }      // every cheat available in Options > Cheats
    public int Cash { get; set; }         // band cash at least this much (0 = leave it)
    public List<string> On { get; set; } = new();   // cheats switched on at load (Cheat_NoFail ...)
    public int Hyperspeed { get; set; }   // 0 = leave it, 1..5
    public bool Complete { get; set; }    // a career band "Complete" with every tier and boss beaten, on every difficulty
    public bool Any => Songs || Items || Cheats || Cash > 0 || On.Count > 0 || Hyperspeed > 0 || Complete;
}

static class Unlocks
{
    const string MemcardRef = "scripts\\guitar\\guitar_memcard.qb";
    const string TagsRef = "scripts\\guitar\\guitar_globaltags.qb";
    const string CheatsRef = "scripts\\guitar\\menu\\menu_cheats.qb";
    const string ScriptName = "gh3launcher_unlocks";
    static readonly string[] HelperNames = { "gh3launcher_complete", "gh3launcher_complete_list" };
    static readonly string[] CheatTags = { "AirGuitar", "PerformanceMode", "Hyperspeed", "NoFail", "EasyExpert", "PrecisionMode", "LargeGems", "BretMichaels" };
    static readonly string[] OnAllowed = { "Cheat_NoFail", "Cheat_AirGuitar", "Cheat_PerformanceMode", "Cheat_EasyExpert", "Cheat_PrecisionMode", "Cheat_LargeGems", "Cheat_BretMichaels" };

    /* ---- the cheat list ---- */
    public static object List(string game)
    {
        var cheats = new List<object>();
        string source = "qb";
        var patch = Path.Combine(game, "DATA", "patch.pak.xen");
        if (File.Exists(patch))
        {
            foreach (var (name, entry) in PAK.PakEntryDictFromFile(patch))
            {
                if (!name.EndsWith(".qb", StringComparison.OrdinalIgnoreCase)) continue;
                var items = TryDecompile(entry.EntryData);
                var list = items?.FirstOrDefault(i => string.Equals(i.Name, "guitar_hero_cheats", StringComparison.OrdinalIgnoreCase));
                if (list == null) continue;
                cheats = ParseCheats(QbToTextString(new List<QBItem> { list }));
                source = "patch";
                break;
            }
        }
        if (cheats.Count == 0)
        {
            var pak = PAK.PakEntryDictFromFile(Setlist.QbPak(game));
            var items = TryDecompile(pak[CheatsRef].EntryData);
            var list = items.First(i => string.Equals(i.Name, "guitar_hero_cheats", StringComparison.OrdinalIgnoreCase));
            cheats = ParseCheats(QbToTextString(new List<QBItem> { list }));
        }
        return new { cheats, source, applied = IsApplied(game) };
    }

    static List<QBItem> TryDecompile(byte[] data)
    {
        try { return DecompileQb(data, "big", "", "GH3", "PC"); } catch { return null; }
    }

    static List<object> ParseCheats(string text)
    {
        var r = new List<object>();
        foreach (Match m in Regex.Matches(text, @"name = (\S+)\s+name_text = (""[^""]*""|\S+)\s+var = (\S+)\s+unlock_pattern = \[([\s\d]*)\]"))
            r.Add(new
            {
                name = m.Groups[1].Value,
                text = m.Groups[2].Value.Trim('"'),
                var = m.Groups[3].Value,
                pattern = m.Groups[4].Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
            });
        return r;
    }

    /* ---- the unlock script ---- */
    static string Hash(string name) => CRC.QBKey(name).ToLowerInvariant();

    public static bool IsApplied(string game)
    {
        var pak = PAK.PakEntryDictFromFile(Setlist.QbPak(game));
        var text = QbToTextString(DecompileQb(pak[MemcardRef].EntryData, "big", "", "GH3", "PC"));
        return Regex.IsMatch(text, $@"^script ({ScriptName}|{Hash(ScriptName)})\b", RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    public static object Apply(string game, string optionsPath)
    {
        var o = JsonSerializer.Deserialize<UnlockOptions>(File.ReadAllText(optionsPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        o.On = o.On.Where(OnAllowed.Contains).Distinct().ToList();
        if (o.On.Contains("Cheat_EasyExpert") && o.On.Contains("Cheat_PrecisionMode")) o.On.Remove("Cheat_PrecisionMode");
        o.Hyperspeed = Math.Clamp(o.Hyperspeed, 0, 5);
        o.Cash = Math.Clamp(o.Cash, 0, 9_999_999);

        var qbPak = Setlist.QbPak(game);
        var pak = PAK.PakEntryDictFromFile(qbPak);
        var mem = pak[MemcardRef];
        var tags = pak[TagsRef];
        var memText = Strip(QbToTextString(DecompileQb(mem.EntryData, "big", "", "GH3", "PC")));
        var tagText = Strip(QbToTextString(DecompileQb(tags.EntryData, "big", "", "GH3", "PC")));
        if (o.Any)
        {
            memText = AddCall(memText, "memcard_post_load_progress") + "\n" + Script(o) + (o.Complete ? "\n" + CompleteScripts : "");
            tagText = AddCall(tagText, "setup_globaltags");
        }
        mem.OverwriteData(Compile(memText, MemcardRef));
        tags.OverwriteData(Compile(tagText, TagsRef));
        var (pakData, pabData) = new PAK.PakCompiler(GAME_GH3, CONSOLE_PC, split: true).CompilePakFromDictionary(pak);
        Setlist.WritePak(qbPak, pakData, pabData);
        var applied = IsApplied(game);
        if (applied != o.Any) throw new InvalidOperationException(Program.T("The unlock patch did not come out as expected.", "El parche de desbloqueos no quedó como se esperaba."));
        return new { applied };
    }

    static byte[] Compile(string text, string name)
    {
        var (items, _) = ParseQFile(text, "PC", "GH3");
        var bytes = CompileQbFile(items, name, "GH3", "PC");
        // must decompile again (a broken script would only show up in the game)
        DecompileQb(bytes, "big", "", "GH3", "PC");
        return bytes;
    }

    /* Removes an earlier gh3launcher_unlocks and its helpers (by name or by their QB keys once
       decompiled) and the calls to gh3launcher_unlocks. */
    static string Strip(string text)
    {
        text = text.Replace("\r\n", "\n");
        foreach (var helper in HelperNames)
            text = Regex.Replace(text, $@"^script (?:{helper}|{Hash(helper)})\b.*?^endscript\n?", "", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var names = $"(?:{ScriptName}|{Hash(ScriptName)})";
        text = Regex.Replace(text, $@"^script {names}\b.*?^endscript\n?", "", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, $@"^\t{names}[ \t]*\n", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return text;
    }

    static string AddCall(string text, string script)
    {
        var m = Regex.Match(text, $@"^script {script}\b.*?^endscript", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!m.Success) throw new InvalidOperationException($"No se encontró {script} en los scripts del juego.");
        int at = m.Index + m.Length - "endscript".Length;
        return text[..at] + "\t" + ScriptName + "\n" + text[at..];
    }

    static string Script(UnlockOptions o)
    {
        var s = new StringBuilder();
        s.Append("script ").Append(ScriptName).Append(" \n");
        if (o.Songs)
        {
            foreach (var list in new[] { "GH3_General_Songs", "GH3_GeneralP2_Songs", "GH3_Bonus_Songs" })
                s.Append("\tGlobalTags_UnlockAll \\{songlist = ").Append(list).Append("\n\t\tsongs_only = 1}\n");
            s.Append("\tGetArraySize ($GH3_Bonus_Songs.tier1.songs)\n\ti = 0\n\tbegin\n")
             .Append("\tSetGlobalTags ($GH3_Bonus_Songs.tier1.songs [<i>]) params = {unlocked = 1}\n")
             .Append("\t<i> = (<i> + 1)\n\trepeat <array_size>\n");
        }
        if (o.Items)
        {
            foreach (var list in new[] { "Bonus_Guitars", "Bonus_Guitar_Finishes", "Secret_Guitars", "Bonus_Basses", "Bonus_Bass_Finishes", "Secret_Basses" })
                Loop(s, list, "unlocked = 1 unlocked_for_purchase = 1");
            foreach (var list in new[] { "Secret_Characters", "Bonus_Outfits", "Bonus_Styles", "Bonus_Videos" })
                Loop(s, list, "unlocked = 1");
        }
        if (o.Cheats)
        {
            s.Append("\tSetGlobalTags \\{user_options\n\t\tparams = {\n");
            foreach (var c in CheatTags) s.Append("\t\t\tunlock_Cheat_").Append(c).Append(" = 1\n");
            s.Append("\t\t}}\n");
        }
        foreach (var c in o.On)
        {
            s.Append("\tchange \\{").Append(c).Append(" = 1}\n");
            if (c == "Cheat_EasyExpert")
                s.Append("\tchange check_time_early = ($original_check_time_early * 2)\n\tchange check_time_late = ($original_check_time_late * 2)\n");
            if (c == "Cheat_PrecisionMode")
                s.Append("\tchange check_time_early = ($original_check_time_early / 2)\n\tchange check_time_late = ($original_check_time_late / 2)\n");
        }
        if (o.Hyperspeed > 0) s.Append("\tchange \\{Cheat_Hyperspeed = ").Append(o.Hyperspeed).Append("}\n");
        if (o.Complete) s.Append("\tgh3launcher_complete\n");
        if (o.Cash > 0)
        {
            s.Append("\tband_count = 1\n\tbegin\n");
            foreach (var mode in new[] { "p1_career", "p2_career" })
                s.Append("\tFormatText checksumname = bandname 'band%i_info_").Append(mode).Append("' i = <band_count>\n")
                 .Append("\tGetGlobalTags <bandname> noassert = 1\n")
                 .Append("\tif (<Cash> < ").Append(o.Cash).Append(")\n")
                 .Append("\t\tSetGlobalTags <bandname> params = {Cash = ").Append(o.Cash).Append("}\n")
                 .Append("\tendif\n");
            s.Append("\t<band_count> = (<band_count> + 1)\n\trepeat ($num_career_bands + 1)\n");
        }
        s.Append("endscript\n");
        return s.ToString();
    }

    /* The "Complete" band: once per save (it stops as soon as a band of that name exists), the first
       free career band slot (the last slot if all are taken) becomes "Complete", and for it, on each
       difficulty and in both career modes (solo and co-op): every tier unlocked and complete with its
       encore and boss battle, every song passed (3 stars / 1000 points, the same as BetterGH3's own
       unlock-all; better results are kept), the career's bonus songs, and every venue. Then the
       game-wide flags a finished career sets: "career beaten" per difficulty and the bonus songs
       bought. It follows the game's own playday_unlockall: progress lives in per-band, per-difficulty
       sections that progression_pop_current/push_current swap in and out. The boss intros play
       whenever a boss battle is played in its venue, so they are not touched. */
    const string CompleteScripts = """
script gh3launcher_complete
	band_index = 1
	begin
	FormatText checksumname = bandname_id 'band%i_info_%g' i = <band_index> g = 'p1_career'
	GetGlobalTags <bandname_id> param = name
	if (<name> = "Complete")
		return
	endif
	<band_index> = (<band_index> + 1)
	repeat ($num_career_bands)
	slot = ($num_career_bands)
	band_index = ($num_career_bands)
	begin
	FormatText checksumname = bandname_id 'band%i_info_%g' i = <band_index> g = 'p1_career'
	GetGlobalTags <bandname_id> param = name
	if (<name> = "")
		slot = <band_index>
	endif
	<band_index> = (<band_index> - 1)
	repeat ($num_career_bands)
	FormatText checksumname = bandname_id 'band%i_info_%g' i = <slot> g = 'p1_career'
	FormatText checksumname = band_unique_id 'band%i_info_%g_gh3launcher' i = <slot> g = 'p1_career'
	SetGlobalTags <bandname_id> params = {name = "Complete" band_unique_id = <band_unique_id>}
	stored_difficulty1 = ($current_difficulty)
	stored_difficulty2 = ($current_difficulty2)
	stored_band = ($current_band)
	stored_gamemode = ($game_mode)
	popped = 0
	if ($progression_pop_count = 1)
		popped = 1
		progression_push_current
	endif
	change current_band = <slot>
	GetArraySize \{$LevelZoneArray}
	zones = <array_size>
	difficulties = [easy medium hard expert]
	diff_index = 0
	begin
	change current_difficulty = (<difficulties> [<diff_index>])
	change current_difficulty2 = (<difficulties> [<diff_index>])
	get_difficulty_text_nl difficulty = (<difficulties> [<diff_index>])
	change \{game_mode = p1_career}
	progression_pop_current
	get_progression_globals \{game_mode = p1_career}
	gh3launcher_complete_list songlist = <tier_global> difficulty_text_nl = <difficulty_text_nl>
	get_progression_globals game_mode = p1_career Bonus
	gh3launcher_complete_list songlist = <tier_global> unlock_only = 1
	progression_push_current
	change \{game_mode = p2_career}
	progression_pop_current
	get_progression_globals \{game_mode = p2_career}
	gh3launcher_complete_list songlist = <tier_global> difficulty_text_nl = <difficulty_text_nl>
	get_progression_globals game_mode = p2_career Bonus
	gh3launcher_complete_list songlist = <tier_global> unlock_only = 1
	progression_push_current
	index = 0
	begin
	get_LevelZoneArray_checksum index = <index>
	FormatText checksumname = venue_band 'p%p_career_band%i_%d%s' p = 1 i = <slot> d = <difficulty_text_nl> s = ($LevelZones.<level_checksum>.name) AddToStringLookup = true
	SetGlobalTags <venue_band> params = {unlocked = 1}
	FormatText checksumname = venue_band 'p%p_career_band%i_%d%s' p = 2 i = <slot> d = <difficulty_text_nl> s = ($LevelZones.<level_checksum>.name) AddToStringLookup = true
	SetGlobalTags <venue_band> params = {unlocked = 1}
	FormatText checksumname = venue_checksum 'venue_%s' s = ($LevelZones.<level_checksum>.name)
	SetGlobalTags <venue_checksum> params = {unlocked = 1}
	<index> = (<index> + 1)
	repeat (<zones> - 1)
	<diff_index> = (<diff_index> + 1)
	repeat 4
	GetArraySize ($GH3_Bonus_Songs.tier1.songs)
	i = 0
	begin
	SetGlobalTags ($GH3_Bonus_Songs.tier1.songs [<i>]) params = {unlocked = 1}
	<i> = (<i> + 1)
	repeat <array_size>
	change \{progression_play_completion_movie = 0}
	change \{progression_unlock_tier_last_song = 0}
	change current_difficulty = <stored_difficulty1>
	change current_difficulty2 = <stored_difficulty2>
	change current_band = <stored_band>
	change game_mode = <stored_gamemode>
	if (<popped> = 1)
		progression_pop_current
	endif
endscript

script gh3launcher_complete_list \{unlock_only = 0}
	setlist_prefix = ($<songlist>.prefix)
	if (<unlock_only> = 0)
		FormatText checksumname = gametype_checksum '%p_%s' p = <setlist_prefix> s = <difficulty_text_nl>
		SetGlobalTags <gametype_checksum> params = {complete = 1}
	endif
	tier = 0
	begin
	FormatText checksumname = tiername '%ptier%i' p = <setlist_prefix> i = (<tier> + 1)
	FormatText checksumname = tier_checksum 'tier%s' s = (<tier> + 1)
	if (<unlock_only> = 0)
		SetGlobalTags <tiername> params = {unlocked = 1 complete = 1 encore_unlocked = 1 boss_unlocked = 1 num_songs_to_progress = 0}
	else
		SetGlobalTags <tiername> params = {unlocked = 1}
	endif
	GetArraySize ($<songlist>.<tier_checksum>.songs)
	if (<array_size> > 0)
		song_count = 0
		begin
		FormatText checksumname = song_checksum '%p_song%i_tier%s' p = <setlist_prefix> i = (<song_count> + 1) s = (<tier> + 1) AddToStringLookup = true
		if (<unlock_only> = 0)
			GetGlobalTags <song_checksum> params = {stars score}
			if (<stars> < 3)
				SetGlobalTags <song_checksum> params = {stars = 3 score = 1000 unlocked = 1}
			else
				SetGlobalTags <song_checksum> params = {unlocked = 1}
			endif
		else
			SetGlobalTags <song_checksum> params = {unlocked = 1}
		endif
		<song_count> = (<song_count> + 1)
		repeat <array_size>
	endif
	<tier> = (<tier> + 1)
	repeat ($<songlist>.num_tiers)
endscript
""";

    static void Loop(StringBuilder s, string list, string param)
    {
        s.Append("\tarray_count = 0\n\tGetArraySize \\{$").Append(list).Append("}\n\tbegin\n")
         .Append("\tSetGlobalTags ($").Append(list).Append(" [<array_count>].id) params = {").Append(param).Append("}\n")
         .Append("\tarray_count = (<array_count> + 1)\n\trepeat <array_size>\n");
    }
}
