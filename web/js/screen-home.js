/* Inicio: GH3's main menu art, the play button, what is wrong in the folder (Health.cs), what each
   player plays with (and how: guitar or controller), the setlist at a glance, the time played. */
App.register('home', (() => {
  const { h, icon } = App;
  let root, side, stats = null, tick = 0;
  const T = (es, en) => I18N.lang === 'es' ? es : en;

  function mount(r) {
    root = r;
    root.append(h('div.home-room'), side = h('div.home'));
    App.onState('songs', render);
    App.onState('input', render);
    App.onState('health', render);
    App.onState('game', () => setTimeout(loadStats, 1500));
    loadStats();
  }

  async function loadStats() {
    try { stats = await Bridge.call('stats.state'); stats.at = Date.now(); } catch (e) { }
    render();
  }

  /* the time played: everything so far, the one being played now included */
  function playtime() {
    if (!stats) return null;
    const live = stats.current > 0 ? stats.current + (Date.now() - stats.at) / 1000 : 0;
    const total = stats.seconds + live;
    const parts = [];
    if (stats.sessions) parts.push(stats.sessions === 1 ? T('1 sesión', '1 session') : `${stats.sessions} ${T('sesiones', 'sessions')}`);
    if (stats.longest) parts.push(`${T('la más larga', 'longest')}: ${I18N.duration(stats.longest, 2)}`);
    if (live > 0) parts.push(`${T('jugando ahora', 'playing now')}: ${I18N.duration(live, 2)}`);
    else if (stats.last) parts.push(`${T('última vez', 'last time')}: ${I18N.ago(stats.last)}`);
    return h('div.playtime', { 'data-sfx': 'none' },
      h('span.pt-label', 'Tiempo jugado'),
      h('b.pt-value', total >= 1 ? I18N.duration(total) : T('Todavía no juegas desde el launcher.', "You haven't played from the launcher yet.")),
      parts.length ? h('small', parts.join(' · ')) : null);
  }

  /* each problem with what fixes it */
  function healthNotices() {
    const list = App.state.health || [];
    return list.map(it => h('div.notice' + (it.level === 'error' ? '.bad' : ''), icon('warn'),
      h('div', h('b', it.title + '. '), it.detail,
        it.fix === 'admin' ? h('div.notice-act', h('button.btn.sm', { onclick: () => Bridge.call('app.restartAdmin').catch(App.fail) }, 'Abrir como administrador')) : null,
        it.fix === 'assets' ? h('div.notice-act', h('button.btn.sm', { onclick: async () => { try { await Bridge.call('assets.ensure'); location.reload(); } catch (e) { App.fail(e); } } }, 'Volver a leer')) : null,
        it.fix === 'x360ce' ? h('div.notice-act', h('button.btn.sm', { onclick: () => Bridge.call('tools.run', { id: 'x360ce' }).catch(App.fail) }, 'Abrir x360ce')) : null)));
  }

  /* what GH3 will get: x360ce's connected pads and keyboard 2, each played as a guitar (you strum)
     or as a plain Xbox controller (no strumming); a click switches it, except for a real Xbox pad
     x360ce passes through, which is always a controller. GH3's own keyboard is always there, so it
     isn't listed. */
  function players() {
    const i = App.state.input; if (!i) return [];
    const list = (i.devices || []).filter(d => d.configured && d.pad && d.connected).sort((a, b) => a.pad - b.pad)
      .map(d => ({ id: d.id, name: d.name, pad: d.pad, style: d.style, fixed: d.passThrough }));
    const kb = i.keyboard2;
    if (kb && kb.enabled && kb.slot) list.push({ id: 'kb2', name: 'Teclado 2', pad: kb.slot, style: kb.style });
    return list;
  }

  async function switchStyle(p) {
    if (p.fixed) { App.toast('Control de Xbox directo', 'x360ce lo pasa tal cual: GH3 siempre lo recibe como control, sin rasguear.'); return; }
    const style = p.style === 'controller' ? 'guitar' : 'controller';
    try {
      const s = p.id === 'kb2' ? await Bridge.call('input.keyboard2', { keyboard2: Object.assign({}, App.state.input.keyboard2, { style }) })
        : await Bridge.call('input.style', { id: p.id, style });
      App.state.input = s;
      App.summarizeInput();
      App.emit('input', s);
    } catch (e) { App.fail(e); }
  }

  function count(section) {
    const s = App.state.songs && App.state.songs.sections.find(x => x.id === section);
    return s ? s.tiers.reduce((n, t) => n + t.songs.length, 0) : '–';
  }

  function render() {
    if (!side) return;
    const app = App.state.app || {}, songs = App.state.songs;
    const lib = (songs && songs.library) || {};
    const total = songs ? songs.total : 0;
    const playing = players();
    const card = p => {
      const pad = p.style === 'controller';
      return h('button.mode-card.on', { title: 'Clic para cambiar entre guitarra y control', onclick: () => switchStyle(p) },
        h('span.pad-tag' + (p.pad === 1 ? '.p1' : ''), 'PAD ' + p.pad),
        icon(pad ? 'pad' : 'guitar'), h('b', pad ? 'Control' : 'Guitarra'),
        h('small', pad ? 'Sin rasgueo' : 'Rasgueas tú'),
        h('small.dev-name', { title: p.name }, p.name));
    };
    const recent = Object.entries(lib).sort((a, b) => String(b[1].installedAt).localeCompare(String(a[1].installedAt))).slice(0, 5);

    side.replaceChildren(...[
      h('div',
        h('h1', '¡A rockear!'),
        h('p.lead', total ? `${total} canciones en tu Guitar Hero III.` : 'Leyendo tu setlist…')),
      h('button.play-btn.play-big', { onclick: App.play, disabled: app.game && app.game.running }, icon('play'), h('span', 'Jugar')),
      ...healthNotices(),
      h('div',
        h('div.section-title', '¿Con qué vas a tocar?'),
        playing.length ? h('div.mode-cards', playing.map(card)) :
          h('p.muted', { style: { margin: 0 } }, 'No hay guitarras ni controles conectados con un PAD de x360ce (el teclado del juego siempre funciona). ', h('button.link', { onclick: () => App.go('controls') }, 'Ver controles →')),
        playing.length ? h('p.faint', { style: { margin: '6px 0 0', fontSize: '14px' } }, 'Clic en una tarjeta para cambiar entre guitarra (rasgueas tú) y control de Xbox (sin rasgueo).') : null),
      h('div.stat-row',
        h('div', h('b', count('setlist')), h('span', 'Setlist')),
        h('div', h('b', count('bonus')), h('span', 'Bonus')),
        h('div', h('b', count('download')), h('span', 'Download')),
        h('div', h('b', Object.keys(lib).length), h('span', 'Importadas'))),
      playtime(),
      h('div',
        h('div.section-title', 'Importadas hace poco'),
        recent.length ? h('div.recent', recent.map(([id, s]) => h('button', { onclick: () => { App.go('songs'); App.emit('select-song', id); } },
          s.cover ? h('img', { src: 'https://gh3.covers/' + encodeURIComponent(s.cover), alt: '' }) : h('span.ph'),
          h('div', h('b', s.title), h('span', s.artist)))))
          : h('p.muted', { style: { margin: 0 } }, 'Todavía no importas canciones. ', h('button.link', { onclick: () => App.go('import') }, 'Importar ahora →')))
    ].filter(Boolean));
  }

  return {
    mount,
    // while the game runs, the time played counts up second by second
    show: () => { render(); loadStats(); clearInterval(tick); tick = setInterval(() => { if (stats && stats.current > 0) { const pt = side.querySelector('.playtime'); if (pt) pt.replaceWith(playtime()); } }, 1000); },
    hide: () => clearInterval(tick)
  };
})());
