/* The shell: screens, the nav, toasts and modals, the play button, drag & drop. */
/* conditional children (cond && node) leave false/null behind: never print them */
for (const m of ['replaceChildren', 'append']) {
  const orig = Element.prototype[m];
  Element.prototype[m] = function (...kids) { return orig.apply(this, kids.flat(9).filter(k => k != null && k !== false && k !== true)); };
}

const App = (() => {
  const screens = {};
  let current = null;
  const state = { app: null, songs: null, input: null };

  /* h('div.cls#id', {attrs}, children...) */
  function h(tag, attrs, ...kids) {
    const m = tag.match(/^([a-z0-9]+)?((?:[.#][\w-]+)*)$/i);
    const e = document.createElement(m[1] || 'div');
    (m[2].match(/[.#][\w-]+/g) || []).forEach(p => p[0] === '.' ? e.classList.add(p.slice(1)) : (e.id = p.slice(1)));
    if (attrs && (attrs.constructor !== Object)) { kids.unshift(attrs); attrs = null; }
    for (const k in attrs || {}) {
      const v = attrs[k];
      if (v == null || v === false) continue;
      if (k.startsWith('on')) e.addEventListener(k.slice(2), v);
      else if (k === 'class') e.className += ' ' + v;
      else if (k === 'html') e.innerHTML = v;
      else if (k === 'style' && typeof v === 'object') for (const s in v) s.startsWith('--') ? e.style.setProperty(s, v[s]) : (e.style[s] = v[s]);
      else if (k in e && k !== 'list' && typeof v !== 'string') e[k] = v;
      else e.setAttribute(k, v === true ? '' : v);
    }
    kids.flat(9).forEach(c => c != null && c !== false && e.append(c.nodeType ? c : document.createTextNode(String(c))));
    return e;
  }
  const icon = (name, cls) => { const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); if (cls) s.setAttribute('class', cls); const u = document.createElementNS('http://www.w3.org/2000/svg', 'use'); u.setAttribute('href', '#i-' + name); s.append(u); return s; };
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];

  function register(name, def) { screens[name] = def; }
  function go(name) {
    if (!screens[name]) return;
    if (current === name) { screens[name].refresh && screens[name].refresh(); return; }
    const prev = current;
    if (prev) { $('#screen-' + prev).classList.remove('on'); screens[prev].hide && screens[prev].hide(); }
    current = name;
    $$('#nav .menu button').forEach(b => b.classList.toggle('on', b.dataset.go === name));
    const root = $('#screen-' + name);
    if (!screens[name].mounted) { screens[name].mounted = true; screens[name].mount(root); }
    root.classList.add('on');
    screens[name].show && screens[name].show();
    try { localStorage.setItem('screen', name); } catch (e) { }
  }

  /* ---- feedback ---- */
  function toast(title, text = '', kind = '') {
    const t = h('div.toast' + (kind ? '.' + kind : ''), h('b', title), text);
    $('#toasts').append(t);
    setTimeout(() => { t.style.transition = 'opacity .3s'; t.style.opacity = 0; setTimeout(() => t.remove(), 320); }, kind === 'bad' ? 7000 : 3800);
  }
  const fail = e => { toast('Algo salió mal', e && e.message || String(e), 'bad'); if (typeof Sfx !== 'undefined') Sfx.play('warning'); };

  function modal({ title, body, actions, wide }) {
    return new Promise(resolve => {
      const close = v => { back.remove(); document.removeEventListener('keydown', key); resolve(v); };
      const key = e => { if (e.key === 'Escape') close(null); };
      const foot = h('footer', (actions || [{ label: 'Cerrar', value: null }]).map(a =>
        h('button.btn' + (a.kind ? '.' + a.kind : ''), { onclick: () => close(typeof a.value === 'function' ? a.value() : a.value) }, a.label)));
      const box = h('div.modal' + (wide ? '.wide' : ''), h('header', h('h2', title)), h('div.body', body), foot);
      const back = h('div.modal-back', { onmousedown: e => { if (e.target === back) close(null); } }, box);
      $('#modal-root').append(back);
      document.addEventListener('keydown', key);
      const first = box.querySelector('input, select, textarea');
      if (first) setTimeout(() => first.focus(), 30);
    });
  }
  const confirm = (title, text, label = 'Aceptar', kind = 'primary') =>
    modal({ title, body: h('p.muted', { style: { fontSize: '17px', margin: '4px 0 0' } }, text), actions: [{ label: 'Cancelar', value: false, kind: 'ghost' }, { label, value: true, kind }] });

  /* ---- format ---- */
  const fmtTime = ms => { if (!ms || ms < 0) return '0:00'; const s = Math.round(ms / 1000); return Math.floor(s / 60) + ':' + String(s % 60).padStart(2, '0'); };
  const fmtSize = b => b > 1e6 ? (b / 1e6).toFixed(1) + ' MB' : Math.max(1, Math.round(b / 1e3)) + ' KB';
  const debounce = (fn, ms) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

  /* ---- app state, game status, play ---- */
  async function refreshApp() {
    try {
      state.app = await Bridge.call('app.state');
      setGame(state.app.game.running);
    } catch (e) { fail(e); }
    return state.app;
  }
  let gameTimer = null;
  function setGame(running) {
    emit('game', running);
    $('#game-status').hidden = !running;
    $('#play').disabled = running;
    $$('.play-big').forEach(b => b.disabled = running);
    clearInterval(gameTimer);
    if (running) {
      const t0 = Date.now() - ((state.app && state.app.game.seconds) || 0) * 1000;
      const tick = () => { $('#game-status-text').textContent = 'Jugando · ' + fmtTime(Date.now() - t0); };
      tick(); gameTimer = setInterval(tick, 1000);
    }
  }
  async function play() {
    try {
      $('#play').disabled = true;
      emit('game', true);   // menu music and previews go quiet on the click, not when GH3 shows up
      Player.stop();
      await Bridge.call('game.launch');
      if (state.app) state.app.game = { running: true, seconds: 0 };
      setGame(true);
    } catch (e) { $('#play').disabled = false; emit('game', false); fail(e); }
  }
  async function refreshSongs(refresh = false) {
    try {
      state.songs = await Bridge.call('songs.list', { refresh });
      const n = state.songs.total || Object.keys(state.songs.songs || {}).length;
      $('#badge-songs').textContent = n || '';
      emit('songs', state.songs);
    } catch (e) { fail(e); }
    return state.songs;
  }
  async function refreshInput() {
    try { state.input = await Bridge.call('input.state'); summarizeInput(); emit('input', state.input); } catch (e) { fail(e); }
    return state.input;
  }
  function summarizeInput() {
    // the pads GH3 will get (x360ce's connected ones, keyboard 2); GH3's own keyboard always works
    const i = state.input; if (!i) return;
    const pads = (i.devices || []).filter(d => d.configured && d.pad && d.connected);
    const n = pads.length + (i.keyboard2 && i.keyboard2.enabled && i.keyboard2.slot ? 1 : 0);
    $('#input-summary').textContent = n === 0 ? 'Solo teclado' : n === 1 ? '1 control listo' : `${n} controles listos`;
    $('#input-summary').title = pads.map(d => `PAD ${d.pad}: ${d.name}`).join('\n');
  }

  /* a tiny event hub between screens */
  const hub = {};
  const onState = (k, fn) => (hub[k] = hub[k] || []).push(fn);
  const emit = (k, v) => (hub[k] || []).forEach(fn => { try { fn(v); } catch (e) { console.error(e); } });

  /* ---- drag & drop anywhere: goes to Importar ---- */
  function setupDrop() {
    let depth = 0;
    const ov = $('#drop-overlay');
    const hasFiles = e => e.dataTransfer && [...e.dataTransfer.types].includes('Files');
    window.addEventListener('dragenter', e => { if (!hasFiles(e)) return; e.preventDefault(); depth++; ov.hidden = false; });
    window.addEventListener('dragleave', e => { if (!hasFiles(e)) return; depth = Math.max(0, depth - 1); if (!depth) ov.hidden = true; });
    window.addEventListener('dragover', e => { if (hasFiles(e)) { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; } });
    window.addEventListener('drop', async e => {
      if (!hasFiles(e)) return;
      e.preventDefault(); depth = 0; ov.hidden = true;
      const items = [...e.dataTransfer.items].filter(i => i.kind === 'file');
      const objs = [];
      for (const it of items) {
        let handle = null;
        try { if (it.getAsFileSystemHandle) handle = await it.getAsFileSystemHandle(); } catch (x) { }
        if (handle && handle.kind === 'directory') objs.push(handle);
        else { const f = it.getAsFile(); if (f) objs.push(f); }
      }
      if (!objs.length) return;
      go('import');
      try { const r = await Bridge.drop(objs); if (!r.added) toast('No se encontraron canciones', 'Arrastra .sng, .zip, .7z, .rar o carpetas con song.ini / notes.chart / notes.mid.'); }
      catch (x) { fail(x); }
    });
  }

  function init() {
    $$('#nav .menu button').forEach(b => b.addEventListener('click', () => go(b.dataset.go)));
    $('#play').addEventListener('click', play);
    $('#game-front').addEventListener('click', () => Bridge.call('game.front'));
    $('#game-kill').addEventListener('click', async () => { if (await confirm('¿Cerrar el juego?', 'Se cerrará Guitar Hero III sin guardar lo que no se haya guardado.', 'Cerrar GH3', 'danger')) Bridge.call('game.kill'); });
    $('#fullscreen').addEventListener('click', () => Bridge.call('window.fullscreen', { toggle: true }));
    Bridge.on('game.started', () => setGame(true));
    Bridge.on('game.exited', d => { if (state.app) state.app.game.running = false; setGame(false); toast('Guitar Hero III se cerró', d && d.seconds ? 'Sesión de ' + fmtTime(d.seconds * 1000) + '.' : ''); refreshInput(); });
    Bridge.on('songs.changed', () => refreshSongs());
    setupDrop();
    document.addEventListener('contextmenu', e => { if (!e.target.closest('input, textarea')) e.preventDefault(); });
  }

  return { h, icon, $, $$, register, go, toast, fail, modal, confirm, fmtTime, fmtSize, debounce, state, refreshApp, refreshSongs, refreshInput, summarizeInput, onState, emit, init, play, get current() { return current; } };
})();
