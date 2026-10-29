using System.Diagnostics;

namespace GH3Setup;

/* The installer's window: welcome, the game folder, what to install, installing, done. */
sealed class SetupForm : Form
{
    static readonly Color Bg = Color.FromArgb(20, 13, 12), Panel2 = Color.FromArgb(30, 18, 16), Text1 = Color.FromArgb(239, 227, 200),
                          Text2 = Color.FromArgb(190, 176, 150), Accent = Color.FromArgb(232, 65, 26), Good = Color.FromArgb(150, 210, 110), Warn = Color.FromArgb(241, 207, 130);

    readonly Options o;
    readonly Payload payload;
    readonly Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(30, 22, 30, 10) };
    readonly List<Panel> pages = new();
    int page;
    bool installing, installed;

    // header / footer
    readonly Label title = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 22f), ForeColor = Text1, Location = new Point(100, 16) };
    readonly Label subtitle = new() { AutoSize = true, Font = new Font("Segoe UI", 10f), ForeColor = Text2, Location = new Point(103, 56) };
    readonly LinkLabel langEn = new() { AutoSize = true, Text = "English" }, langEs = new() { AutoSize = true, Text = "Español" };
    readonly Button back = new(), next = new(), cancel = new();

    // pages
    readonly Label welcomeHead = Head(), welcomeText = Body(), folderHead = Head(), folderHelp = Body(), folderStatus = Body(), compHead = Head(), compNote = Body(), progHead = Head(), progText = Body(), doneHead = Head(), doneText = Body();
    readonly LinkLabel notices = new() { AutoSize = true };
    readonly TextBox folder = new() { Font = new Font("Segoe UI", 10.5f), BackColor = Color.FromArgb(12, 8, 7), ForeColor = Text1, BorderStyle = BorderStyle.FixedSingle };
    readonly Button browse = new();
    readonly ListBox found = new() { BackColor = Color.FromArgb(12, 8, 7), ForeColor = Text1, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f), IntegralHeight = false };
    readonly CheckBox cLauncher = Check(), cHoneycomb = Check(), cX360 = Check(), cDesktop = Check(), cStart = Check(), cOpen = Check();
    readonly ProgressBar bar = new() { Style = ProgressBarStyle.Continuous, Maximum = 1000, Height = 22 };
    readonly LinkLabel webview = new() { AutoSize = true, Visible = false };

    public SetupForm(Options o, Payload payload)
    {
        this.o = o; this.payload = payload;
        Text = "GH3 Launcher Setup";
        ClientSize = new Size(780, 520);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg; ForeColor = Text1;
        Font = new Font("Segoe UI", 10f);
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }

        // header: icon, name, the language
        var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Panel2 };
        var pic = new PictureBox { Location = new Point(24, 14), Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom };
        try { pic.Image = new Icon(Icon, 64, 64).ToBitmap(); } catch { }
        foreach (var l in new[] { langEn, langEs })
        {
            l.LinkColor = Text2; l.ActiveLinkColor = Accent; l.VisitedLinkColor = Text2; l.LinkBehavior = LinkBehavior.HoverUnderline; l.Font = new Font("Segoe UI", 9.5f);
        }
        langEn.Location = new Point(640, 14); langEs.Location = new Point(700, 14);
        langEn.LinkClicked += (s, e) => { T.Lang = "en"; ApplyTexts(); };
        langEs.LinkClicked += (s, e) => { T.Lang = "es"; ApplyTexts(); };
        header.Controls.AddRange(new Control[] { pic, title, subtitle, langEn, langEs });

        // footer: back / next / cancel
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Panel2 };
        foreach (var b in new[] { back, next, cancel }) Style(b);
        next.BackColor = Accent; next.FlatAppearance.BorderColor = Accent; next.ForeColor = Color.White;
        cancel.Location = new Point(660, 14); next.Location = new Point(540, 14); back.Location = new Point(420, 14);
        footer.Controls.AddRange(new Control[] { back, next, cancel });
        back.Click += (s, e) => Show(page - 1);
        next.Click += (s, e) => Next();
        cancel.Click += (s, e) => Close();

        BuildPages();
        Controls.Add(content);
        Controls.Add(footer);
        Controls.Add(header);

        // a folder to start with
        var list = Detect.Candidates();
        found.Items.AddRange(list.ToArray());
        folder.Text = o.Target ?? list.FirstOrDefault() ?? "";
        if (!Detect.HasWebView2()) webview.Visible = true;
        ApplyTexts();
        FormClosing += (s, e) =>
        {
            if (installing) { e.Cancel = true; return; }
            if (installed && cOpen.Checked) Open();
        };
        Shown += (s, e) =>
        {
            if (o.Elevated) { Show(3); Install(); }   // Windows started this copy as administrator: straight on
            else Show(0);
        };
    }

    static Label Head() => new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 15f), ForeColor = Text1 };
    static Label Body() => new() { AutoSize = false, Font = new Font("Segoe UI", 10.5f), ForeColor = Text2 };
    static CheckBox Check() => new() { AutoSize = true, Font = new Font("Segoe UI", 10.5f), ForeColor = Text1, Checked = true };
    static void Style(Button b)
    {
        b.FlatStyle = FlatStyle.Flat; b.Size = new Size(108, 34); b.BackColor = Color.FromArgb(44, 28, 24); b.ForeColor = Text1;
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 60, 52); b.Font = new Font("Segoe UI Semibold", 10f); b.Cursor = Cursors.Hand;
    }

    Panel NewPage(params Control[] controls)
    {
        var p = new Panel { Dock = DockStyle.Fill, Visible = false };
        p.Controls.AddRange(controls);
        pages.Add(p);
        content.Controls.Add(p);
        return p;
    }

    void BuildPages()
    {
        // 0 welcome
        welcomeHead.Location = new Point(0, 0);
        welcomeText.Location = new Point(2, 40); welcomeText.Size = new Size(716, 250);
        notices.Location = new Point(2, 300); notices.LinkColor = Warn; notices.ActiveLinkColor = Accent; notices.VisitedLinkColor = Warn;
        notices.LinkClicked += (s, e) => ShowNotices();
        NewPage(welcomeHead, welcomeText, notices);

        // 1 folder
        folderHead.Location = new Point(0, 0);
        folderHelp.Location = new Point(2, 38); folderHelp.Size = new Size(716, 44);
        folder.Location = new Point(2, 86); folder.Size = new Size(590, 28);
        Style(browse); browse.Location = new Point(604, 83);
        browse.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { Description = T.S("The Guitar Hero III folder (the one with GH3.exe)", "La carpeta de Guitar Hero III (la que tiene GH3.exe)"), UseDescriptionForTitle = true, ShowNewFolderButton = false };
            if (Directory.Exists(folder.Text)) dlg.InitialDirectory = folder.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK) folder.Text = dlg.SelectedPath;
        };
        found.Location = new Point(2, 128); found.Size = new Size(716, 92);
        found.SelectedIndexChanged += (s, e) => { if (found.SelectedItem is string d) folder.Text = d; };
        folderStatus.Location = new Point(2, 230); folderStatus.Size = new Size(716, 110);
        folder.TextChanged += (s, e) => FolderStatus();
        NewPage(folderHead, folderHelp, folder, browse, found, folderStatus);

        // 2 components
        compHead.Location = new Point(0, 0);
        int y = 44;
        foreach (var c in new[] { cLauncher, cHoneycomb, cX360, cDesktop, cStart }) { c.Location = new Point(4, y); y += 36; }
        cLauncher.Enabled = false;
        compNote.Location = new Point(2, y + 6); compNote.Size = new Size(716, 80);
        webview.Location = new Point(2, y + 90); webview.LinkColor = Warn; webview.ActiveLinkColor = Accent; webview.VisitedLinkColor = Warn;
        webview.LinkClicked += (s, e) => Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
        foreach (var c in new[] { cHoneycomb, cX360 }) c.CheckedChanged += (s, e) => CompNote();
        NewPage(compHead, cLauncher, cHoneycomb, cX360, cDesktop, cStart, compNote, webview);

        // 3 installing
        progHead.Location = new Point(0, 0);
        bar.Location = new Point(2, 60); bar.Size = new Size(716, 22);
        progText.Location = new Point(2, 96); progText.Size = new Size(716, 60);
        NewPage(progHead, bar, progText);

        // 4 done
        doneHead.Location = new Point(0, 0);
        doneText.Location = new Point(2, 40); doneText.Size = new Size(716, 200);
        cOpen.Location = new Point(4, 250);
        NewPage(doneHead, doneText, cOpen);
    }

    void ApplyTexts()
    {
        langEn.Font = new Font("Segoe UI", 9.5f, T.Lang == "en" ? FontStyle.Bold : FontStyle.Regular);
        langEs.Font = new Font("Segoe UI", 9.5f, T.Lang == "es" ? FontStyle.Bold : FontStyle.Regular);
        title.Text = "GH3 Launcher";
        subtitle.Text = T.S("Setup", "Instalación") + " · " + Installer.Version;
        back.Text = T.S("Back", "Atrás");
        cancel.Text = T.S("Cancel", "Cancelar");
        browse.Text = T.S("Browse…", "Buscar…");

        welcomeHead.Text = T.S("Welcome", "Bienvenido");
        welcomeText.Text = T.S(
            "This installs GH3 Launcher in your Guitar Hero III (PC) folder: a launcher and song manager to import Clone Hero songs, organize the Download list in divisions, set up guitars, controllers and the keyboard, unlock things, and more.\n\n" +
            "It brings: GH3 Launcher · Honeycomb (GH Toolkit), which compiles the songs · FFmpeg (ffmpeg, ffprobe, ffplay) · x360ce, for guitars and controllers.\n\n" +
            "It is free software: use it, share it and change it as you like, both the code and the program. It was made to make things simpler and was not polished in depth, so it may contain errors. The launcher backs up the game's song list and your save before every change.",
            "Esto instala GH3 Launcher en tu carpeta de Guitar Hero III (PC): un launcher y gestor de canciones para importar canciones de Clone Hero, ordenar la lista Download en divisiones, configurar guitarras, controles y el teclado, desbloquear cosas y más.\n\n" +
            "Trae: GH3 Launcher · Honeycomb (GH Toolkit), que compila las canciones · FFmpeg (ffmpeg, ffprobe, ffplay) · x360ce, para guitarras y controles.\n\n" +
            "Es software libre: úsalo, compártelo y cámbialo como quieras, tanto el código como el programa. Se hizo para hacer las cosas más simples y no se pulió a fondo, así que puede contener errores. El launcher respalda la lista de canciones del juego y tu partida antes de cada cambio.");
        notices.Text = T.S("Licenses and third-party notices", "Licencias y avisos de terceros");

        folderHead.Text = T.S("Where is Guitar Hero III?", "¿Dónde está Guitar Hero III?");
        folderHelp.Text = T.S("Pick the game's folder, the one with GH3.exe. These were found on this PC:", "Elige la carpeta del juego, la que tiene GH3.exe. Se encontraron estas en esta PC:");

        compHead.Text = T.S("What to install", "Qué instalar");
        cLauncher.Text = T.S("GH3 Launcher (required)", "GH3 Launcher (necesario)");
        cHoneycomb.Text = T.S("Honeycomb (GH Toolkit) + FFmpeg: to import songs", "Honeycomb (GH Toolkit) + FFmpeg: para importar canciones");
        cX360.Text = T.S("x360ce: guitars and controllers (your x360ce.ini is kept)", "x360ce: guitarras y controles (se conserva tu x360ce.ini)");
        cDesktop.Text = T.S("Desktop shortcut", "Acceso directo en el escritorio");
        cStart.Text = T.S("Start menu shortcut", "Acceso directo en el menú Inicio");
        webview.Text = T.S("The Microsoft Edge WebView2 Runtime is not installed: the launcher needs it. Get it here.", "No está instalado Microsoft Edge WebView2 Runtime: el launcher lo necesita. Descárgalo aquí.");

        progHead.Text = T.S("Installing…", "Instalando…");
        cOpen.Text = T.S("Open GH3 Launcher now", "Abrir GH3 Launcher ahora");
        FolderStatus();
        CompNote();
        UpdateButtons();
    }

    void FolderStatus()
    {
        var d = folder.Text.Trim();
        if (!Detect.IsGame(d))
        {
            folderStatus.ForeColor = Warn;
            folderStatus.Text = T.S("✗  GH3.exe is not in this folder.", "✗  GH3.exe no está en esta carpeta.");
        }
        else
        {
            var lines = new List<string> { T.S("✓  Guitar Hero III found.", "✓  Se encontró Guitar Hero III.") };
            if (!Detect.Writable(d)) lines.Add(T.S("•  This folder needs administrator rights: Windows will ask for permission.", "•  Esta carpeta necesita permisos de administrador: Windows te pedirá permiso."));
            if (!File.Exists(Path.Combine(d, "dinput8.dll")) || !File.Exists(Path.Combine(d, "PLUGINS", "core.dll")))
                lines.Add(T.S("•  GH3+ (plugin loader) is not here: the launcher works, but its InputPlus plugin (auto-strum, keyboard 2, whammy) needs GH3+ (BetterGH3 includes it).",
                              "•  No está GH3+ (cargador de plugins): el launcher funciona, pero su plugin InputPlus (rasgueo automático, Teclado 2, whammy) necesita GH3+ (BetterGH3 lo incluye)."));
            if (File.Exists(Path.Combine(d, "GH3 Launcher.exe"))) lines.Add(T.S("•  GH3 Launcher is already here: it will be updated, your settings stay.", "•  GH3 Launcher ya está aquí: se actualizará, tus ajustes se quedan."));
            folderStatus.ForeColor = Good;
            folderStatus.Text = string.Join("\n", lines);
        }
        UpdateButtons();
    }

    void CompNote()
    {
        o.Components = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "launcher" };
        if (cHoneycomb.Checked) o.Components.Add("honeycomb");
        if (cX360.Checked) o.Components.Add("x360ce");
        if (cDesktop.Checked) o.Components.Add("desktop");
        if (cStart.Checked) o.Components.Add("startmenu");
        var mb = new Installer(payload, o, (p, w) => { }).Bytes() / 1048576.0;
        compNote.Text = T.S($"Space needed: {mb:0} MB.", $"Espacio necesario: {mb:0} MB.") +
            (cHoneycomb.Checked ? "" : "\n" + T.S("Without Honeycomb and FFmpeg the launcher cannot import songs (unless they are already in the folder).", "Sin Honeycomb y FFmpeg el launcher no puede importar canciones (salvo que ya estén en la carpeta)."));
    }

    void UpdateButtons()
    {
        back.Visible = page is 1 or 2;
        cancel.Visible = page < 3;
        next.Text = page switch { 2 => T.S("Install", "Instalar"), 4 => T.S("Finish", "Terminar"), _ => T.S("Next", "Siguiente") };
        next.Enabled = page switch { 1 => Detect.IsGame(folder.Text.Trim()), 3 => false, _ => true };
        next.Visible = page != 3;
    }

    void Show(int i)
    {
        page = Math.Clamp(i, 0, pages.Count - 1);
        for (int k = 0; k < pages.Count; k++) pages[k].Visible = k == page;
        UpdateButtons();
    }

    void Next()
    {
        switch (page)
        {
            case 0: Show(1); break;
            case 1: o.Target = folder.Text.Trim(); CompNote(); Show(2); break;
            case 2:
                o.Target = folder.Text.Trim();
                CompNote();
                if (!Detect.Writable(o.Target) && !Detect.IsAdmin())
                {
                    if (Program.Elevate(o)) { Close(); return; }
                    MessageBox.Show(this, T.S("Without administrator rights the installer cannot write in that folder.", "Sin permisos de administrador el instalador no puede escribir en esa carpeta."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Show(3);
                Install();
                break;
            case 4: Close(); break;
        }
    }

    void Install()
    {
        installing = true;
        UpdateButtons();
        Task.Run(() =>
        {
            try
            {
                new Installer(payload, o, (p, what) => BeginInvoke(() =>
                {
                    bar.Value = (int)Math.Clamp(p * 1000, 0, 1000);
                    progText.Text = what;
                })).Run();
                BeginInvoke(() => Done(null));
            }
            catch (Exception ex) { BeginInvoke(() => Done(ex)); }
        });
    }

    void Done(Exception error)
    {
        installing = false;
        installed = error == null;
        if (error == null)
        {
            doneHead.Text = T.S("GH3 Launcher is installed", "GH3 Launcher está instalado");
            doneText.ForeColor = Text2;
            doneText.Text = T.S(
                $"It is in {o.Target}.\n\nThe first time it opens, it reads the game's art and sounds from your copy of Guitar Hero III (a few seconds). To uninstall it, use Windows' Apps & features or Tools › Uninstall in the launcher.",
                $"Está en {o.Target}.\n\nLa primera vez que se abre, lee el arte y los sonidos del juego de tu copia de Guitar Hero III (unos segundos). Para desinstalarlo usa Aplicaciones instaladas de Windows o Herramientas › Desinstalar en el launcher.")
                + (Detect.HasWebView2() ? "" : "\n\n" + T.S("Install the Microsoft Edge WebView2 Runtime before opening it.", "Instala Microsoft Edge WebView2 Runtime antes de abrirlo."));
            cOpen.Visible = true;
        }
        else
        {
            doneHead.Text = T.S("The installation did not finish", "La instalación no terminó");
            doneText.ForeColor = Warn;
            doneText.Text = error.Message;
            cOpen.Visible = false;
            cOpen.Checked = false;
        }
        Show(4);
    }

    void Open()
    {
        try { Process.Start(new ProcessStartInfo(Path.Combine(o.Target, "GH3 Launcher.exe")) { UseShellExecute = true, WorkingDirectory = o.Target }); } catch { }
    }

    void ShowNotices()
    {
        var text = payload.Text("Licenses/THIRD-PARTY-NOTICES.txt") ?? "";
        var lic = payload.Text("Licenses/LICENSE.txt");
        if (lic != null) text = "GH3 Launcher\r\n============\r\n" + lic + "\r\n\r\n" + text;
        using var f = new Form { Text = notices.Text, ClientSize = new Size(720, 520), StartPosition = FormStartPosition.CenterParent, BackColor = Bg, MinimizeBox = false, MaximizeBox = false, ShowIcon = false };
        var box = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n"), BackColor = Color.FromArgb(12, 8, 7), ForeColor = Text1, Font = new Font("Consolas", 9.5f), BorderStyle = BorderStyle.None };
        f.Controls.Add(box);
        box.Select(0, 0);
        f.ShowDialog(this);
    }
}
