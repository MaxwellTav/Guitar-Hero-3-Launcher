/* Controles. x360ce sets up the pads: its own app writes x360ce.ini (which device is PAD 1-4 and
   its buttons). This screen shows them as GH3 will get them, live, and sets how each one is played
   (guitar: you strum; controller: every fret strums by itself, done by the InputPlus plugin).
   Keyboard 1 is GH3's own; keyboard 2 is a virtual guitar for a second player, on a PAD x360ce
   leaves free. The host streams each connected device's raw DirectInput state (input.state, by
   instance) while this screen is open. */
App.register('controls', (() => {
  const { h, icon } = App;
  const ACTIONS = [
    { id: 'green', label: 'Verde', c: 'var(--g)', t: 'A' },
    { id: 'red', label: 'Rojo', c: 'var(--r)', t: 'B' },
    { id: 'yellow', label: 'Amarillo', c: 'var(--y)', t: 'Y' },
    { id: 'blue', label: 'Azul', c: 'var(--b)', t: 'X' },
    { id: 'orange', label: 'Naranja', c: 'var(--o)', t: 'Left Shoulder' },
    { id: 'strumUp', label: 'Rasgueo arriba', t: 'D-pad Up', d: 0 },
    { id: 'strumDown', label: 'Rasgueo abajo', t: 'D-pad Down', d: 2 },
    { id: 'left', label: 'Menú izquierda', t: 'D-pad Left', d: 3 },
    { id: 'right', label: 'Menú derecha', t: 'D-pad Right', d: 1 },
    { id: 'start', label: 'Start / pausa', t: 'Start' },
    { id: 'select', label: 'Star Power', t: 'Back' },
    { id: 'whammy', label: 'Whammy', ax: 'Right Analog X' },
    { id: 'tilt', label: 'Inclinar (Star Power)', ax: 'Right Analog Y' }
  ];
  const DPAD = ['D-pad Up', 'D-pad Right', 'D-pad Down', 'D-pad Left'];
  const DZ = { A: 'A DeadZone', B: 'B DeadZone', X: 'X DeadZone', Y: 'Y DeadZone', 'Left Shoulder': 'Left Shoulder DeadZone', 'Right Shoulder': 'Right Shoulder DeadZone', 'Left Trigger': 'Left Trigger DeadZone', 'Right Trigger': 'Right Trigger DeadZone', Back: 'Back DeadZone', Start: 'Start DeadZone', 'D-pad Up': 'AxisToDPadUpDeadZone', 'D-pad Down': 'AxisToDPadDownDeadZone', 'D-pad Left': 'AxisToDPadLeftDeadZone', 'D-pad Right': 'AxisToDPadRightDeadZone' };
  const AXN = ['X', 'Y', 'Z', 'Rx', 'Ry', 'Rz', 'Slider 1', 'Slider 2'];
  const ARROWS = ['↑', '→', '↓', '←'];
  const STYLES = { guitar: ['guitar', 'Guitarra', 'Rasgueas tú'], controller: ['pad', 'Control', 'Sin rasgueo (control de Xbox)'] };
  // the five frets as x360ce keys: a guitar's layout, and GH3's layout for a plain gamepad
  const FRETS = { guitar: ['A', 'B', 'Y', 'X', 'Left Shoulder'], controller: ['Left Trigger', 'Left Shoulder', 'Right Shoulder', 'Right Trigger', 'A'] };
  const actionsFor = style => ACTIONS.map((a, i) => i < 5 ? Object.assign({}, a, { t: FRETS[style][i] }) : a);
  // a real Xbox pad that x360ce passes through, as DirectInput shows it (triggers share the Z axis)
  const XBOX = { A: '1', B: '2', X: '3', Y: '4', 'Left Shoulder': '5', 'Right Shoulder': '6', Back: '7', Start: '8', 'D-pad POV': '1', 'Left Trigger': 'x3', 'Right Trigger': 'x-3', 'Left Trigger DeadZone': '8000', 'Right Trigger DeadZone': '8000', 'Right Analog X': '4', 'Right Analog Y': '-5' };
  // keyboard 2's defaults (as the host's): letters for the frets, the numeric keypad for the rest
  const KB2_KEYS = { green: [65], red: [83], yellow: [68], blue: [70], orange: [71], strumUp: [104], strumDown: [98, 101], left: [100], right: [102], start: [107], select: [96], whammy: [105], tilt: [110] };

  let root, listEl, edEl, topEl;
  let input = null;          // the host's input state (x360ce, devices, keyboard2, whammy ...)
  let sel = null;            // 'kb1', 'kb2' or a device id
  const live = {};           // device instance -> last raw state
  const keysDown = new Set();
  let listening = null;      // {action, timer}: keyboard 2 waiting for a key
  let vizRefs = null, rawRefs = null;

  /* ---- keyboard: e.code -> Windows virtual-key code ---- */
  const VK = {};
  for (let i = 0; i < 26; i++) VK['Key' + String.fromCharCode(65 + i)] = 65 + i;
  for (let i = 0; i < 10; i++) { VK['Digit' + i] = 48 + i; VK['Numpad' + i] = 96 + i; }
  for (let i = 1; i <= 24; i++) VK['F' + i] = 111 + i;
  Object.assign(VK, { ArrowUp: 38, ArrowDown: 40, ArrowLeft: 37, ArrowRight: 39, Space: 32, Enter: 13, NumpadEnter: 13, Tab: 9, Backspace: 8, ShiftLeft: 160, ShiftRight: 161, ControlLeft: 162, ControlRight: 163, AltLeft: 164, AltRight: 165, MetaLeft: 91, MetaRight: 92, ContextMenu: 93, Insert: 45, Delete: 46, Home: 36, End: 35, PageUp: 33, PageDown: 34, Minus: 189, Equal: 187, BracketLeft: 219, BracketRight: 221, Backslash: 220, Semicolon: 186, Quote: 222, Backquote: 192, Comma: 188, Period: 190, Slash: 191, IntlBackslash: 226, CapsLock: 20, NumpadMultiply: 106, NumpadAdd: 107, NumpadSubtract: 109, NumpadDecimal: 110, NumpadDivide: 111 });
  const VKNAME = { 38: 'Flecha ↑', 40: 'Flecha ↓', 37: 'Flecha ←', 39: 'Flecha →', 32: 'Espacio', 13: 'Enter', 9: 'Tab', 8: 'Retroceso', 160: 'Shift izq.', 161: 'Shift der.', 162: 'Ctrl izq.', 163: 'Ctrl der.', 164: 'Alt', 165: 'Alt Gr', 91: 'Win', 93: 'Menú', 45: 'Insert', 46: 'Supr', 36: 'Inicio', 35: 'Fin', 33: 'RePág', 34: 'AvPág', 20: 'Bloq Mayús', 106: 'Num *', 107: 'Num +', 109: 'Num -', 110: 'Num .', 111: 'Num /', 192: 'Ñ', 186: '´', 222: '{', 219: "'", 221: '¡', 220: 'º', 191: '}', 187: '+', 189: '-', 188: ',', 190: '.', 226: '<' };
  const vkName = (vk, labels) => (labels && labels[vk]) || VKNAME[vk] || (vk >= 48 && vk <= 57 ? String.fromCharCode(vk) : vk >= 65 && vk <= 90 ? String.fromCharCode(vk) : vk >= 96 && vk <= 105 ? 'Num ' + (vk - 96) : vk >= 112 && vk <= 135 ? 'F' + (vk - 111) : 'Tecla ' + vk);

  function mount(r) {
    root = r;
    topEl = h('div');
    listEl = h('div.dev-list');
    edEl = h('div');
    root.append(h('div.room.room-controls'), h('div.scroll', h('div.page-wrap',
      h('div.head', h('h1', 'Controles'), h('div.sub', 'x360ce asigna los botones; aquí eliges cómo se toca cada control y ves lo que recibe GH3')),
      topEl,
      h('div.ctl-layout', { style: { marginTop: '16px' } }, h('div', h('div.section-title', 'Dispositivos'), listEl), edEl))));
    Bridge.on('input.state', onLive);
    App.onState('input', s => { input = s; renderAll(); });
    window.addEventListener('keydown', onKeyDown, true);
    window.addEventListener('keyup', e => { const vk = VK[e.code]; if (vk) keysDown.delete(vk); if (App.current === 'controls') renderViz(); });
    // back from x360ce's window: read what it saved
    window.addEventListener('blur', () => keysDown.clear());
    window.addEventListener('focus', () => { if (App.current === 'controls' && !listening) App.refreshInput(); });
  }

  async function show() {
    Bridge.call('input.watch', { on: true });
    input = await App.refreshInput();
    renderAll();
  }
  function hide() { Bridge.call('input.watch', { on: false }); cancelListen(); }

  /* ---- saving: every change goes to the host at once, which rewrites InputPlus.ini ---- */
  const flash = () => { const f = root && root.querySelector('.saved-flash'); if (f) { f.classList.add('on'); clearTimeout(f._t); f._t = setTimeout(() => f.classList.remove('on'), 1400); } };
  function took(s) { App.state.input = input = s; App.summarizeInput(); App.emit('input', s); }

  async function setStyle(d, style) {
    try { took(await Bridge.call('input.style', { id: d.id, style })); flash(); } catch (e) { App.fail(e); }
  }
  async function saveKb() {
    try { took(await Bridge.call('input.keyboard2', { keyboard2: input.keyboard2 })); flash(); } catch (e) { App.fail(e); }
  }
  const saveTuning = App.debounce(async () => {
    try { await Bridge.call('input.tuning', { whammy: input.whammy, chordWindowMs: input.chordWindowMs, pulseMs: input.pulseMs }); flash(); } catch (e) { App.fail(e); }
  }, 350);

  async function remove(d) {
    const ok = await App.confirm('¿Quitar este control de x360ce?',
      `Se borra de x360ce.ini${d.pad ? ' y deja libre el PAD ' + d.pad : ''}. Antes se guarda una copia en Launcher\\backups. Para volver a usarlo, configúralo otra vez en x360ce.`, 'Quitar', 'danger');
    if (!ok) return;
    try { sel = null; took(await Bridge.call('input.remove', { id: d.id })); App.toast('Control quitado de x360ce', d.name); } catch (e) { App.fail(e); }
  }
  const openX360 = () => Bridge.call('tools.run', { id: 'x360ce' }).catch(App.fail);

  /* ---- layout ---- */
  const sorted = () => input.devices.slice().sort((a, b) => ((a.pad || 9) - (b.pad || 9)) || (b.connected - a.connected) || (b.configured - a.configured) || a.name.localeCompare(b.name));
  const device = () => input.devices.find(d => d.id === sel);
  const styleOf = s => s === 'controller' ? 'controller' : 'guitar';

  function renderAll() {
    if (!root || !input) return;
    if (!sel || (sel !== 'kb1' && sel !== 'kb2' && !device())) {
      const d = sorted().find(x => x.pad && x.connected) || sorted()[0];
      sel = d ? d.id : 'kb1';
    }
    renderTop(); renderList(); renderEditor();
  }

  function renderTop() {
    const x = input.x360ce || {};
    topEl.replaceChildren(h('div.card',
      h('div.x360-row',
        h('div', h('h2', { style: { margin: '0 0 4px' } }, 'x360ce'),
          h('p.muted', { style: { margin: 0 } }, 'Los botones de cada control y quién es PAD 1, 2, 3 o 4 se configuran en x360ce; el launcher no los cambia. Aquí eliges si cada uno se toca como guitarra o como control, y ves lo que recibe GH3.')),
        h('div.x360-acts',
          h('button.btn', { onclick: openX360, disabled: !x.app }, icon('controls'), 'Abrir x360ce'),
          h('button.btn.ghost', { onclick: () => App.refreshInput(), title: 'Volver a leer x360ce.ini' }, icon('refresh')))),
      h('div.chips',
        x.app ? h('span.chip.ok', icon('check'), 'x360ce') : h('span.chip.bad', 'x360ce no está instalado'),
        x.dll === 'ok' ? h('span.chip.ok', icon('check'), 'xinput1_3.dll') : h('span.chip.bad', x.dll === 'missing' ? 'Falta xinput1_3.dll' : 'xinput1_3.dll no es de x360ce'),
        x.running ? h('span.chip.warn', 'x360ce abierto') : null,
        input.gameRunning ? h('span.chip.warn', 'GH3 abierto: lo que cambies en x360ce cuenta al reiniciar el juego') : null)));
  }

  function renderList() {
    const kb2 = input.keyboard2;
    const item = (id, ic, name, sub, pad, off, dot) => h('button.dev' + (sel === id ? '.on' : '') + (off ? '.off' : ''), { onclick: () => select(id) },
      icon(ic), h('div', h('b', { title: name }, name), h('small', dot != null ? h('span.live' + (dot ? '.yes' : '')) : null, sub)),
      pad ? h('span.pad' + (pad === 1 ? '.p1' : ''), 'PAD ' + pad) : null);
    const items = [
      item('kb1', 'keyboard', 'Teclado 1', 'Del juego'),
      item('kb2', 'keyboard', 'Teclado 2', !kb2.enabled ? 'Desactivado' : kb2.slot ? STYLES[styleOf(kb2.style)][1] : 'Sin PAD libre', kb2.enabled ? kb2.slot : 0, !kb2.enabled)
    ];
    sorted().forEach(d => items.push(item(d.id, STYLES[styleOf(d.style)][0], d.name,
      !d.configured ? 'Sin configurar en x360ce' : !d.connected ? 'No conectado' : !d.pad ? 'Sin PAD en x360ce' : STYLES[styleOf(d.style)][1],
      d.configured ? d.pad : 0, !d.connected || !d.configured || !d.pad, d.connected)));
    if (!input.devices.length) items.push(h('p.faint', { style: { margin: '6px 4px' } }, 'Conecta tu guitarra o control: aparecerá aquí solo.'));
    listEl.replaceChildren(...items);
  }

  function select(id) { sel = id; cancelListen(); renderList(); renderEditor(); }

  function renderEditor() {
    vizRefs = rawRefs = null;
    if (sel === 'kb1') return renderKb1();
    if (sel === 'kb2') return renderKb2();
    const d = device();
    if (d) renderDevice(d);
  }

  /* how it's played: the choice that matters here (set = null: fixed, with the reason) */
  function styleField(style, set, why) {
    const opt = id => {
      const [ic, title, note] = STYLES[id];
      return h('button.style-opt' + (style === id ? '.on' : ''), { disabled: !set && style !== id, onclick: () => { if (set && style !== id) set(id); } }, icon(ic), h('span', h('b', title), h('small', note)));
    };
    return h('div.style-field', h('div.label', 'Cómo se toca'),
      h('div.hint', why || 'Guitarra: GH3 lo recibe como guitarra y rasgueas tú. Control: lo recibe como control de Xbox y toca sin rasguear (los trastes pasan a LT, LB, RB, RT y A).'),
      h('div.style-pick', opt('guitar'), opt('controller')));
  }

  function renderDevice(d) {
    const style = styleOf(d.style);
    const mapped = d.configured && !d.passThrough;
    edEl.replaceChildren(...[
      h('div.card',
        h('div.ed-head', h('h3', d.name),
          h('span.chip' + (d.connected ? '.ok' : ''), d.connected ? 'Conectado' : 'Sin conectar'),
          d.configured ? h('span.chip' + (d.pad ? '.ok' : '.warn'), d.pad ? 'PAD ' + d.pad : 'Sin PAD') : h('span.chip.warn', 'Sin configurar'),
          d.configured ? h('span.chip', d.passThrough ? 'x360ce: directo' : d.controllerType === 7 ? 'x360ce: guitarra' : 'x360ce: mapeado') : null,
          h('span.saved-flash', 'Guardado ✓')),
        !d.configured ? h('div.notice', { style: { marginTop: '12px' } }, icon('warn'), h('div', 'x360ce todavía no tiene este control, así que GH3 no lo verá. Ábrelo en x360ce, asígnale un PAD y guarda.')) :
          !d.pad ? h('div.notice', { style: { marginTop: '12px' } }, icon('warn'), h('div', 'x360ce lo tiene, pero sin PAD: GH3 no lo verá hasta que se lo asignes en x360ce.')) : null,
        !d.configured ? null : d.passThrough
          ? styleField(style, null, 'x360ce pasa este control de Xbox tal cual: GH3 siempre lo recibe como control y toca sin rasguear.')
          : styleField(style, v => setStyle(d, v)),
        h('div.ed-acts',
          h('button.btn.sm', { onclick: openX360, disabled: !input.x360ce.app }, icon('controls'), d.configured ? 'Cambiar botones en x360ce' : 'Configurar en x360ce'),
          d.configured ? h('button.btn.sm.danger', { onclick: () => remove(d) }, icon('trash'), 'Quitar de x360ce') : null)),
      mapped ? h('div.card', h('h2', 'Así lo verá GH3'), viz(style), h('div.binds', actionsFor(style).map(a => bindRow(d.keys || {}, a)))) : null,
      d.configured && d.passThrough ? h('div.card', h('h2', 'Así lo verá GH3'), viz('controller'),
        h('p.faint', { style: { margin: '0 0 8px' } }, 'Con el esquema de control de GH3: LT verde, LB rojo, RB amarillo, RT azul, A naranja.'),
        h('div.binds', actionsFor('controller').map(a => bindRow(XBOX, a)))) : null,
      d.connected ? rawCard(d) : null,
      style === 'guitar' ? whammyCard() : null
    ].filter(Boolean));
    renderViz(); renderRaw();
  }

  function renderKb1() {
    edEl.replaceChildren(h('div.card',
      h('div.ed-head', h('h3', 'Teclado 1'), h('span.chip.ok', 'Nativo de GH3')),
      h('p', { style: { margin: '12px 0 6px' } }, 'Es el teclado propio de Guitar Hero III: el juego lo lee directamente. Sus teclas se cambian en el juego: Options › Controller Settings › Configure Keyboard.'),
      h('p.muted', { style: { margin: '0 0 6px' } }, 'El juego solo deja cambiar: los 5 trastes, Star Power, Whammy, rasgueo arriba y rasgueo abajo. Pausa y menú izquierda/derecha son fijas.'),
      h('p.muted', { style: { margin: '0 0 12px' } }, 'Para que otra persona toque también con teclado, activa el Teclado 2.'),
      h('button.btn.sm', { onclick: () => select('kb2') }, icon('keyboard'), 'Ir a Teclado 2')));
  }

  function renderKb2() {
    const kb = input.keyboard2;
    const style = styleOf(kb.style);
    const set = (k, v) => { kb[k] = v; saveKb(); };
    edEl.replaceChildren(...[
      h('div.card',
        h('div.ed-head', h('h3', 'Teclado 2 (emulado)'),
          h('span.chip' + (kb.enabled ? '.ok' : ''), kb.enabled ? 'Activado' : 'Desactivado'),
          kb.slot ? h('span.chip', 'PAD ' + kb.slot) : h('span.chip.warn', 'Sin PAD libre'),
          h('span.saved-flash', 'Guardado ✓')),
        h('p.muted', { style: { margin: '12px 0 4px' } }, 'Para un segundo jugador con teclado. El plugin InputPlus crea un control virtual con estas teclas (guitarra o control de Xbox, según elijas abajo), en un PAD que x360ce deja libre (x360ce no lee teclados).'),
        h('div.field', h('div.label', 'Activar'), h('div.hint', kb.slot ? `Tocará como PAD ${kb.slot}.` : 'x360ce usa los 4 PAD: quita un control para dejar uno libre.'), h('div.ctl', toggle(!!kb.enabled, v => set('enabled', v)))),
        h('div.field', h('div.label', 'Solo con GH3 en primer plano'), h('div.hint', 'Ignora el teclado cuando estás en otra ventana.'), h('div.ctl', toggle(kb.focusOnly !== false, v => set('focusOnly', v)))),
        styleField(style, v => set('style', v))),
      h('div.card', h('h2', 'Teclas'), viz(style),
        h('p.faint', { style: { margin: '0 0 8px' } }, 'Usa teclas que el Teclado 1 no use: el juego deja de ver las de aquí, para que ninguna haga dos cosas. Con dos teclados físicos pasa igual, porque Windows los junta en uno. El teclado numérico funciona con Bloq Num activado.'),
        h('div.binds', ACTIONS.map(a => keyRow(a))),
        h('div', { style: { marginTop: '10px' } }, h('button.btn.sm', { onclick: resetKeys }, icon('refresh'), 'Teclas predeterminadas'))),
      style === 'guitar' ? whammyCard() : null
    ].filter(Boolean));
    renderViz();
  }

  function toggle(on, fn) {
    return h('label.toggle', h('input', { type: 'checkbox', checked: on, onchange: e => fn(e.target.checked) }), h('i'));
  }

  /* ---- a pad's buttons, read from its x360ce section ---- */
  const isEmpty = v => v == null || v === '' || v === '0';
  function bindingOf(keys, a) {
    if (a.ax) {
      if (!isEmpty(keys[a.ax])) return { v: keys[a.ax], axis: true };
      if (!isEmpty(keys[a.ax + '+ Button'])) return { v: keys[a.ax + '+ Button'] };
      return null;
    }
    if (!isEmpty(keys[a.t])) return { v: keys[a.t] };
    const pov = parseInt(keys['D-pad POV']);
    if (a.d != null && pov > 0) return { v: 'd' + ((pov - 1) * 4 + a.d + 1), viaPov: true };
    return null;
  }
  function label(b) {
    if (!b) return null;
    const v = String(b.v);
    let m;
    if ((m = v.match(/^d(\d+)$/))) { const n = +m[1] - 1; return `POV ${Math.floor(n / 4) + 1} ${ARROWS[n % 4]}`; }
    if ((m = v.match(/^([xh])(-?)(\d+)$/))) return `Eje ${m[1] === 'h' ? AXN[5 + +m[3]] : AXN[+m[3] - 1]} ${m[2] ? '−' : '+'}`;
    if ((m = v.match(/^([as])(-?)(\d+)$/))) return `Eje ${m[1] === 's' ? AXN[5 + +m[3]] : AXN[+m[3] - 1]}${m[2] ? ' (inv.)' : ''}`;
    if ((m = v.match(/^(-?)(\d+)$/))) return b.axis ? `Eje ${AXN[+m[2] - 1] || m[2]}${m[1] ? ' (inv.)' : ''}` : `Botón ${m[2]}`;
    return v;
  }
  function bindRow(keys, a) {
    const b = bindingOf(keys, a);
    return h('div.bind.ro', { 'data-action': a.id },
      h('span.sw' + (a.c ? '' : '.flat'), { style: { '--c': a.c || '#6c5f50' } }),
      h('span.name', a.label),
      h('div.vals', b ? h('span.key.ro', { title: b.viaPov ? 'Parte del D-pad (POV) completo' : '' }, label(b)) : h('span.key.none', 'Sin asignar')));
  }

  function onLive(list) {
    for (const s of list) live[s.id] = s;
    if (App.current === 'controls') { renderViz(); renderRaw(); }
  }

  /* ---- keyboard 2's keys ---- */
  function keyRow(a) {
    const kb = input.keyboard2;
    kb.keys = kb.keys || {};
    const list = (kb.keys[a.id] = kb.keys[a.id] || []);
    const lst = listening && listening.action === a.id;
    return h('div.bind' + (lst ? '.listening' : ''), { 'data-action': a.id },
      h('span.sw' + (a.c ? '' : '.flat'), { style: { '--c': a.c || '#6c5f50' } }),
      h('span.name', a.label),
      h('div.vals', list.map(vk => h('span.key' + (keysDown.has(vk) ? '.live' : ''), { 'data-vk': vk }, vkName(vk, kb.labels),
        h('button', { title: 'Quitar', onclick: () => { kb.keys[a.id] = list.filter(x => x !== vk); saveKb(); } }, icon('x')))),
        !list.length && !lst ? h('span.key.none', 'Sin asignar') : null),
      h('button.btn.sm', { onclick: () => lst ? cancelListen() : listen(a.id), disabled: !lst && list.length >= 8 }, lst ? 'Cancelar' : icon('plus'), lst ? null : 'Tecla'));
  }

  function listen(action) {
    cancelListen();
    listening = { action, timer: setTimeout(() => { cancelListen(); App.toast('No se detectó nada', 'Vuelve a intentarlo y presiona la tecla.'); }, 9000) };
    renderKb2();
  }
  function cancelListen() {
    if (!listening) return;
    clearTimeout(listening.timer); listening = null;
    if (root && sel === 'kb2') renderKb2();
  }

  function onKeyDown(e) {
    if (App.current !== 'controls') return;
    const vk = VK[e.code];
    if (listening) {
      e.preventDefault(); e.stopPropagation();
      if (e.code === 'Escape') { cancelListen(); return; }
      if (!vk) { App.toast('Esa tecla no se puede usar', e.code); return; }
      const kb = input.keyboard2, a = listening.action;
      Object.keys(kb.keys).forEach(k => { if (k !== a && kb.keys[k].includes(vk)) App.toast('Tecla repetida', `${vkName(vk, kb.labels)} también está en "${ACTIONS.find(x => x.id === k).label}".`); });
      if (!kb.keys[a].includes(vk)) kb.keys[a].push(vk);
      kb.labels = kb.labels || {};
      // the keypad's digits keep their "Num n" name
      if (!e.code.startsWith('Numpad') && e.key && e.key.length === 1 && e.key.trim()) kb.labels[vk] = e.key.toUpperCase();
      clearTimeout(listening.timer); listening = null;
      saveKb();
      return;
    }
    if (vk && !(e.target.closest && e.target.closest('input, select, textarea'))) { keysDown.add(vk); renderViz(); }
  }

  function resetKeys() {
    input.keyboard2.keys = JSON.parse(JSON.stringify(KB2_KEYS));
    input.keyboard2.labels = {};
    saveKb();
  }

  /* ---- what GH3 gets: x360ce's rules applied to the raw state ---- */
  function povDirs(deg) {
    if (deg < 0) return [false, false, false, false];
    const y = Math.floor((2250 + deg) / 4500) % 8;
    return [y <= 1 || y === 7, y >= 1 && y <= 3, y >= 3 && y <= 5, y >= 5 && y <= 7];
  }
  function pressed(keys, target, s) {
    const v = String(keys[target] || '');
    let m;
    if (isEmpty(v)) {
      const i = DPAD.indexOf(target), pov = parseInt(keys['D-pad POV']);
      return i >= 0 && pov > 0 ? povDirs(s.povs[pov - 1])[i] : false;
    }
    if ((m = v.match(/^d(\d+)$/))) { const n = +m[1] - 1; return povDirs(s.povs[Math.floor(n / 4)])[n % 4]; }
    if ((m = v.match(/^([xh])(-?)(\d+)$/))) { const val = s.axes[m[1] === 'h' ? 5 + +m[3] : +m[3] - 1] || 0; const dz = parseInt(keys[DZ[target]]) || 0; return (m[2] ? -1 - val : val) > dz; }
    if ((m = v.match(/^(\d+)$/))) return !!s.buttons[+m[1] - 1];
    return false;
  }
  function axisLevel(keys, ax, s) {
    const v = String(keys[ax] || '');
    let m;
    if (!isEmpty(v) && (m = v.match(/^([as]?)(-?)(\d+)$/))) {
      const idx = m[1] === 's' ? 5 + +m[3] : +m[3] - 1;
      let val = s.axes[idx] || 0; if (m[2]) val = -1 - val;
      return (val + 32768) / 65535;
    }
    const b = keys[ax + '+ Button'];
    if (!isEmpty(b)) return pressed({ x: b }, 'x', s) ? 1 : 0;
    return 0;
  }

  function viz(style) {
    const frets = ['g', 'r', 'y', 'b', 'o'].map(c => h('div.fret.' + c));
    const up = h('i', 'ARRIBA'), down = h('i', 'ABAJO');
    const wbar = h('i'), tilt = h('span.pill', 'Inclinar'), sp = h('span.pill', 'Star Power'), start = h('span.pill', 'Start');
    const auto = style === 'controller' ? h('span.pill', 'Sin rasgueo') : null;
    vizRefs = { frets, up, down, wbar, tilt, sp, start, auto };
    return h('div.viz', h('div.frets', frets),
      h('div.viz-side',
        h('div.strum', up, down),
        h('div.meter', h('span', 'Whammy'), h('div.bar', wbar)),
        h('div', { style: { display: 'flex', flexWrap: 'wrap', gap: '6px', gridColumn: '1 / -1' } }, sp, tilt, start, auto)));
  }

  function renderViz() {
    if (!vizRefs) return;
    let st = { frets: [0, 0, 0, 0, 0], w: 0 };
    if (sel === 'kb2') {
      const k = input.keyboard2.keys || {};
      const on = id => (k[id] || []).some(vk => keysDown.has(vk));
      st = { frets: ['green', 'red', 'yellow', 'blue', 'orange'].map(on), up: on('strumUp'), down: on('strumDown'), left: on('left'), right: on('right'), w: on('whammy') ? 1 : 0, tilt: on('tilt'), sp: on('select'), start: on('start') };
    } else {
      const d = device(), s = d && live[d.instance];
      if (s) {
        const keys = d.passThrough ? XBOX : d.keys || {};
        st = {
          frets: FRETS[d.passThrough ? 'controller' : styleOf(d.style)].map(t => pressed(keys, t, s)), up: pressed(keys, 'D-pad Up', s), down: pressed(keys, 'D-pad Down', s),
          left: pressed(keys, 'D-pad Left', s), right: pressed(keys, 'D-pad Right', s), w: axisLevel(keys, 'Right Analog X', s),
          tilt: axisLevel(keys, 'Right Analog Y', s) > 0.75, sp: pressed(keys, 'Back', s), start: pressed(keys, 'Start', s)
        };
      }
    }
    const v = vizRefs;
    v.frets.forEach((f, i) => f.classList.toggle('on', !!st.frets[i]));
    v.up.classList.toggle('on', !!st.up);
    v.down.classList.toggle('on', !!st.down);
    v.wbar.style.width = Math.round((st.w || 0) * 100) + '%';
    v.tilt.classList.toggle('on', !!st.tilt);
    v.sp.classList.toggle('on', !!st.sp);
    v.start.classList.toggle('on', !!st.start);
    if (v.auto) v.auto.classList.toggle('on', st.frets.some(Boolean));   // a fret strums by itself
    const act = { green: st.frets[0], red: st.frets[1], yellow: st.frets[2], blue: st.frets[3], orange: st.frets[4], strumUp: st.up, strumDown: st.down, left: st.left, right: st.right, start: st.start, select: st.sp, whammy: st.w > 0.6, tilt: st.tilt };
    edEl.querySelectorAll('.bind[data-action]').forEach(r => r.classList.toggle('live', !!act[r.dataset.action]));
    if (sel === 'kb2') edEl.querySelectorAll('.bind .key[data-vk]').forEach(k => k.classList.toggle('live', keysDown.has(+k.dataset.vk)));
  }

  /* ---- raw inputs ---- */
  function rawCard(d) {
    const btns = h('div.raw'), axes = h('div.axes'), povs = h('div.muted', { style: { marginTop: '8px', fontSize: '14px' } });
    rawRefs = { id: d.instance, btns, axes, povs, n: -1 };
    return h('div.card', h('details.more', { open: !d.configured || d.passThrough }, h('summary', 'Entradas crudas (DirectInput)'), btns, axes, povs));
  }
  function renderRaw() {
    if (!rawRefs) return;
    const s = live[rawRefs.id];
    if (!s) { rawRefs.btns.replaceChildren(h('span.faint', 'Esperando datos…')); return; }
    if (rawRefs.n !== s.buttons.length) {
      rawRefs.n = s.buttons.length;
      rawRefs.btns.replaceChildren(...s.buttons.map((_, i) => h('span.b', i + 1)));
      rawRefs.axes.replaceChildren(...AXN.map(n => h('div', n, h('div.track', h('i')))));
    }
    [...rawRefs.btns.children].forEach((b, i) => b.classList.toggle('on', !!s.buttons[i]));
    [...rawRefs.axes.children].forEach((a, i) => { a.querySelector('i').style.left = ((s.axes[i] + 32768) / 65535 * 100) + '%'; });
    rawRefs.povs.textContent = 'POV: ' + s.povs.map((p, i) => `${i + 1}: ${p < 0 ? '—' : p / 100 + '°'}`).join('  ');
  }

  /* ---- whammy and auto-strum timing (InputPlus) ---- */
  function whammyCard() {
    const w = input.whammy;
    const tg = (k, label, hint) => h('div.field', h('div.label', label), h('div.hint', hint), h('div.ctl', toggle(!!w[k], v => { w[k] = v ? 1 : 0; saveTuning(); })));
    const rg = (k, label, hint, min, max, step, fmt = v => v) => {
      const out = h('output', fmt(w[k]));
      return h('div.field', h('div.label', label), h('div.hint', hint), h('div.ctl.range',
        h('input', { type: 'range', min, max, step, value: w[k], oninput: e => { w[k] = +e.target.value; out.textContent = fmt(w[k]); saveTuning(); } }), out));
    };
    const pct = v => Math.round(v * 100) + '%';
    return h('div.card', h('h2', 'Whammy (guitarras y teclado)'),
      h('p.faint', { style: { margin: '-4px 0 6px' } }, 'Se guarda solo y el juego lo aplica en ~1 s, aunque esté abierto.'),
      tg('Enabled', 'Mejoras de whammy', 'Si lo apagas, GH3 recibe el whammy tal cual.'),
      tg('ParkFix', 'Corregir whammy "pegado"', 'Tu guitarra Wii manda 50% tras 1–2 s quieta; esto lo trata como barra suelta.'),
      rg('RestDeadzone', 'Zona muerta', 'Recorrido inicial que no cuenta.', 0, 0.5, 0.01, pct),
      rg('Sensitivity', 'Sensibilidad', '1.5 = llegas al máximo con 2/3 del recorrido.', 0.5, 3, 0.05, v => (+v).toFixed(2)),
      tg('Amplify', 'Exagerar el movimiento', 'Cada subida y bajada de la barra se amplifica.'),
      rg('MotionGain', 'Cuánto exagera', '', 1, 6, 0.1, v => (+v).toFixed(1) + '×'),
      tg('Turbo', 'Turbo (oscilación automática)', 'Mientras la barra esté presionada, sube y baja sola: notas largas más "vivas".'),
      rg('TurboHz', 'Velocidad del turbo', 'Oscilaciones por segundo.', 1, 20, 0.5, v => (+v).toFixed(1) + ' Hz'),
      rg('TurboDepth', 'Profundidad del turbo', '', 0, 1, 0.05, pct));
  }
  return { mount, show, hide };
})());
