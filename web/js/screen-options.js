/* Opciones: GH3's own settings (AspyrConfig.xml), GH3+'s settings.ini, the plugins, the launcher. */
App.register('options', (() => {
  const { h, icon } = App;
  let root, body, st = null;

  function mount(r) {
    root = r;
    body = h('div');
    root.append(h('div.room.room-options'), h('div.scroll', h('div.page-wrap',
      h('div.head', h('h1', 'Opciones'), h('div.sub', 'Video, audio, GH3+ y plugins · se guardan solas'), h('div.tools', h('span.saved-flash', 'Guardado ✓'))),
      body)));
  }

  async function show() {
    try { st = await Bridge.call('settings.state'); gfx = await Bridge.call('graphics.state'); render(); } catch (e) { App.fail(e); }
  }

  /* ---- Gráficos mejorados: ReShade's effects (GraphicsService.cs) + GH3's quality settings ---- */
  let gfx = null, gfxBusy = false;
  const PRESETS = [
    ['remaster', 'Remasterizado', 'Luz ambiental, color, contraste, nitidez y un toque de desenfoque de movimiento, como el preset "GH3 Remastered".'],
    ['sharp', 'Nítido', 'Más definición y algo de color, sin tocar la iluminación.'],
    ['cinema', 'Cinematográfico', 'Bloom, luz ambiental y movimiento suave: más espectáculo.'],
    ['vivid', 'Vivo', 'Colores intensos y brillo.']
  ];
  const FX = {
    light: ['Iluminación ambiental', 'Luz suave alrededor de las zonas brillantes.'],
    bloom: ['Bloom', 'Resplandor en luces y destellos.'],
    color: ['Color', 'Saturación inteligente: aviva lo apagado sin quemar lo intenso.'],
    levels: ['Contraste', 'Negros más profundos y blancos limpios.'],
    sharpen: ['Nitidez', 'Bordes y texturas más definidos.'],
    motion: ['Desenfoque de movimiento', 'Movimientos más suaves.']
  };
  async function gfxDo(fn) {
    gfxBusy = true; render();
    let ok = true;
    try { const r = await fn(); if (r) gfx = r.state || r; } catch (e) { ok = false; App.fail(e); }
    gfxBusy = false; render();
    return ok;
  }
  const gfxEnable = async on => { if (await gfxDo(() => Bridge.call(on ? 'graphics.enable' : 'graphics.disable'))) App.toast(on ? 'Gráficos mejorados activados' : 'Gráficos mejorados desactivados', 'Se ven la próxima vez que abras el juego.'); };
  async function gfxPreset(id) {
    await gfxDo(async () => {
      const r = await Bridge.call('graphics.preset', { id });
      if (r.game) await aspyr(r.game);   // its GH3 quality settings too
      return r;
    });
  }
  const gfxEffect = App.debounce((id, change) => gfxDo(() => Bridge.call('graphics.effect', Object.assign({ id }, change))), 300);

  function graphicsCard() {
    if (!gfx) return null;
    const running = gfx.gameRunning;
    return h('div.card', h('h2', 'Gráficos mejorados'),
      h('p.muted', { style: { margin: '0 0 6px', fontSize: '14.5px' } }, 'Efectos de ReShade sobre el juego, más los ajustes de calidad de GH3. Los activas y desactivas aquí cuando quieras; se ven al abrir el juego (dentro del juego, la tecla Inicio abre el menú de ReShade).'),
      field('Activar', gfx.on ? `ReShade ${gfx.version} puesto en el juego.` : 'La primera vez se descarga ReShade (reshade.me) y sus efectos oficiales.',
        toggle(gfx.on, v => gfxEnable(v), gfxBusy || running || gfx.otherDll)),
      gfx.otherDll ? h('p', { style: { color: '#f1cf82', margin: '4px 0' } }, 'Ya hay otro d3d9.dll junto a GH3.exe (otro mod): quítalo para usar esto.') : null,
      running ? h('p', { style: { color: '#f1cf82', margin: '4px 0' } }, 'Cierra el juego para activar o desactivar.') : null,
      gfxBusy ? h('p.faint', { style: { margin: '4px 0' } }, h('span.spinner'), ' Un momento…') : null,
      h('div.preset-grid', PRESETS.map(([id, title, note]) => h('button.preset' + (gfx.preset === id ? '.on' : ''), { disabled: gfxBusy, onclick: () => gfxPreset(id) }, h('b', title), h('small', note)))),
      gfx.preset === 'custom' ? h('p.faint', { style: { margin: '6px 0 0' } }, 'Personalizado: tus ajustes de abajo.') : null,
      h('details.more', { open: gfx.preset === 'custom' }, h('summary', 'Ajustar cada efecto'),
        gfx.effects.map(e => {
          const [label, hint] = FX[e.id] || [e.id, ''];
          const out = h('output', e.value.toFixed(2));
          return h('div.field', h('div.label', label), h('div.hint', hint),
            h('div.ctl', e.slider ? h('div.ctl.range', h('input', { type: 'range', min: e.min, max: e.max, step: (e.max - e.min) / 100, value: e.value, disabled: !e.on,
              oninput: ev => { out.textContent = (+ev.target.value).toFixed(2); gfxEffect(e.id, { value: +ev.target.value }); } }), out) : null,
              toggle(e.on, v => gfxEffect(e.id, { on: v }))));
        })));
  }

  function flash() { const f = root.querySelector('.saved-flash'); f.classList.add('on'); clearTimeout(f._t); f._t = setTimeout(() => f.classList.remove('on'), 1400); }
  async function aspyr(values) { try { st = await Bridge.call('settings.aspyr', { values }); flash(); } catch (e) { App.fail(e); render(); } }
  async function plus(values) { try { st = await Bridge.call('settings.plus', { values }); flash(); } catch (e) { App.fail(e); render(); } }
  async function launcher(settings) {
    try { App.state.app.launcher = await Bridge.call('launcher.save', { settings }); flash(); BgMusic.apply(); }
    catch (e) { App.fail(e); }
  }

  const toggle = (on, fn, disabled) => h('label.toggle', h('input', { type: 'checkbox', checked: on, disabled, onchange: e => fn(e.target.checked) }), h('i'));
  const field = (label, hint, ctl) => h('div.field', h('div.label', label), h('div.hint', hint || ''), h('div.ctl', ctl));
  const select = (value, opts, fn) => h('select', { onchange: e => fn(e.target.value) }, opts.map(([v, l]) => h('option', { value: v, selected: String(v) === String(value) }, l)));

  function render() {
    if (!st) return;
    const a = st.aspyr;
    const res = `${a['Video.Width'] || st.display.current[0]}x${a['Video.Height'] || st.display.current[1]}`;
    const modes = st.display.modes.map(([w, hh]) => [`${w}x${hh}`, `${w} × ${hh}` + (w === st.display.current[0] && hh === st.display.current[1] ? '  (tu monitor)' : '')]);
    if (!modes.some(m => m[0] === res)) modes.unshift([res, res.replace('x', ' × ')]);
    const bool = k => a[k] === '1';
    const tri = (v, fn) => select(v == null ? '' : v, [['', 'Predeterminado'], ['1', 'Sí'], ['0', 'No']], fn);
    const num = (v, fn, ph) => h('input', { type: 'number', value: v == null ? '' : v, placeholder: ph || 'Predet.', min: 0, onchange: e => fn(e.target.value.trim()) });
    const p = st.plus;
    const L = (App.state.app && App.state.app.launcher) || {};

    const video = h('div.card', h('h2', 'Video'),
      field('Resolución', 'La del juego. Con "Ventana sin bordes" lo ideal es la de tu monitor.',
        select(res, modes, v => { const [w, hh] = v.split('x'); aspyr({ 'Video.Width': w, 'Video.Height': hh }); })),
      field('Antialiasing', 'Suaviza los bordes (MSAA). Más alto = más carga para la GPU.',
        select(a['Video.MultiSampleType'] || '0', [['0', 'Desactivado'], ['2', '2×'], ['4', '4×'], ['8', '8×']], v => aspyr({ 'Video.MultiSampleType': v, 'Video.MultiSampleQuality': '0' }))),
      field('Calidad gráfica', 'El mismo ajuste que Opciones › Gráficos dentro del juego.',
        select(a['Options.GraphicsQuality'] || '0', [['0', 'Nivel 0'], ['1', 'Nivel 1'], ['2', 'Nivel 2']], v => aspyr({ 'Options.GraphicsQuality': v }))),
      field('Público', 'El público animado en los escenarios.', toggle(bool('Options.Crowd'), v => aspyr({ 'Options.Crowd': v ? '1' : '0' }))),
      field('Física', 'Pelo, ropa y objetos con física.', toggle(bool('Options.Physics'), v => aspyr({ 'Options.Physics': v ? '1' : '0' }))),
      field('Destellos', 'Luces y destellos de los escenarios.', toggle(bool('Options.Flares'), v => aspyr({ 'Options.Flares': v ? '1' : '0' }))),
      field('Cámara de primera fila', 'Cámara fija frente al escenario.', toggle(bool('Options.FrontRowCamera'), v => aspyr({ 'Options.FrontRowCamera': v ? '1' : '0' }))),
      h('details.more', h('summary', 'Avanzado'),
        field('Pantalla completa exclusiva (Aspyr)', 'Ajuste interno del port de Aspyr; con BorderlessWindow normalmente no hace falta.',
          toggle(a['Debugging.FullScreen'] === '1', v => aspyr({ 'Debugging.FullScreen': v ? '1' : null })))),
      !st.aspyrExists && h('p.faint', 'GH3 todavía no creó su configuración: se creará con estos valores.'));

    const audio = h('div.card', h('h2', 'Audio'),
      field('Desfase de la canción (Song Skew)', 'Ajuste fino de sincronía audio/notas en ms. Para calibrar usa también Opciones › Calibrar en el juego.',
        num(a['Sound.SongSkew'] || '0', v => aspyr({ 'Sound.SongSkew': v || '0' }), '0')),
      h('details.more', h('summary', 'Búferes (solo si el audio se corta)'),
        field('DSP: largo del búfer', 'Sound.DspBufferLength', num(a['Sound.DspBufferLength'], v => aspyr({ 'Sound.DspBufferLength': v || null }))),
        field('DSP: número de búferes', 'Sound.DspNumBuffers', num(a['Sound.DspNumBuffers'], v => aspyr({ 'Sound.DspNumBuffers': v || null }))),
        field('Streaming: búfer', 'Sound.StreamBufferSize', num(a['Sound.StreamBufferSize'], v => aspyr({ 'Sound.StreamBufferSize': v || null }))),
        field('Streaming: búfer de decodificación', 'Sound.StreamDecodeBufferSize', num(a['Sound.StreamDecodeBufferSize'], v => aspyr({ 'Sound.StreamDecodeBufferSize': v || null }))),
        field('Intervalo de sincronía', 'Sound.SongSyncInterval', num(a['Sound.SongSyncInterval'], v => aspyr({ 'Sound.SongSyncInterval': v || null })))));

    const ghplus = h('div.card', h('h2', 'GH3+ (settings.ini)'),
      h('p.muted', { style: { margin: '0 0 6px', fontSize: '14.5px' } }, 'Lo leen los plugins FastGH3 y NoteLimitFix al iniciar el juego. "Predeterminado" no escribe nada.'),
      field('Modo ventana', 'GFX.Windowed', tri(p['GFX.Windowed'], v => plus({ 'GFX.Windowed': v }))),
      field('Sin bordes', 'GFX.Borderless', tri(p['GFX.Borderless'], v => plus({ 'GFX.Borderless': v }))),
      field('VSync', 'GFX.VSync: sincroniza con el monitor (sin tearing, algo más de latencia).', tri(p['GFX.VSync'], v => plus({ 'GFX.VSync': v }))),
      field('FPS máximo', 'Player.MaxFPS', num(p['Player.MaxFPS'], v => plus({ 'Player.MaxFPS': v }))),
      field('Máximo de notas', 'Player.MaxNotes: para charts enormes.', num(p['Player.MaxNotes'], v => plus({ 'Player.MaxNotes': v }))));

    const running = App.state.app && App.state.app.game && App.state.app.game.running;
    const plugins = h('div.card', h('h2', 'Plugins GH3+'),
      running && h('p', { style: { color: '#f1cf82', margin: '0 0 8px' } }, 'Cierra el juego para activar o desactivar plugins.'),
      st.plugins.map(pl => h('div.plug', h('b', pl.title, h('small', pl.name)), h('span', pl.desc),
        toggle(pl.enabled, async v => { try { st = await Bridge.call('plugins.set', { name: pl.name, enabled: v }); flash(); render(); } catch (e) { App.fail(e); render(); } }, pl.locked || running))));

    const launch = h('div.card', h('h2', 'Launcher'),
      field('Idioma del launcher', 'Los menús, mensajes y textos de este launcher.',
        select(App.state.app && App.state.app.language || 'en', [['en', 'English'], ['es', 'Español']], v => launcher({ language: v }).then(() => location.reload()))),
      field('Al presionar Jugar', '',
        select(L.onPlay || 'minimize', [['minimize', 'Minimizar el launcher'], ['none', 'Dejarlo abierto'], ['close', 'Cerrar el launcher']], v => launcher({ onPlay: v }))),
      field('Prioridad alta para GH3', 'Le da preferencia al juego en la CPU.', toggle(!!L.highPriority, v => launcher({ highPriority: v }))),
      field('Abrir en pantalla completa', 'También con F11 en cualquier momento.', toggle(!!L.startFullscreen, v => launcher({ startFullscreen: v }))),
      field('Idioma del juego', App.state.app && App.state.app.tools && App.state.app.tools.fixes ? 'Se cambia con "Arreglar problemas de inicio" (Herramientas). Esta versión de BetterGH3 está en inglés.' : '', h('span.chip', (st.language || '—').toUpperCase())));

    const mm = L.menuMusic !== false;
    const mvol = typeof L.menuMusicVolume === 'number' ? L.menuMusicVolume : 0.35;
    const volLabel = h('span.chip', Math.round(mvol * 100) + '%');
    const music = h('div.card', h('h2', 'Música del menú'),
      h('p.muted', { style: { margin: '0 0 6px', fontSize: '14.5px' } }, 'Primero la música del menú del juego, en orden al azar hasta acabarla; después los previews de todas tus canciones, en otro orden al azar hasta acabarlas; y otra vuelta. Se calla sola mientras escuchas una canción, juegas o el launcher está minimizado.'),
      field('Música de fondo', '', toggle(mm, v => launcher({ menuMusic: v }).then(render))),
      field('Volumen', '', h('div', { style: { display: 'flex', alignItems: 'center', gap: '10px' } },
        h('input', { type: 'range', min: 0, max: 1, step: 0.01, value: mvol, disabled: !mm, style: { width: '150px', accentColor: 'var(--accent)' },
          oninput: e => { volLabel.textContent = Math.round(e.target.value * 100) + '%'; App.state.app.launcher = Object.assign({}, L, { menuMusicVolume: +e.target.value }); BgMusic.apply(); },
          onchange: e => launcher({ menuMusicVolume: +e.target.value }) }), volLabel)),
      field('Música del menú del juego', 'Los 6 temas que suenan en los menús de GH3 (Paint It Black, Bulls on Parade…).', toggle(L.menuMusicMenu !== false, v => launcher({ menuMusicMenu: v }), !mm)),
      field('Canciones del juego', 'Previews del Setlist, Bonus y los DLC oficiales.', toggle(L.menuMusicGame !== false, v => launcher({ menuMusicGame: v }), !mm)),
      field('Canciones importadas', 'Previews de las que instalaste desde Importar.', toggle(L.menuMusicImported !== false, v => launcher({ menuMusicImported: v }), !mm)));

    // GH3's menu sounds when picking, going back and moving around (sfx.js)
    const us = L.uiSounds !== false;
    const uvol = typeof L.uiSoundsVolume === 'number' ? L.uiSoundsVolume : 0.5;
    const uvolLabel = h('span.chip', Math.round(uvol * 100) + '%');
    const sounds = h('div.card', h('h2', 'Sonidos del menú'),
      field('Sonidos de la interfaz', 'Los sonidos de GH3 al elegir, volver y moverte por el launcher.', toggle(us, v => launcher({ uiSounds: v }).then(render))),
      field('Volumen', '', h('div', { style: { display: 'flex', alignItems: 'center', gap: '10px' } },
        h('input', { type: 'range', min: 0, max: 1, step: 0.01, value: uvol, disabled: !us, style: { width: '150px', accentColor: 'var(--accent)' },
          oninput: e => { uvolLabel.textContent = Math.round(e.target.value * 100) + '%'; App.state.app.launcher = Object.assign({}, App.state.app.launcher, { uiSoundsVolume: +e.target.value }); },
          onchange: e => { launcher({ uiSoundsVolume: +e.target.value }); Sfx.play('select'); } }), uvolLabel)));

    body.replaceChildren(h('div.opt-grid', h('div.opt-col', graphicsCard(), video, audio, music, sounds), h('div.opt-col', launch, plugins, ghplus)));
  }

  return { mount, show };
})());
