/* The bridge to the native host (WebView2: window.chrome.webview). Requests carry an id and come
   back as {id, ok, result | error}; the host's own events arrive as {event, data}.
   Opened in an ordinary browser (a preview), a simulated host answers instead. */
const Bridge = (() => {
  const wv = window.chrome && window.chrome.webview;
  const pending = new Map();
  const listeners = {};
  let seq = 0;

  function on(event, fn) { (listeners[event] = listeners[event] || []).push(fn); }
  function emit(event, data) { (listeners[event] || []).forEach(fn => { try { fn(data); } catch (e) { console.error(e); } }); }

  if (wv) {
    wv.addEventListener('message', e => {
      const m = e.data;
      if (m && m.id && pending.has(m.id)) {
        const p = pending.get(m.id);
        pending.delete(m.id);
        m.ok ? p.resolve(m.result) : p.reject(new Error(m.error || 'error'));
      } else if (m && m.event) emit(m.event, m.data);
    });
    window.addEventListener('error', e => wv.postMessage({ id: 0, type: 'log', data: { text: `${e.message} @ ${e.filename}:${e.lineno}` } }));
    window.addEventListener('unhandledrejection', e => wv.postMessage({ id: 0, type: 'log', data: { text: String(e.reason && e.reason.stack || e.reason) } }));
  }

  function call(type, data = {}) {
    if (!wv) return Sim.call(type, data);
    return new Promise((resolve, reject) => {
      const id = ++seq;
      pending.set(id, { resolve, reject });
      wv.postMessage({ id, type, data });
    });
  }

  /* files dropped on the page: their paths only reach the host as "additional objects" */
  function drop(files) {
    if (!wv) return Sim.call('import.add', { paths: files.map(f => f.name) });
    return new Promise((resolve, reject) => {
      const id = ++seq;
      pending.set(id, { resolve, reject });
      wv.postMessageWithAdditionalObjects({ id, type: 'import.drop', data: {} }, files);
    });
  }

  /* ---- the simulated host (preview in a browser) ---- */
  const Sim = (() => {
    const art = 'assets/art/';
    const songs = {};
    const add = (id, title, artist, year, extra = {}) => { songs[id] = { id, name: id, title, artist, year, famousBy: !!extra.famous, charter: extra.charter || null, album: extra.album || null, audio: true }; return id; };
    const tiers = [
      ['1. Starting out small', ['slowride', 'talkdirtytome', 'hitmewithyourbestshot', 'storyofmylife', 'rocknrollallnite', 'sabotage']],
      ['2. Your first real gig', ['cultofpersonality', 'missmurder', 'theseeker', 'laydown', 'paintitblack', 'paranoid']],
      ['3. Making the video', ['anarchyintheuk', 'koolthing', 'mynameisjonas', 'evenflow', 'holidayincambodia', 'rockyouliketohurricane']],
      ['8. Battle for your Soul', ['thrufireandflames', 'cliffsofdover', 'knightsofcydonia', 'raininblood']]
    ];
    const names = { slowride: ['Slow Ride', 'Foghat', 1975], talkdirtytome: ['Talk Dirty to Me', 'Poison', 1987], hitmewithyourbestshot: ['Hit Me With Your Best Shot', 'Pat Benatar', 1980], storyofmylife: ['Story of My Life', 'Social Distortion', 1990], rocknrollallnite: ['Rock and Roll All Nite', 'Kiss', 1975], sabotage: ['Sabotage', 'Beastie Boys', 1994], cultofpersonality: ['Cult of Personality', 'Living Colour', 1988], missmurder: ['Miss Murder', 'AFI', 2006], theseeker: ['The Seeker', 'The Who', 1970], laydown: ['Lay Down', 'Priestess', 2005], paintitblack: ['Paint It Black', 'The Rolling Stones', 1966], paranoid: ['Paranoid', 'Black Sabbath', 1970], anarchyintheuk: ['Anarchy in the U.K.', 'The Sex Pistols', 2007], koolthing: ['Kool Thing', 'Sonic Youth', 1990], mynameisjonas: ['My Name Is Jonas', 'Weezer', 1994], evenflow: ['Even Flow', 'Pearl Jam', 1991], holidayincambodia: ['Holiday in Cambodia', 'Dead Kennedys', 1980], rockyouliketohurricane: ['Rock You Like a Hurricane', 'Scorpions', 1984], thrufireandflames: ['Through the Fire and Flames', 'DragonForce', 2006], cliffsofdover: ['Cliffs of Dover', 'Eric Johnson', 1990], knightsofcydonia: ['Knights of Cydonia', 'Muse', 2006], raininblood: ['Raining Blood', 'Slayer', 1986] };
    Object.entries(names).forEach(([id, [t, a, y]]) => add(id, t, a, String(y)));
    const bonus = [add('avalancha', 'Avalancha', 'Heroes del Silencio', '1995'), add('ruby', 'Ruby', 'Kaiser Chiefs', '2007'), add('impulse', 'Impulse', 'An Endless Sporadic', '2006')];
    const dl = [add('dlc5', 'Halo Theme MJOLNIR Mix', 'Steve Ouimette', '2007'), add('dlc17', 'Hier Kommt Alex', 'Die Toten Hosen', '1988'), add('beatit', 'Beat It', 'Michael Jackson', '1983', { charter: 'Neversoft', album: 'Thriller' }), add('afterlife', 'Afterlife', 'Avenged Sevenfold', '2009', { charter: 'DeltaOm3ga, Stickheadz32', album: 'Avenged Sevenfold' })];
    const library = { beatit: { title: 'Beat It', artist: 'Michael Jackson', album: 'Thriller', year: 1983, charter: 'Neversoft', lengthMs: 278229, source: 'Michael Jackson - Beat It (Neversoft).sng', installedAt: '2026-10-27 01:12', cover: null }, afterlife: { title: 'Afterlife', artist: 'Avenged Sevenfold', album: 'Avenged Sevenfold', year: 2009, charter: 'DeltaOm3ga, Stickheadz32', lengthMs: 354226, source: 'Avenged Sevenfold - Afterlife.sng', installedAt: '2026-10-27 01:14', cover: null } };
    const list = { total: Object.keys(songs).length, songs, sections: [
      { id: 'setlist', title: 'Setlist', tiers: tiers.map(([title, s]) => ({ title, songs: s })) },
      { id: 'bonus', title: 'Bonus', tiers: [{ title: 'Bonus songs', songs: bonus }] },
      { id: 'download', title: 'Download', tiers: [{ title: 'Downloaded songs', songs: dl }] }
    ], library };
    let queue = [];
    const input = {
      x360ce: { app: true, dll: 'ok', ini: true, running: false }, chordWindowMs: 12, pulseMs: 35, gameRunning: false,
      keyboard2: { enabled: false, style: 'guitar', focusOnly: true, slot: 3, keys: { green: [65], red: [83], yellow: [68], blue: [70], orange: [71], strumUp: [104], strumDown: [98, 101], left: [100], right: [102], start: [107], select: [96], whammy: [105], tilt: [110] } },
      whammy: { Enabled: 1, ParkFix: 1, RestDeadzone: 0.1, Sensitivity: 1.5, Amplify: 1, MotionGain: 2.5, MotionSmoothMs: 120, Turbo: 1, TurboHz: 9, TurboDepth: 0.7, TurboThreshold: 0.05, DebugLog: 0 },
      devices: [
        { id: 'IG_cf80fd10d0d311f18001444553540000', instance: 'cf80fd10-d0d3-11f1-8001-444553540000', product: '30101bad-0000-0000-0000-504944564944', name: 'Harmonix Guitar Controller for Nintendo Wii', configured: true, connected: true, pad: 1, passThrough: false, controllerType: 7, style: 'guitar', xinput: false, axes: 4, buttons: 13, povs: 1, keys: { A: '2', B: '3', Y: '4', X: '1', 'Left Shoulder': '5', Back: '9', Start: '10', 'D-pad POV': '1', 'Right Analog X': '3', 'Right Analog Y+ Button': '6' } },
        { id: 'IG_f432d950b56a11f1800e444553540000', instance: 'f432d950-b56a-11f1-800e-444553540000', product: '028e045e-0000-0000-0000-504944564944', name: 'Controller (XBOX 360 For Windows)', configured: true, connected: true, pad: 2, passThrough: true, controllerType: 1, style: 'controller', xinput: true, axes: 5, buttons: 10, povs: 1, keys: {} },
        { id: 'IG_6f5a4ab0bf6011f18006444553540000', instance: '6f5a4ab0-bf60-11f1-8006-444553540000', product: '0ce6054c-0000-0000-0000-504944564944', name: 'DualSense Wireless Controller', configured: true, connected: false, pad: 0, passThrough: false, controllerType: 7, style: 'controller', xinput: false, axes: 0, buttons: 0, povs: 0, keys: { A: '2', B: '3' } }
      ]
    };
    const settings = {
      aspyr: { 'Video.Width': '1920', 'Video.Height': '1080', 'Options.GraphicsQuality': '0', 'Options.Crowd': '1', 'Options.Physics': '1', 'Options.Flares': '1', 'Options.FrontRowCamera': '0', 'Sound.SongSkew': '0' },
      plus: { 'GFX.Windowed': null, 'GFX.Borderless': null, 'GFX.VSync': null, 'Player.MaxFPS': null, 'Player.MaxNotes': null },
      plugins: [
        { name: 'core.dll', enabled: true, title: 'Núcleo GH3+', desc: 'Carga los demás plugins. No se puede desactivar.', locked: true },
        { name: 'InputPlus.dll', enabled: true, title: 'InputPlus', desc: 'Del launcher: Teclado 2 (segundo jugador, guitarra o control) y whammy mejorado.' },
        { name: 'BorderlessWindow.dll', enabled: true, title: 'Ventana sin bordes', desc: 'Pantalla completa en modo ventana.' },
        { name: 'TapHopoChord.dll', enabled: true, title: 'Taps, opens y acordes HOPO', desc: 'Notas tap/open de Clone Hero en los customs.' },
        { name: 'WhammyBoost.dll', enabled: false, title: 'WhammyBoost (anterior)', desc: 'Reemplazado por InputPlus.' }
      ],
      language: 'en', aspyrExists: true,
      display: { modes: [[1920, 1080], [1680, 1050], [1600, 900], [1280, 720]], current: [1920, 1080], hz: 120 }
    };
    let t = 0;
    setInterval(() => {
      if (!simWatch) return;
      t++;
      const press = Math.floor(t / 20) % 6;
      emit('input.state', [{ id: input.devices[0].instance, axes: [0, 0, -32768 + (t % 40) * 1600, 0, 0, 0, 0, 0], povs: [press === 5 ? 18000 : -1, -1, -1, -1], buttons: [press === 3 ? 1 : 0, press === 0 ? 1 : 0, press === 1 ? 1 : 0, press === 2 ? 1 : 0, press === 4 ? 1 : 0, 0, 0, 0, 0, 0, 0, 0, 0] }]);
    }, 50);
    let simWatch = false;
    const gfxMock = { on: false, downloaded: false, version: '6.8.0', preset: 'remaster', otherDll: false, gameRunning: false, effects: [
      { id: 'light', on: true, value: 6, min: 0, max: 15, slider: true }, { id: 'bloom', on: false, value: 0.6, min: 0, max: 2, slider: true }, { id: 'color', on: true, value: 0.45, min: -0.5, max: 1.5, slider: true },
      { id: 'levels', on: true, value: 1, min: 0, max: 1, slider: false }, { id: 'sharpen', on: true, value: 0.8, min: 0.2, max: 2, slider: true }, { id: 'motion', on: true, value: 0.3, min: 0, max: 0.9, slider: true }] };
    let simUnlocks = {}, simApplied = false;
    const delay = (v, ms = 120) => new Promise(r => setTimeout(() => r(v), ms));

    function call(type, d) {
      switch (type) {
        case 'app.state': return delay({ root: 'C:\\Games\\Guitar Hero III', version: '1.0.0', pathTooLong: false, honeycomb: true, game: { running: false }, launcher: { onPlay: 'minimize' }, language: 'en', sessionStart: '2026-10-27 01:13', assetsReady: true, busy: false, tools: { honeycombGui: true, x360ce: true, fixes: true, onlineAccount: true } });
        case 'health.state': return delay([]);
        case 'stats.state': return delay({ seconds: 187530, sessions: 12, longest: 9420, last: new Date(Date.now() - 2 * 86400e3).toISOString(), current: 0 });
        case 'songs.list': return delay(JSON.parse(JSON.stringify(list)), 300);
        case 'songs.delete': d.names.forEach(n => { const s = list.sections[2].tiers[0].songs; s.splice(s.indexOf(n), 1); }); setTimeout(() => emit('songs.changed', {}), 50); return delay({});
        case 'songs.stems': return delay(['guitar', 'preview', 'rhythm', 'song']);
        case 'songs.prepare': return delay([]);
        case 'songs.order': { const s = list.sections[2].tiers[0]; s.songs = d.names.slice(); setTimeout(() => emit('songs.changed', {}), 50); return delay({ changed: true }); }
        case 'cheats.state': return delay({ options: simUnlocks, list: { applied: simApplied, source: 'patch', cheats: [
          { name: 'AirGuitar', text: 'air guitar', var: 'Cheat_AirGuitar', pattern: [272, 65792, 65792, 4112, 4112, 4352, 4352, 272, 65792, 65792, 4112, 4112, 4352, 4352, 65792, 65792, 4352, 4352] },
          { name: 'hyperspeed', text: 'hyperspeed', var: 'Cheat_Hyperspeed', pattern: [1, 16, 1, 256, 1, 16, 1, 256] },
          { name: 'NoFail', text: 'no fail', var: 'Cheat_NoFail', pattern: [69632, 16, 69632, 65792, 16, 65792, 4352, 1, 4352, 65792, 256, 65792, 69632] },
          { name: 'EasyExpert', text: 'easy expert', var: 'Cheat_EasyExpert', pattern: [69632, 65792, 272, 4112, 17, 257, 4352, 4112] },
          { name: 'LargeGems', text: 'large gems', var: 'Cheat_LargeGems', pattern: [65536, 4096, 65536, 256, 65536, 16, 65536, 1, 65536, 16, 65536, 256, 65536, 4096, 65536, 69632, 4352, 69632, 272, 69632, 17, 69632, 272, 69632, 4352, 69632, 65792] },
          { name: 'UnlockAllEverything', text: 'unlocked everything', var: 'Cheat_UnlockAll_Everything', pattern: [69649, 69904, 69889, 65809, 69904, 4369, 69904, 65809, 69904, 69889, 69889, 69904, 69889] }] } });
        case 'cheats.apply': { simUnlocks = d.options; simApplied = !!(d.options.songs || d.options.items || d.options.cheats || d.options.cash || (d.options.on || []).length || d.options.hyperspeed); return delay({ result: { applied: simApplied }, options: simUnlocks }, 600); }
        case 'import.list': return delay(queue);
        case 'import.add': case 'import.pick': {
          const it = { id: 'x' + Date.now(), title: 'Black or White', artist: 'Michael Jackson', album: 'Dangerous', year: 1991, charter: 'Peddy', diffGuitar: 3, lengthMs: 200840, previewStart: 93010, hopoFrequency: 170, sourceName: 'Michael Jackson - Black or White (Peddy).sng', stems: [{ kind: 'backing', name: 'song.opus', url: '' }], warnings: ['Sin pista de guitarra separada: los fallos no silenciarán la guitarra'], art: null, hasGuitar: true, selected: true, status: 'ready', notesType: 'chart', countoff: 'HiHat01', singer: 'Male' };
          queue.push(it); setTimeout(() => emit('import.item', it), 200); return delay({ added: 1 }, 400);
        }
        case 'import.update': { const it = queue.find(q => q.id === d.id); Object.assign(it, d.fields); return delay(it); }
        case 'import.remove': queue = queue.filter(q => !d.ids.includes(q.id)); return delay(true);
        case 'import.clearDone': queue = queue.filter(q => q.status !== 'done'); return delay(true);
        case 'import.install': {
          const todo = queue.filter(q => d.ids.includes(q.id));
          emit('import.progress', { phase: 'start', count: todo.length });
          let i = 0;
          return new Promise(res => {
            const step = () => {
              if (i >= todo.length) { emit('import.progress', { phase: 'end', installed: todo.length, failed: 0 }); return res({ installed: todo.length, failed: [] }); }
              const it = todo[i];
              ['chart', 'audio', 'install', 'done'].forEach((stage, k) => setTimeout(() => emit('import.progress', { phase: 'song', id: it.id, stage, pct: [.05, .25, .85, 1][k], index: i + 1, count: todo.length, title: it.title }), k * 500));
              setTimeout(() => { it.status = 'done'; emit('import.item', it); i++; step(); }, 2100);
            };
            step();
          });
        }
        case 'input.state': return delay(JSON.parse(JSON.stringify(input)));
        case 'input.style': { const x = input.devices.find(v => v.id === d.id); if (x.passThrough) return Promise.reject(new Error('x360ce pasa este control de Xbox tal cual.')); x.style = d.style; x.controllerType = d.style === 'guitar' ? 7 : 1; return delay(JSON.parse(JSON.stringify(input))); }
        case 'input.keyboard2': Object.assign(input.keyboard2, d.keyboard2, { slot: 3 }); return delay(JSON.parse(JSON.stringify(input)));
        case 'input.remove': input.devices = input.devices.filter(x => x.id !== d.id); return delay(JSON.parse(JSON.stringify(input)));
        case 'input.tuning': return delay(true);
        case 'menu.music': return delay([]);
        case 'graphics.state': return delay(gfxMock);
        case 'graphics.enable': gfxMock.on = true; return delay(gfxMock, 600);
        case 'graphics.disable': gfxMock.on = false; return delay(gfxMock);
        case 'graphics.preset': gfxMock.preset = d.id; return delay({ state: gfxMock, game: { 'Video.MultiSampleType': '8' } });
        case 'graphics.effect': { const e = gfxMock.effects.find(x => x.id === d.id); if (d.on != null) e.on = d.on; if (d.value != null) e.value = d.value; gfxMock.preset = 'custom'; return delay(gfxMock); }
        case 'theme.state': return delay({ current: '', gameRunning: false, themes: [{ id: '', name: 'Guitar Hero III (original)', author: 'Neversoft', kind: 'game', preview: null }, { id: '8bit', name: '8-BIT', author: '3UP', kind: 'textures', preview: null }],
          catalog: [{ id: 'guitarhero2', name: 'Guitar Hero 2', author: 'donnaken15', size: '5.4 MB' }] });
        case 'theme.notes': { const n = []; for (let i = 0; i < 400; i++) n.push([2000 + i * 260, i % 7 === 0 ? 700 : 60, 1 << (i * 3 % 5) | (i % 9 === 0 ? 4 : 0)]); return delay({ notes: n }); }
        case 'input.watch': simWatch = d.on; return delay(true);
        case 'settings.state': return delay(JSON.parse(JSON.stringify(settings)));
        case 'settings.aspyr': Object.assign(settings.aspyr, d.values); return delay(JSON.parse(JSON.stringify(settings)));
        case 'settings.plus': Object.assign(settings.plus, d.values); return delay(JSON.parse(JSON.stringify(settings)));
        case 'plugins.set': settings.plugins.find(p => p.name === d.name).enabled = d.enabled; return delay(JSON.parse(JSON.stringify(settings)));
        case 'backups.list': return delay({ qb: [{ id: '2026-10-27_01-12-03', reason: 'instalar 2 canción(es)', size: 2577000 }, { id: 'original', reason: '', size: 2577000 }], save: [{ id: '2026-10-27_01-12-03', reason: 'instalar 2 canción(es)', size: 2112000 }] });
        case 'game.launch': setTimeout(() => emit('game.started', {}), 100); return delay({ pid: 1 });
        default: return delay(true);
      }
    }
    return { call };
  })();

  return { call, on, drop, native: !!wv };
})();
