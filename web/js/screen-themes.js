/* Temas: how the notes, the highway and the HUD look in the game. The community's FastGH3 themes
   (downloaded here or imported from a file) go into the game's global zone texture by texture
   (ThemeService.cs + the song tool's theme-apply); "Guitar Hero III (original)" puts the game's own
   back. The preview plays the song that was on in the background (its full audio, from a busy
   part) and draws its expert notes on the highway with the chosen theme's textures, hitting every
   one, so you see the theme move before putting it on the game. */
App.register('themes', (() => {
  const { h, icon } = App;
  const SLOTS = ['highway', 'gem_g', 'gem_r', 'gem_y', 'gem_b', 'gem_o', 'btn_g', 'btn_r', 'btn_y', 'btn_b', 'btn_o', 'flame'];
  const COLORS = ['#2fbf4a', '#e5352b', '#f2d22e', '#2f7ff0', '#f28a1e'];

  let root, listEl, sideEl, st = null, sel = null;
  let canvas, cx, raf = 0, imgs = {}, notes = [], song = null, songLabel, busy = false;

  function mount(r) {
    root = r;
    listEl = h('div.theme-grid');
    canvas = h('canvas.theme-canvas', { width: 560, height: 460 });
    cx = canvas.getContext('2d');
    songLabel = h('div.theme-song');
    sideEl = h('div');
    root.append(h('div.room.room-options'), h('div.scroll', h('div.page-wrap',
      h('div.head', h('h1', 'Temas'), h('div.sub', 'Cómo se ven las notas, la autopista y el HUD en el juego')),
      h('div.theme-layout',
        h('div', h('div.section-title', 'Tus temas'), listEl, sideEl),
        h('div.card.theme-stage', canvas, songLabel, h('div.theme-acts'))))));
  }

  async function show() {
    await refresh();
    startPreview();
  }
  function hide() { stopPreview(); }

  async function refresh() {
    try { st = await Bridge.call('theme.state'); } catch (e) { App.fail(e); return; }
    if (sel == null || !st.themes.some(t => t.id === sel)) sel = st.current || '';
    render();
    await loadImages();
  }

  const theme = () => st.themes.find(t => t.id === sel) || st.themes[0];

  function render() {
    listEl.replaceChildren(...st.themes.map(t => h('button.theme-card' + (t.id === sel ? '.on' : ''), { onclick: () => { sel = t.id; render(); loadImages(); } },
      h('div.theme-thumb', t.preview ? [h('img', { src: t.preview + 'highway.png', alt: '' }), h('div.gems', ['gem_g', 'gem_r', 'gem_y', 'gem_b', 'gem_o'].map(s => h('img', { src: t.preview + s + '.png', alt: '' })))] : icon('brush')),
      h('div.theme-meta', h('b', t.name), h('small', t.author || ''), t.id === st.current ? h('span.chip.ok', 'Puesto') : null))));
    const t = theme();
    const acts = root.querySelector('.theme-acts');
    acts.replaceChildren(
      h('button.btn.primary', { disabled: busy || t.id === st.current || st.gameRunning, onclick: apply }, icon('check'), t.id === st.current ? 'Puesto en el juego' : 'Poner en el juego'),
      t.id && t.id !== st.current ? h('button.btn.sm.ghost', { onclick: () => remove(t) }, icon('trash'), 'Quitar') : null,
      h('button.btn.sm.ghost', { title: 'Otra canción para el preview', onclick: () => { song = null; startPreview(true); } }, icon('skip')),
      st.gameRunning ? h('span.chip.warn', 'Cierra el juego para cambiar el tema') : null);
    sideEl.replaceChildren(
      h('div', { style: { display: 'flex', gap: '8px', margin: '12px 0' } },
        h('button.btn.sm', { disabled: busy, onclick: importFile }, icon('import'), 'Importar tema…')),
      st.catalog.length ? h('div.card',
        h('h2', 'Temas de la comunidad'),
        h('p.faint', { style: { margin: '0 0 8px' } }, 'De la página de temas de FastGH3 (donnaken15.com). Se descargan aquí y no se tocan tus archivos hasta que pongas uno.'),
        st.catalog.map(c => h('div.cat-row', h('div', h('b', c.name), h('small', c.author + ' · ' + c.size)),
          h('button.btn.sm', { disabled: busy, onclick: () => download(c) }, icon('import'), 'Descargar')))) : null,
      h('p.faint', { style: { marginTop: '10px' } }, 'Se cambian las texturas que trae el tema (notas, autopista, HUD); los menús, personajes y lo que el tema no traiga quedan como en tu juego. Tu original se guarda en Launcher\\backups\\theme.'));
  }

  async function run(fn, okText) {
    busy = true; render();
    try { st = await fn(); if (okText) App.toast(okText); } catch (e) { App.fail(e); }
    busy = false; render(); loadImages();
  }
  const apply = () => run(() => Bridge.call('theme.apply', { id: sel }), 'Tema puesto: se verá la próxima vez que abras el juego');
  const importFile = () => run(() => Bridge.call('theme.import'));
  const download = c => run(async () => { App.toast('Descargando ' + c.name + '…'); const s = await Bridge.call('theme.download', { id: c.id }); sel = c.id; return s; }, 'Tema descargado');
  async function remove(t) {
    if (!await App.confirm('¿Quitar este tema?', t.name, 'Quitar', 'danger')) return;
    run(() => Bridge.call('theme.remove', { id: t.id }));
  }

  /* ---- the preview ---- */
  async function loadImages() {
    const t = theme();
    const base = t && t.preview;
    const out = {};
    await Promise.all(SLOTS.map(s => new Promise(res => {
      if (!base) return res();
      const im = new Image();
      im.onload = () => { out[s] = im; res(); };
      im.onerror = () => res();
      im.src = base + s + '.png';
    })));
    imgs = out;
  }

  // the song on in the background (else any song with notes), from a busy part
  async function pickSong(next) {
    const d = App.state.songs;
    if (!d) return null;
    const bg = BgMusic.info().song;
    let name = !next && bg && bg.kind === 'song' ? bg.name : null;
    const all = Object.keys(d.songs || {});
    for (let tries = 0; tries < 4; tries++) {
      if (!name) name = all[Math.floor(Math.random() * all.length)];
      try {
        const r = await Bridge.call('theme.notes', { song: name });
        if (r.notes && r.notes.length > 20) return { name, notes: r.notes, info: d.songs[name] || {} };
      } catch (e) { }
      name = null;
    }
    return null;
  }

  async function startPreview(next) {
    stopPreview();
    if (App.current !== 'themes') return;
    songLabel.textContent = 'Preparando el preview…';
    const pick = song && !next ? song : await pickSong(next);
    if (!pick || App.current !== 'themes') { songLabel.textContent = 'No hay canciones para el preview.'; loop(); return; }
    song = pick; notes = pick.notes;
    const busyAt = notes[Math.floor(notes.length * 0.4)][0];
    const start = Math.max(0, busyAt - 1500);
    songLabel.textContent = 'Probando con: ' + (pick.info.title || pick.name) + (pick.info.artist ? ' · ' + pick.info.artist : '');
    try {
      const urls = await Bridge.call('songs.prepare', { name: pick.name, stems: ['song', 'guitar', 'rhythm'] });
      if (App.current === 'themes' && urls && urls.length) await Player.play('theme:' + pick.name, urls, start);
    } catch (e) { }
    loop();
  }
  function stopPreview() {
    cancelAnimationFrame(raf); raf = 0;
    if ((Player.info().key || '').startsWith('theme:')) Player.stop();
  }
  function loop() {
    cancelAnimationFrame(raf);
    const step = () => { draw(); raf = requestAnimationFrame(step); };
    raf = requestAnimationFrame(step);
  }

  // perspective: z = 0 at the strike line, 1 at the far end; p(z) shrinks with distance
  const K = 2.4, AHEAD = 1.5;   // seconds of notes on screen
  const p = z => 1 / (1 + K * z);

  function draw() {
    const W = canvas.width, H = canvas.height;
    const yTop = 18, yStrike = H - 70, nearW = W * 0.86;
    const yV = (yTop * (1 + K) - yStrike) / K;
    const Y = z => yV + (yStrike - yV) * p(z);
    const Z = y => ((yStrike - yV) / (y - yV) - 1) / K;
    const info = Player.info();
    const playing = song && (info.key || '') === 'theme:' + song.name;
    const t = playing ? info.pos : performance.now() % 60000;
    cx.clearRect(0, 0, W, H);
    cx.fillStyle = '#050304'; cx.fillRect(0, 0, W, H);

    // the highway: one texture row per screen row, scrolling with the song
    const hw = imgs.highway;
    for (let y = yTop; y < H; y++) {
      const z = Z(y + 0.5), s = p(z), w = nearW * s;
      const x0 = W / 2 - w / 2;
      if (hw) {
        const v = (((t / 1000) * 0.55 + z * AHEAD * 0.55) % 1 + 1) % 1;
        cx.drawImage(hw, 0, Math.floor((1 - v) * (hw.height - 1)), hw.width, 1, x0, y, w, 1);
      } else { cx.fillStyle = '#1b1416'; cx.fillRect(x0, y, w, 1); }
    }
    // rails and strings
    cx.strokeStyle = 'rgba(255,255,255,.35)'; cx.lineWidth = 1;
    for (let i = 0; i <= 5; i++) {
      cx.beginPath();
      const f = i / 5 - 0.5;
      cx.moveTo(W / 2 + f * nearW * p(1), Y(1)); cx.lineTo(W / 2 + f * nearW * p(-0.3), Y(-0.3)); cx.stroke();
    }
    const laneX = (lane, z) => W / 2 + (lane - 2) * (nearW / 5) * p(z);

    // sustains, then notes (far first)
    const visible = notes.filter(n => n[0] + n[1] >= t - 120 && n[0] <= t + AHEAD * 1000);
    for (const n of visible) {
      if (n[1] < 200) continue;
      const z0 = Math.max(0, (n[0] - t) / 1000 / AHEAD), z1 = Math.min(1, (n[0] + n[1] - t) / 1000 / AHEAD);
      if (z1 <= z0) continue;
      for (let f = 0; f < 5; f++) if (n[2] & (1 << f)) {
        cx.strokeStyle = COLORS[f]; cx.globalAlpha = 0.75;
        cx.lineWidth = 9 * p(z0);
        cx.beginPath(); cx.moveTo(laneX(f, z0), Y(z0)); cx.lineTo(laneX(f, z1), Y(z1)); cx.stroke();
        cx.globalAlpha = 1;
      }
    }
    // the strike line's buttons; lit while a note is being hit
    const hit = [0, 0, 0, 0, 0];
    for (const n of visible) {
      const held = t >= n[0] - 40 && t <= n[0] + Math.max(90, n[1]);
      if (held) for (let f = 0; f < 5; f++) if (n[2] & (1 << f)) hit[f] = Math.max(hit[f], 1 - Math.min(1, Math.max(0, t - n[0]) / 160));
    }
    const bw = nearW / 5 * 0.92;
    for (let f = 0; f < 5; f++) {
      const x = laneX(f, 0), y = Y(0);
      const im = imgs['btn_' + 'grybo'[f]];
      if (im) cx.drawImage(im, x - bw / 2, y - bw / 4, bw, bw / 2);
      else { cx.strokeStyle = COLORS[f]; cx.lineWidth = 4; cx.beginPath(); cx.ellipse(x, y, bw * 0.42, bw * 0.18, 0, 0, Math.PI * 2); cx.stroke(); }
      if (hit[f] > 0) {
        cx.globalAlpha = hit[f];
        const fl = imgs.flame;
        if (fl) cx.drawImage(fl, 0, 0, fl.width / 4, fl.height / 4, x - bw * 0.6, y - bw * 1.25, bw * 1.2, bw * 1.2);
        const g = cx.createRadialGradient(x, y, 2, x, y, bw * 0.6);
        g.addColorStop(0, '#fff'); g.addColorStop(0.35, COLORS[f]); g.addColorStop(1, 'rgba(0,0,0,0)');
        cx.fillStyle = g; cx.beginPath(); cx.ellipse(x, y, bw * 0.6, bw * 0.3, 0, 0, Math.PI * 2); cx.fill();
        cx.globalAlpha = 1;
      }
    }
    for (let i = visible.length - 1; i >= 0; i--) {
      const n = visible[i];
      const z = (n[0] - t) / 1000 / AHEAD;
      if (z < -0.02 || z > 1) continue;
      const s = p(z), w = nearW / 5 * 0.8 * s, y = Y(z);
      for (let f = 0; f < 5; f++) if (n[2] & (1 << f)) {
        const im = imgs['gem_' + 'grybo'[f]];
        if (im) cx.drawImage(im, laneX(f, z) - w / 2, y - w * 0.32, w, w / 2);
        else { cx.fillStyle = COLORS[f]; cx.beginPath(); cx.ellipse(laneX(f, z), y, w * 0.42, w * 0.2, 0, 0, Math.PI * 2); cx.fill(); }
      }
    }
    if (!playing && song) { cx.fillStyle = 'rgba(0,0,0,.45)'; cx.fillRect(0, H - 34, W, 34); cx.fillStyle = '#efe3c8'; cx.font = '600 15px "Barlow Condensed", sans-serif'; cx.fillText(I18N.lang === 'es' ? 'Sin audio: el preview corre en silencio' : 'No audio: the preview runs silent', 14, H - 12); }
  }

  return { mount, show, hide, refresh };
})());
