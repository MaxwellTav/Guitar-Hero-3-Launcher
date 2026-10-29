/* Menu music, like GH3's own menus. Two lists, each played to the end in a shuffled order:
     1. the game's menu music (the six tracks GH3 itself plays in its menus),
     2. then every song you have (Setlist, Bonus, DLC and imported ones), as their short previews,
   and round again with new shuffles. Its own <audio>, apart from Player: it steps aside while a song is
   being listened to, while the game runs and while the launcher is minimized.
   Launcher settings: menuMusic (on/off), menuMusicVolume (0-1), menuMusicMenu (list 1),
   menuMusicGame (Setlist, Bonus, DLC), menuMusicImported (imported songs). */
const BgMusic = (() => {
  const a = new Audio();
  a.preload = 'auto';
  let cur = null, token = 0, fade = 0, gap = 0, started = false, held = false;
  let phase = 'menu';            // which list is playing
  let queue = [];                // what is left of it, already shuffled
  let menuTracks = null;         // the game's menu music: [{id, title, artist, url}]
  const subs = [];

  const cfg = () => {
    const L = (App.state.app && App.state.app.launcher) || {};
    return {
      on: L.menuMusic !== false,
      volume: typeof L.menuMusicVolume === 'number' ? L.menuMusicVolume : 0.35,
      menu: L.menuMusicMenu !== false,
      game: L.menuMusicGame !== false,
      imported: L.menuMusicImported !== false
    };
  };

  function shuffle(list) {
    const x = list.slice();
    for (let i = x.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [x[i], x[j]] = [x[j], x[i]]; }
    if (x.length > 1 && cur && x[0].id === cur.id) x.push(x.shift());   // never the same song twice in a row
    return x;
  }

  /* list 1: the game's menu music */
  async function menuList() {
    if (!cfg().menu) return [];
    if (!menuTracks) { try { menuTracks = await Bridge.call('menu.music'); } catch (e) { menuTracks = []; } }
    return menuTracks.map(t => Object.assign({ kind: 'menu' }, t));
  }

  /* list 2: every song with audio, as its preview */
  function songList() {
    const d = App.state.songs, c = cfg();
    if (!d) return [];
    const lib = d.library || {};
    const seen = new Set(), out = [];
    d.sections.forEach(s => s.tiers.forEach(t => t.songs.forEach(id => {
      const x = d.songs[id];
      if (!x || !x.audio || seen.has(id)) return;
      const imported = !!lib[id];
      if (imported ? !c.imported : !c.game) return;
      seen.add(id);
      out.push({ kind: 'song', id, name: x.name, title: x.title, artist: x.artist, imported });
    })));
    return out;
  }

  const allowed = item => { const c = cfg(); return item.kind === 'menu' ? c.menu : item.imported ? c.imported : c.game; };

  /* the next track: the rest of this list, else the other list from the top (new shuffle) */
  async function take() {
    queue = queue.filter(allowed);
    for (let tries = 0; tries < 2 && !queue.length; tries++) {
      phase = phase === 'menu' ? 'songs' : 'menu';
      queue = shuffle(phase === 'menu' ? await menuList() : songList());
    }
    return queue.shift() || null;
  }

  // something else is using the speakers: a preview in Canciones/Importar, the game, a hidden window
  let gameRunning = false;
  let userPaused = false;   // the pause button next to "next": stays until pressed again
  const blocked = () => {
    const p = Player.info();
    return userPaused || held || gameRunning || document.hidden || !!(p.key && (p.playing || p.loading));
  };

  function ramp(to, ms, then) {
    cancelAnimationFrame(fade);
    const from = a.volume, t0 = performance.now();
    const step = now => {
      const k = Math.min(1, (now - t0) / ms);
      a.volume = Math.max(0, Math.min(1, from + (to - from) * k));
      if (k < 1) fade = requestAnimationFrame(step); else if (then) then();
    };
    fade = requestAnimationFrame(step);
  }

  async function next() {
    clearTimeout(gap);
    if (!cfg().on || blocked()) return;
    const my = ++token;
    const pick = await take();
    if (my !== token) { if (pick) queue.unshift(pick); return; }
    if (!pick) { cur = null; notify(); return; }
    try {
      let src = pick.url;
      if (pick.kind === 'song') {
        const srcs = await Bridge.call('songs.prepare', { name: pick.name, stems: ['preview'] });
        if (my !== token) { queue.unshift(pick); return; }
        src = srcs[0];
      }
      if (!src) { gap = setTimeout(next, 300); return; }
      cur = pick;
      a.src = src;
      a.volume = 0;
      if (blocked() || !cfg().on) { notify(); return; }
      await a.play();
      if (my !== token) return;
      ramp(cfg().volume, 1200);
      notify();
    } catch (e) {
      if (my === token) gap = setTimeout(next, 2000);
    }
  }

  a.addEventListener('ended', () => { gap = setTimeout(next, 900); });
  a.addEventListener('timeupdate', () => {
    // fade out over the clip's last 1.5 s, like the game's menu
    if (a.duration && a.duration - a.currentTime < 1.5 && !a._out) { a._out = true; ramp(0, 1400); }
  });
  a.addEventListener('playing', () => { a._out = false; });

  function stop(fadeMs = 600) {
    token++;
    clearTimeout(gap);
    if (!a.paused) ramp(0, fadeMs, () => a.pause()); else a.pause();
  }

  /* pause/resume as the world changes */
  function check() {
    if (!started) return;
    const c = cfg();
    if (!c.on || blocked()) { if (!a.paused) { token++; ramp(0, 400, () => a.pause()); } clearTimeout(gap); notify(); return; }
    if (cur && a.src && a.paused && !a.ended && a.currentTime > 0) { a.play().then(() => ramp(c.volume, 800)).catch(() => next()); notify(); return; }
    if (a.paused) next();
  }

  function start() {
    if (started) return;
    started = true;
    let busy = false;   // Player reports ten times a second: act only when it starts or stops
    Player.onChange(i => { const b = !!(i.key && (i.playing || i.loading)); if (b !== busy) { busy = b; check(); } });
    document.addEventListener('visibilitychange', check);
    // the game: silent from the click on "Jugar" until it closes (App emits 'game' for both)
    App.onState('game', running => { gameRunning = !!running; if (running) check(); else setTimeout(check, 1500); });
    gameRunning = !!(App.state.app && App.state.app.game && App.state.app.game.running);
    // a GH3 opened outside the launcher (its own shortcut) never says so: look every few seconds while playing
    setInterval(async () => {
      if (gameRunning || a.paused) return;
      try { if (await Bridge.call('game.running')) App.refreshApp(); } catch (e) { }
    }, 6000);
    App.onState('songs', () => { if (a.paused && !cur) check(); });
    // the first round starts with the game's menu music
    phase = 'songs'; queue = [];
    check();
  }

  /* settings changed in Opciones */
  function apply() {
    const c = cfg();
    if (!c.on) { stop(); cur = null; notify(); return; }
    if (cur && !allowed(cur)) { stop(300); cur = null; setTimeout(next, 350); return; }
    if (!a.paused) ramp(c.volume, 250);
    check();
  }

  function skip() { userPaused = false; stop(250); setTimeout(next, 280); }
  function hold(on) { held = on; check(); }
  function pause(on) { userPaused = !!on; check(); notify(); }

  const info = () => ({ song: cur, playing: !a.paused, paused: userPaused, on: cfg().on, phase, time: a.currentTime, duration: a.duration || 0 });
  function notify() { const i = info(); subs.forEach(fn => { try { fn(i); } catch (e) { console.error(e); } }); }

  return { start, apply, skip, hold, pause, info, onChange: fn => subs.push(fn) };
})();
