/* Trucos: the game's cheat codes drawn as frets, and unlocks the launcher writes into the game's
   scripts (they run every time GH3 loads the save, so the game saves them itself). */
App.register('cheats', (() => {
  const { h, icon } = App;
  // Fret masks in the game's cheat patterns
  const FRETS = [[65536, 'g'], [4096, 'r'], [256, 'y'], [16, 'b'], [1, 'o']];
  const INFO = {
    Cheat_UnlockAll_Everything: ['Desbloquear todo', 'Canciones, guitarras, bajos, personajes, trajes, estilos y videos. Ojo: también pone 5 estrellas y 1.000.000 de puntos a todas las canciones.'],
    Cheat_UnlockAll: ['Desbloquear todas las canciones', 'Quickplay y Bonus, sin tocar tus estrellas.'],
    Cheat_NoFail: ['Sin fallar (No Fail)', 'No pierdes la canción aunque falles. No funciona en Carrera.'],
    Cheat_Hyperspeed: ['Hiper velocidad', 'Las notas bajan más rápido. En el menú eliges el nivel (1 a 5).'],
    Cheat_EasyExpert: ['Experto fácil', 'El doble de margen para acertar cada nota.'],
    Cheat_PrecisionMode: ['Modo precisión', 'La mitad de margen para acertar: más difícil.'],
    Cheat_AirGuitar: ['Guitarra de aire', 'Tu guitarrista toca sin guitarra.'],
    Cheat_PerformanceMode: ['Performance Mode', 'El modo "Performance" del juego original.'],
    Cheat_LargeGems: ['Notas grandes', 'Gemas más grandes en la pista (truco de BetterGH3).'],
    Cheat_BretMichaels: ['Bret Michaels', 'Bret Michaels canta en lugar del cantante de la banda.'],
    Cheat_SuperUser: ['Modo debug', 'Activa el menú de depuración de BetterGH3. Solo si sabes lo que haces.']
  };
  const ORDER = Object.keys(INFO);
  const ON = [['Cheat_NoFail', 'Sin fallar'], ['Cheat_AirGuitar', 'Guitarra de aire'], ['Cheat_LargeGems', 'Notas grandes'], ['Cheat_PerformanceMode', 'Performance Mode'], ['Cheat_BretMichaels', 'Bret Michaels']];
  let root, body, st = null, opts = null, busy = false;

  function mount(r) {
    root = r;
    body = h('div');
    root.append(h('div.room.room-cheats'), h('div.scroll', h('div.page-wrap',
      h('div.head', h('h1', 'Trucos'), h('div.sub', 'Códigos del juego, desbloqueos y tu partida')), body)));
  }

  async function show() {
    try {
      st = await Bridge.call('cheats.state');
      opts = Object.assign({ songs: false, items: false, cheats: false, cash: 0, on: [], hyperspeed: 0, complete: false }, JSON.parse(JSON.stringify(st.options || {})));
      opts.on = opts.on || [];
    } catch (e) { App.fail(e); }
    render();
  }

  function gems(pattern) {
    return h('div.gems', pattern.map((m, i) => h('div.step', { title: 'Paso ' + (i + 1) },
      FRETS.map(([bit, c]) => h('i.' + c + ((m & bit) ? '.on' : ''))))));
  }

  const toggle = (on, fn) => h('label.toggle', h('input', { type: 'checkbox', checked: on, onchange: e => fn(e.target.checked) }), h('i'));
  const field = (label, hint, ctl) => h('div.field', h('div.label', label), h('div.hint', hint || ''), h('div.ctl', ctl));

  async function apply(off) {
    if (busy) return;
    const running = App.state.app && App.state.app.game && App.state.app.game.running;
    if (running) { App.toast('Cierra el juego primero', 'Los desbloqueos se escriben en los scripts del juego.', 'bad'); return; }
    const o = off ? { songs: false, items: false, cheats: false, cash: 0, on: [], hyperspeed: 0, complete: false } : opts;
    busy = true; render();
    try {
      const r = await Bridge.call('cheats.apply', { options: o });
      st.list.applied = r.result.applied;
      st.options = r.options;
      if (off) opts = Object.assign({}, o);
      App.toast(r.result.applied ? 'Desbloqueos aplicados' : 'Desbloqueos quitados',
        r.result.applied ? 'Se aplican cuando GH3 cargue tu partida (al abrir el juego).' + (o.complete ? ' La banda "Complete" estará en Carrera.' : '') : 'Lo que ya se desbloqueó queda en tu partida.', 'ok');
    } catch (e) { App.fail(e); }
    busy = false; render();
  }

  function render() {
    if (!st) { body.replaceChildren(h('div.empty', h('strong', 'Leyendo trucos…'), 'Un momento.')); return; }
    const list = (st.list && st.list.cheats || []).filter(c => INFO[c.var]).sort((a, b) => ORDER.indexOf(a.var) - ORDER.indexOf(b.var));
    const applied = st.list && st.list.applied;
    const timing = opts.on.includes('Cheat_EasyExpert') ? 'easy' : opts.on.includes('Cheat_PrecisionMode') ? 'precision' : 'normal';
    const setOn = (v, on) => { opts.on = opts.on.filter(x => x !== v); if (on) opts.on.push(v); render(); };

    const unlocks = h('div.card',
      h('div', { style: { display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '6px' } },
        h('h2', { style: { margin: 0 } }, 'Desbloqueos automáticos'),
        h('span.chip' + (applied ? '.ok' : ''), applied ? 'Aplicado en el juego' : 'Sin aplicar')),
      h('p.muted', { style: { margin: '0 0 6px', fontSize: '15px' } }, 'Se ejecutan cada vez que GH3 carga tu partida (también en partidas nuevas) y el juego los guarda. Si cambias canciones y GH3 te pide una partida nueva, esto la deja desbloqueada otra vez.'),
      field('Todas las canciones', 'Setlist en Quickplay y todas las Bonus, sin comprarlas. No cambia tus estrellas ni puntajes.', toggle(!!opts.songs, v => { opts.songs = v; render(); })),
      field('Toda la tienda', 'Guitarras, bajos y acabados, personajes secretos (Lou, Slash, Tom Morello…), trajes, estilos y videos.', toggle(!!opts.items, v => { opts.items = v; render(); })),
      field('Todos los trucos disponibles', 'Aparecen listos para encender en Opciones › Trucos, sin escribir códigos.', toggle(!!opts.cheats, v => { opts.cheats = v; render(); })),
      field('Banda "Complete"', 'Una banda de Carrera con todo el juego pasado en las 4 dificultades (solo y cooperativo): todos los niveles, encores y guerras de guitarras, escenarios y canciones bonus. Así puedes repetir cualquier guerra con su intro. Se crea una vez en el primer espacio libre (si los 5 están ocupados, reemplaza la quinta banda).',
        toggle(!!opts.complete, v => { opts.complete = v; render(); })),
      field('Dinero de la banda', 'Si tienes menos, sube a esta cantidad al cargar.', h('select', { onchange: e => { opts.cash = +e.target.value; } },
        [[0, 'No cambiar'], [50000, '$50.000'], [250000, '$250.000'], [999999, '$999.999']].map(([v, l]) => h('option', { value: v, selected: +opts.cash === v }, l)))),
      h('details.more', { open: opts.on.length > 0 || opts.hyperspeed > 0 },
        h('summary', 'Trucos encendidos al abrir el juego'),
        ON.map(([v, l]) => field(l, INFO[v][1], toggle(opts.on.includes(v), on => setOn(v, on)))),
        field('Margen para acertar', 'Experto fácil o Modo precisión (no los dos).', h('div.seg',
          [['normal', 'Normal'], ['easy', 'Experto fácil'], ['precision', 'Precisión']].map(([v, l]) => h('button' + (timing === v ? '.on' : ''), {
            onclick: () => { opts.on = opts.on.filter(x => x !== 'Cheat_EasyExpert' && x !== 'Cheat_PrecisionMode'); if (v === 'easy') opts.on.push('Cheat_EasyExpert'); if (v === 'precision') opts.on.push('Cheat_PrecisionMode'); render(); }
          }, l)))),
        field('Hiper velocidad', 'Nivel con el que arranca.', h('select', { onchange: e => { opts.hyperspeed = +e.target.value; } },
          [[0, 'No cambiar'], [1, '1'], [2, '2'], [3, '3'], [4, '4'], [5, '5']].map(([v, l]) => h('option', { value: v, selected: +opts.hyperspeed === v }, l))))),
      h('div', { style: { display: 'flex', gap: '8px', marginTop: '14px', alignItems: 'center' } },
        h('button.btn.primary', { disabled: busy, onclick: () => apply(false) }, icon('check'), busy ? 'Aplicando…' : 'Aplicar al juego'),
        applied ? h('button.btn.ghost', { disabled: busy, onclick: () => apply(true) }, 'Quitar del juego') : null,
        h('span.faint', { style: { fontSize: '14px', marginLeft: '6px' } }, 'Con el juego cerrado. Antes se respaldan el setlist y tu partida.')));

    const codes = h('div.card',
      h('h2', 'Códigos de trucos'),
      h('p.muted', { style: { margin: '0 0 12px', fontSize: '15px' } }, 'En el juego: Options › Cheats › Enter New Cheat. Toca cada paso con los trastes indicados y rasguea; los pasos con varias notas son acordes.'),
      h('div.codes', list.map(c => h('div.code',
        h('div.code-head', h('b', INFO[c.var][0]), h('small', c.text)),
        h('p', INFO[c.var][1]),
        gems(c.pattern)))));

    const save = h('div.card',
      h('h2', 'Tu partida'),
      h('p.muted', { style: { margin: '0 0 10px', fontSize: '15px' } }, 'GH3 asocia la partida a las canciones de sus listas y al orden en que están. Al agregar, borrar o reordenar canciones, el juego dice que la partida está dañada y te pide crear una nueva. El launcher la respalda antes de cada cambio, y los desbloqueos de arriba la dejan como estaba en segundos.'),
      h('div', { style: { display: 'flex', gap: '8px', flexWrap: 'wrap' } },
        h('button.btn.sm', { onclick: async () => { try { await Bridge.call('backups.saveNow'); App.toast('Partida respaldada', '', 'ok'); } catch (e) { App.fail(e); } } }, icon('plus'), 'Respaldar ahora'),
        h('button.btn.sm', { onclick: () => App.go('tools') }, 'Ver y restaurar respaldos'),
        h('button.btn.sm.ghost', { onclick: () => Bridge.call('tools.run', { id: 'folder:save' }).catch(App.fail) }, icon('folder'), 'Carpeta de partidas')));

    body.replaceChildren(h('div.opt-grid', h('div.opt-col', unlocks, save), h('div.opt-col', codes)));
  }

  return { mount, show };
})());
