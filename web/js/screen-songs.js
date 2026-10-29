/* Canciones: the game's setlist the way GH3 shows it, three papers on the wooden table (Setlist,
   Bonus, Download), with the song's details and its preview on the right. The Download paper can
   be edited: drag songs by their grip (several at once when ticked), sort it, delete from it. */
App.register('songs', (() => {
  const { h, icon } = App;
  const PAGES = [
    { id: 'setlist', label: 'Setlist', cls: 'p1' },
    { id: 'bonus', label: 'Bonus', cls: 'p2' },
    { id: 'download', label: 'Download', cls: 'p3' }
  ];
  const VENUES = ['backyard', 'bar', 'videoshoot', 'odeon', 'prison', 'desert', 'megadome', 'hell'];
  let root, tabsEl, scrollEl, npEl, barEl, searchEl;
  let tab = 'setlist', query = '', selected = null, mode = 'preview';
  let editing = false, layout = null, dirty = false;   // layout: the Download list being edited, [{title, songs}]
  let singers = {};                                     // singer changes made while editing: {id: male|female|bret}
  const checked = new Set();
  const DEFAULT_TIER = 'Lost and Cursed Songs';

  function mount(r) {
    root = r;
    try { tab = localStorage.getItem('songs.tab') || tab; } catch (e) { }
    searchEl = h('input', { type: 'search', placeholder: 'Buscar canción o artista…', oninput: App.debounce(e => { query = e.target.value.trim().toLowerCase(); renderAll(); }, 120) });
    barEl = h('div.songs-bar');
    tabsEl = h('div.tabs');
    scrollEl = h('div.paper-scroll');
    npEl = h('aside.np');
    root.append(h('div.table-room'), h('div.songs', h('div.songs-left', barEl, tabsEl, scrollEl), npEl));
    scrollEl.addEventListener('click', onRowClick);
    scrollEl.addEventListener('pointerdown', onGripDown);
    App.onState('songs', () => { if (editing && !dirty) layout = downloadTiers(); renderAll(); });
    App.onState('select-song', id => { if (editing) return; const sec = sectionOf(id); if (sec) { tab = sec.id; query = ''; searchEl.value = ''; } select(id, false); renderAll(); requestAnimationFrame(() => { const row = scrollEl.querySelector(`[data-id="${CSS.escape(id)}"]`); if (row) row.scrollIntoView({ block: 'center' }); }); });
    Player.onChange(onPlayer);
    renderAll();
  }

  const data = () => App.state.songs;
  const song = id => data() && data().songs[id];
  const lib = id => data() && data().library && data().library[id];
  const downloadTiers = () => { const d = data(); const s = d && d.sections.find(x => x.id === 'download'); return s ? s.tiers.map(t => ({ title: t.title, songs: t.songs.slice() })) : []; };
  const order = () => layout ? layout.flatMap(t => t.songs) : [];
  const singerOf = id => (singers[id] || ((song(id) || {}).singer || 'male')).toLowerCase();
  function sectionOf(id) { const d = data(); return d && d.sections.find(s => s.tiers.some(t => t.songs.includes(id))); }
  function tierOf(id) {
    const d = data(); if (!d) return null;
    for (const s of d.sections) for (let i = 0; i < s.tiers.length; i++) if (s.tiers[i].songs.includes(id)) return { section: s, index: i, tier: s.tiers[i] };
    return null;
  }
  function iconFor(sectionId, index) {
    if (sectionId === 'setlist') return `assets/art/icon_${VENUES[index] || 'generic'}.webp`;
    return sectionId === 'download' ? 'assets/art/icon_download.webp' : 'assets/art/icon_generic.webp';
  }
  const count = s => s.tiers.reduce((n, t) => n + t.songs.length, 0);

  function renderAll() { renderBar(); renderTabs(); renderPage(); renderNp(); }

  /* ---- the bar over the papers ---- */
  function renderBar() {
    const isDl = tab === 'download' && !query;
    if (editing) {
      const sel = checked.size;
      barEl.replaceChildren(
        h('button.btn.sm' + (dirty ? '.primary' : ''), { disabled: !dirty, onclick: saveLayout }, icon('check'), 'Guardar'),
        h('button.btn.sm.ghost', { onclick: () => leaveEdit() }, dirty ? 'Descartar' : 'Salir'),
        h('select.sort', { title: 'Ordenar las canciones dentro de cada división', onchange: e => { sortBy(e.target.value); e.target.value = ''; } },
          h('option', { value: '' }, 'Ordenar por…'),
          h('option', { value: 'artist' }, 'Artista (juntos por artista)'),
          h('option', { value: 'title' }, 'Título A–Z'),
          h('option', { value: 'year' }, 'Año'),
          h('option', { value: 'recent' }, 'Importadas más recientes primero'),
          h('option', { value: 'dlcfirst' }, 'DLC oficiales primero'),
          h('option', { value: 'reverse' }, 'Invertir orden')),
        h('button.btn.sm.ghost', { title: 'Agregar una división vacía al final', onclick: addTier }, icon('plus'), 'División'),
        h('span.sel-count', sel ? `${sel} marcada${sel > 1 ? 's' : ''}` : 'Marca canciones para moverlas o borrarlas juntas'),
        h('div', { style: { marginLeft: 'auto', display: 'flex', gap: '6px' } },
          h('select.sort', { disabled: !sel, title: 'Mover las marcadas a otra división', onchange: e => { moveCheckedTo(e.target.value); e.target.value = ''; } },
            h('option', { value: '' }, 'Mover a…'),
            layout.map((t, i) => h('option', { value: String(i) }, t.title || DEFAULT_TIER)),
            h('option', { value: 'new' }, '+ División nueva…')),
          h('button.btn.sm.ghost', { disabled: !sel, title: 'Subir las marcadas al principio de su división', onclick: () => moveChecked('top') }, '⤒'),
          h('button.btn.sm.ghost', { disabled: !sel, title: 'Bajar las marcadas al final de su división', onclick: () => moveChecked('bottom') }, '⤓'),
          h('button.btn.sm.ghost', { onclick: () => { const all = order().every(id => checked.has(id)); checked.clear(); if (!all) order().forEach(id => checked.add(id)); renderBar(); refreshChecks(); } }, 'Todas'),
          h('button.btn.sm.danger', { disabled: !sel, onclick: () => remove([...checked]) }, icon('trash'), 'Eliminar')));
      return;
    }
    barEl.replaceChildren(
      h('div.search', icon('search'), searchEl),
      isDl && h('button.btn.sm', { onclick: enterEdit }, icon('edit'), 'Editar lista'),
      h('div', { style: { marginLeft: 'auto', display: 'flex', gap: '8px' } },
        h('button.btn.sm.ghost', { title: 'Volver a leer el setlist del juego', onclick: async () => { await App.refreshSongs(true); App.toast('Setlist actualizado'); } }, icon('refresh')),
        h('button.btn.sm', { onclick: () => App.go('import') }, icon('import'), 'Importar')));
  }

  function renderTabs() {
    const d = data();
    tabsEl.replaceChildren(...PAGES.map(p => {
      const s = d && d.sections.find(x => x.id === p.id);
      return h('button.tab.' + p.cls + (tab === p.id && !query ? '.on' : ''), {
        onclick: async () => {
          if (editing && !await leaveEdit()) return;
          tab = p.id; query = ''; searchEl.value = '';
          try { localStorage.setItem('songs.tab', tab); } catch (e) { }
          renderAll(); scrollEl.scrollTop = 0;
        }
      }, p.label, h('small', s ? count(s) : ''));
    }));
  }

  /* ---- the papers ---- */
  function row(id, n, sectionId) {
    const s = song(id);
    if (!s) return null;
    const custom = !!lib(id);
    // the stamp marks only what was imported since the launcher opened: after that it's not new
    const since = App.state.app && App.state.app.sessionStart;
    const fresh = custom && since && String(lib(id).installedAt || '') >= since;
    const on = Player.info().key === 'song:' + id;
    const edit = editing && sectionId === 'download';
    return h('div.song' + (custom ? '.custom' : '') + (selected === id && !edit ? '.sel' : '') + (on ? '.playing' : '') + (edit && checked.has(id) ? '.checked' : ''), { 'data-id': id },
      edit ? h('span.grip', { title: 'Arrastra para mover' }, h('i'), h('i'), h('i')) : null,
      edit ? h('span.ck', h('input', { type: 'checkbox', checked: checked.has(id), tabindex: -1 })) : null,
      edit ? null : h('span.n', n + '.'),
      h('div', { style: { minWidth: 0 } },
        h('div.t', s.title),
        h('div.a', (s.famousBy ? 'como lo hizo famoso ' : '') + s.artist + (s.charter ? ' · chart: ' + s.charter : ''))),
      h('div.meta', edit ? singerPick(id) : null, h('span.eq', h('i'), h('i'), h('i')), fresh && h('span.stamp', 'IMPORTADA'), s.year));
  }

  /* only while editing the Download list: the singer GH3 puts on stage for that song */
  function singerPick(id) {
    const v = singerOf(id);
    return h('span.sing.ink', { title: 'Cantante en el escenario' },
      [['male', '♂', 'Cantante (el de siempre)'], ['female', '♀', 'Cantante mujer'], ['bret', 'B', 'Bret Michaels'], ['none', null, 'Sin cantante (canción sin voz)']].map(([val, glyph, label]) =>
        h('button' + (v === val ? '.on' : ''), { type: 'button', title: label, 'data-singer': val }, glyph || icon('nomic'))));
  }

  function renderPage() {
    const d = data();
    if (!d) { scrollEl.replaceChildren(h('div.empty', h('strong', 'Leyendo el setlist…'), 'Un momento.')); return; }
    if (query) return renderSearch(d);
    const page = PAGES.find(p => p.id === tab);
    const sec = d.sections.find(s => s.id === tab);
    const body = h('div.ph-body');
    if (tab === 'setlist') body.append(h('img.title-img', { src: 'assets/art/page1_title.webp', alt: 'Setlist', draggable: false }));
    else body.append(h('div.title-ink', page.label));
    if (editing && tab === 'download') {
      body.append(h('p.edit-hint', 'Arrastra cada canción por su agarradera, también a otra división. Si marcas varias, se mueven juntas. Haz clic en el nombre de una división para cambiarlo; ▲ ▼ la mueven de lugar. ♂ ♀ B y el micrófono tachado: el cantante de cada canción (o ninguno). Al terminar, presiona "Guardar".'));
      let n = 0;
      layout.forEach((t, ti) => {
        const title = h('input.tier-title', { type: 'text', value: t.title || '', placeholder: DEFAULT_TIER, maxlength: 48, spellcheck: false,
          oninput: e => { t.title = e.target.value; markDirty(); }, onchange: () => renderBar() });
        body.append(h('div.tier', { 'data-tier': ti },
          h('div.tier-head', h('img', { src: iconFor('download', 0), alt: '', draggable: false }), title,
            h('small', t.songs.length + ' canciones'),
            h('button.tier-mv', { title: 'Subir la división (así queda en el juego)', disabled: ti === 0, onclick: () => moveTier(ti, -1) }, '▲'),
            h('button.tier-mv', { title: 'Bajar la división (así queda en el juego)', disabled: ti === layout.length - 1, onclick: () => moveTier(ti, 1) }, '▼'),
            h('button.tier-x', { title: layout.length > 1 ? 'Quitar la división (sus canciones pasan a la de arriba)' : 'Es la única división', disabled: layout.length < 2, onclick: () => dropTier(ti) }, icon('x'))),
          h('div.edit-list', { 'data-tier': ti }, t.songs.length ? t.songs.map(id => row(id, ++n, 'download')) : h('div.tier-empty', 'Vacía: arrastra canciones aquí (si queda vacía, no se guarda)'))));
      });
    } else {
      let n = 0;
      (sec ? sec.tiers : []).forEach((t, i) => {
        if (!t.songs.length) return;
        body.append(h('div.tier',
          h('div.tier-head', h('img', { src: iconFor(tab, i), alt: '', draggable: false }), h('h3', tierTitle(tab, t)), h('small', t.songs.length + ' canciones')),
          t.songs.map(id => row(id, ++n, tab))));
      });
      if (tab === 'download' && n === 0) body.append(h('p.no-results', 'No hay canciones descargadas. Importa las tuyas desde Importar.'));
    }
    const paper = h('div.paper.' + page.cls + (editing ? '.editing' : ''), h('div.ph-head'), body,
      tab === 'setlist' && h('img.clip', { src: 'assets/art/clip.webp', alt: '', draggable: false }));
    scrollEl.replaceChildren(paper);
  }

  function tierTitle(sectionId, t) {
    if (sectionId === 'bonus') return 'Canciones bonus';
    return t.title || (sectionId === 'download' ? DEFAULT_TIER : '');
  }

  function renderSearch(d) {
    const q = query;
    const hits = [];
    d.sections.forEach(s => s.tiers.forEach(t => t.songs.forEach(id => {
      const x = song(id);
      if (x && (x.title + ' ' + x.artist + ' ' + (x.album || '') + ' ' + (x.charter || '')).toLowerCase().includes(q)) hits.push([s, id]);
    })));
    const body = h('div.ph-body', h('div.title-ink', 'Buscar'));
    if (!hits.length) body.append(h('p.no-results', `Nada con "${query}".`));
    PAGES.forEach(p => {
      const ids = hits.filter(x => x[0].id === p.id).map(x => x[1]);
      if (!ids.length) return;
      let n = 0;
      body.append(h('div.tier', h('div.tier-head', h('img', { src: iconFor(p.id, 0), alt: '' }), h('h3', p.label), h('small', ids.length + ' resultados')), ids.map(id => row(id, ++n, p.id))));
    });
    scrollEl.replaceChildren(h('div.paper.p3', h('div.ph-head'), body));
  }

  function onRowClick(e) {
    const r = e.target.closest('.song');
    if (!r || e.target.closest('.grip')) return;
    const id = r.dataset.id;
    const sb = e.target.closest('[data-singer]');
    if (sb && editing) {
      const v = sb.dataset.singer;
      if ((((song(id) || {}).singer) || 'male').toLowerCase() === v) delete singers[id]; else singers[id] = v;
      r.querySelectorAll('[data-singer]').forEach(b => b.classList.toggle('on', b.dataset.singer === v));
      markDirty();
      return;
    }
    if (editing && tab === 'download') {
      checked.has(id) ? checked.delete(id) : checked.add(id);
      r.classList.toggle('checked', checked.has(id));
      r.querySelector('.ck input').checked = checked.has(id);
      renderBar();
      return;
    }
    if (selected === id && Player.info().key === 'song:' + id) { Player.toggle(); return; }
    select(id, true);
  }

  function refreshChecks() {
    scrollEl.querySelectorAll('.song[data-id]').forEach(r => {
      const on = checked.has(r.dataset.id);
      r.classList.toggle('checked', on);
      const c = r.querySelector('.ck input'); if (c) c.checked = on;
    });
  }

  /* ---- editing the Download list ---- */
  function enterEdit() {
    editing = true; dirty = false; checked.clear(); singers = {};
    layout = downloadTiers();
    if (!layout.length) layout = [{ title: DEFAULT_TIER, songs: [] }];
    Player.stop();
    renderAll();
  }

  async function leaveEdit(force) {
    if (dirty && !force && !await App.confirm('¿Descartar los cambios?', 'Cambiaste la lista y no lo guardaste.', 'Descartar', 'danger')) return false;
    editing = false; dirty = false; layout = null; singers = {}; checked.clear();
    renderAll();
    return true;
  }

  function markDirty() {
    const was = dirty;
    const base = downloadTiers().filter(t => t.songs.length);
    const now = layout.filter(t => t.songs.length);
    dirty = Object.keys(singers).length > 0 || base.length !== now.length ||
      now.some((t, i) => (t.title || '').trim() !== (base[i].title || '').trim() || t.songs.length !== base[i].songs.length || t.songs.some((id, j) => id !== base[i].songs[j]));
    if (was !== dirty) renderBar();
  }

  function setLayout(next) {
    layout = next;
    markDirty();
    renderBar(); renderPage();
  }
  const cloneLayout = () => layout.map(t => ({ title: t.title, songs: t.songs.slice() }));

  async function askTitle(title, value) {
    const inp = h('input', { type: 'text', value: value || '', maxlength: 48, placeholder: DEFAULT_TIER });
    const ok = await App.modal({ title, body: h('label.full', 'Nombre de la división', inp), actions: [{ label: 'Cancelar', value: false, kind: 'ghost' }, { label: 'Aceptar', value: true, kind: 'primary' }] });
    return ok ? (inp.value.trim() || DEFAULT_TIER) : null;
  }

  async function addTier() {
    const t = await askTitle('División nueva', '');
    if (t == null) return;
    const next = cloneLayout(); next.push({ title: t, songs: [] });
    setLayout(next);
    requestAnimationFrame(() => { const all = scrollEl.querySelectorAll('.tier'); if (all.length) all[all.length - 1].scrollIntoView({ block: 'center', behavior: 'smooth' }); });
  }

  /* a whole division up or down: GH3 shows them in this order */
  function moveTier(i, by) {
    const j = i + by;
    if (j < 0 || j >= layout.length) return;
    const next = cloneLayout();
    [next[i], next[j]] = [next[j], next[i]];
    setLayout(next);
    requestAnimationFrame(() => { const el = scrollEl.querySelector(`.tier[data-tier="${j}"]`); if (el) el.scrollIntoView({ block: 'nearest', behavior: 'smooth' }); });
  }

  function dropTier(i) {
    if (layout.length < 2) return;
    const next = cloneLayout();
    const [gone] = next.splice(i, 1);
    next[Math.max(0, i - 1)].songs.push(...gone.songs);
    setLayout(next);
  }

  async function moveCheckedTo(where) {
    if (!where) return;
    const next = cloneLayout();
    let target;
    if (where === 'new') {
      const t = await askTitle('Mover a una división nueva', '');
      if (t == null) { renderBar(); return; }
      next.push(target = { title: t, songs: [] });
    } else target = next[+where];
    const picked = order().filter(id => checked.has(id));
    next.forEach(t => { t.songs = t.songs.filter(id => !checked.has(id)); });
    target.songs.push(...picked);
    setLayout(next);
  }

  function sortBy(kind) {
    if (!kind) return;
    const s = id => song(id) || {};
    const cmp = (a, b) => String(a || '').localeCompare(String(b || ''), 'es', { sensitivity: 'base', numeric: true });
    const base = order();
    const next = cloneLayout();
    for (const t of next) {
      if (kind === 'artist') t.songs.sort((a, b) => cmp(s(a).artist, s(b).artist) || cmp(s(a).year, s(b).year) || cmp(s(a).title, s(b).title));
      else if (kind === 'title') t.songs.sort((a, b) => cmp(s(a).title, s(b).title));
      else if (kind === 'year') t.songs.sort((a, b) => cmp(s(a).year, s(b).year) || cmp(s(a).artist, s(b).artist));
      else if (kind === 'recent') t.songs.sort((a, b) => cmp((lib(b) || {}).installedAt, (lib(a) || {}).installedAt) || base.indexOf(a) - base.indexOf(b));
      else if (kind === 'dlcfirst') t.songs.sort((a, b) => (!!lib(a) - !!lib(b)) || base.indexOf(a) - base.indexOf(b));
      else if (kind === 'reverse') t.songs.reverse();
    }
    setLayout(next);
  }

  function moveChecked(where) {
    const next = cloneLayout();
    for (const t of next) {
      const picked = t.songs.filter(id => checked.has(id));
      const rest = t.songs.filter(id => !checked.has(id));
      t.songs = where === 'top' ? picked.concat(rest) : rest.concat(picked);
    }
    setLayout(next);
  }

  /* drag by the grip: a lifted copy follows the pointer, a dashed gap shows where it lands */
  function onGripDown(e) {
    const grip = e.target.closest('.grip');
    if (!grip || !editing || e.button !== 0) return;
    e.preventDefault();
    const rowEl = grip.closest('.song');
    const id = rowEl.dataset.id;
    const moving = checked.has(id) && checked.size > 1 ? order().filter(x => checked.has(x)) : [id];
    const lists = [...scrollEl.querySelectorAll('.edit-list')];
    const rect = rowEl.getBoundingClientRect();
    const ghost = rowEl.cloneNode(true);
    ghost.classList.add('song-ghost');
    ghost.style.width = rect.width + 'px';
    if (moving.length > 1) ghost.append(h('b.ghost-count', '+' + (moving.length - 1)));
    document.body.append(ghost);
    const gap = h('div.song-gap', { style: { height: rect.height * Math.min(moving.length, 3) + 'px' } });
    const srcRows = moving.map(m => scrollEl.querySelector(`.edit-list [data-id="${CSS.escape(m)}"]`)).filter(Boolean);
    srcRows.forEach(r => r.classList.add('lifted'));
    rowEl.before(gap);
    const dy = e.clientY - rect.top;
    let lastY = e.clientY, raf = 0;
    // the division under the pointer: the last one whose top is above it
    const listAt = y => { let at = lists[0]; for (const l of lists) if (l.closest('.tier').getBoundingClientRect().top <= y) at = l; return at; };
    const place = y => {
      ghost.style.transform = `translate(${rect.left}px, ${y - dy}px) rotate(-1.2deg)`;
      const list = listAt(y);
      const rows = [...list.querySelectorAll('.song:not(.lifted)')];
      let before = null;
      for (const r of rows) { const b = r.getBoundingClientRect(); if (y < b.top + b.height / 2) { before = r; break; } }
      if (before) { if (gap.nextSibling !== before) before.before(gap); }
      else if (list.lastChild !== gap) list.append(gap);
    };
    const scroll = () => {
      const b = scrollEl.getBoundingClientRect();
      const edge = 70;
      let v = 0;
      if (lastY < b.top + edge) v = -Math.ceil((b.top + edge - lastY) / 6);
      else if (lastY > b.bottom - edge) v = Math.ceil((lastY - (b.bottom - edge)) / 6);
      if (v) { scrollEl.scrollTop += v; place(lastY); }
      raf = requestAnimationFrame(scroll);
    };
    const move = ev => { lastY = ev.clientY; place(ev.clientY); };
    const up = () => {
      cancelAnimationFrame(raf);
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', up);
      ghost.remove();
      // where they land: the division holding the gap, before the first non-moving row after it
      const ti = +gap.closest('.edit-list').dataset.tier;
      let next = gap.nextElementSibling;
      while (next && !(next.classList.contains('song') && !next.classList.contains('lifted'))) next = next.nextElementSibling;
      const beforeId = next ? next.dataset.id : null;
      const out = cloneLayout();
      out.forEach(t => { t.songs = t.songs.filter(x => !moving.includes(x)); });
      const dest = out[ti].songs;
      const at = beforeId ? dest.indexOf(beforeId) : dest.length;
      dest.splice(at < 0 ? dest.length : at, 0, ...moving);
      setLayout(out);
    };
    place(e.clientY);
    raf = requestAnimationFrame(scroll);
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  }

  const layoutJob = () => ({
    tiers: layout.filter(t => t.songs.length).map(t => ({ title: (t.title || '').trim() || DEFAULT_TIER, songs: t.songs.slice() })),
    singers: Object.assign({}, singers)
  });

  async function saveLayout() {
    const empty = layout.filter(t => !t.songs.length).length;
    const body = h('div',
      h('p', 'La lista Download quedará así' + (Object.keys(singers).length ? `, con ${Object.keys(singers).length} cantante(s) cambiado(s)` : '') + '.'),
      empty ? h('p.faint', `${empty} división(es) vacía(s) no se guardan.`) : null,
      h('p', { style: { color: '#f1cf82' } }, 'GH3 asocia tu partida guardada al orden de las listas: al abrir el juego probablemente te pida crear una partida nueva. Antes de guardar, el launcher respalda tu partida y el setlist.'),
      h('p.faint', 'Con Trucos › Desbloqueos automáticos recuperas al instante canciones, tienda y trucos en la partida nueva.'));
    if (!await App.modal({ title: '¿Guardar la lista?', body, actions: [{ label: 'Cancelar', value: false, kind: 'ghost' }, { label: 'Guardar', value: true, kind: 'primary' }] })) return;
    try {
      await Bridge.call('songs.layout', { layout: layoutJob() });
      App.toast('Lista guardada', 'La lista Download ya está en el juego así.', 'ok');
      dirty = false;
      await App.refreshSongs();
      leaveEdit(true);
    } catch (e) { App.fail(e); }
  }

  /* ---- selecting, playing, deleting ---- */
  function select(id, autoplay) {
    selected = id;
    scrollEl.querySelectorAll('.song.sel').forEach(x => x.classList.remove('sel'));
    const r = scrollEl.querySelector(`[data-id="${CSS.escape(id)}"]`);
    if (r) r.classList.add('sel');
    renderNp();
    if (autoplay) playSelected();
  }

  async function playSelected() {
    const s = song(selected); if (!s) return;
    const id = selected;
    try {
      const srcs = await Bridge.call('songs.prepare', { name: s.name, stems: mode === 'full' ? ['guitar', 'rhythm', 'song'] : ['preview'] });
      if (selected !== id) return;
      if (!srcs.length) throw new Error('No se encontró el audio de esta canción (DATA\\MUSIC\\' + s.name + '.fsb.xen).');
      await Player.play('song:' + id, srcs, 0);
    } catch (e) { App.fail(e); }
  }

  async function remove(ids) {
    const dlc = ids.filter(id => !lib(id));
    const names = ids.map(id => (song(id) || {}).title || id);
    const text = h('div', h('p', `Se quitarán de la lista Download y se borrarán sus archivos (${ids.length}):`),
      h('p.faint', names.slice(0, 8).join(', ') + (names.length > 8 ? '…' : '')),
      dlc.length ? h('p', { style: { color: '#f1cf82' } }, `${dlc.length} son DLC oficiales que trae BetterGH3.`) : null,
      h('p', { style: { color: '#f1cf82' } }, 'Como al agregar canciones, GH3 probablemente te pida una partida nueva.'),
      h('p.faint', 'Antes se respaldan el setlist y tu partida guardada (Herramientas › Respaldos).'));
    if (!await App.modal({ title: '¿Eliminar canciones?', body: text, actions: [{ label: 'Cancelar', value: false, kind: 'ghost' }, { label: 'Eliminar', value: true, kind: 'danger' }] })) return;
    try {
      Player.stop();
      if (editing && dirty) { await Bridge.call('songs.layout', { layout: layoutJob() }); dirty = false; singers = {}; }
      await Bridge.call('songs.delete', { names: ids.map(id => (song(id) || {}).name || id) });
      App.toast('Canciones eliminadas', `${ids.length} fuera del juego.`, 'ok');
      checked.clear(); if (ids.includes(selected)) selected = null;
      await App.refreshSongs();
      if (editing) { layout = downloadTiers(); renderAll(); }
    } catch (e) { App.fail(e); }
  }

  /* ---- the right column ---- */
  let npPlayer = null;
  function renderNp() {
    const s = song(selected);
    if (editing && tab === 'download') {
      npEl.replaceChildren(h('div.empty', { style: { padding: '50px 10px' } }, h('strong', 'Editando'), 'Arrastra por la agarradera (también a otra división) o usa "Ordenar por…". Marca canciones para moverlas, cambiarlas de división o eliminarlas juntas. Cada división se ve con su título en Quickplay › Download.'));
      npPlayer = null;
      return;
    }
    if (!s) {
      npEl.replaceChildren(h('div.empty', { style: { padding: '60px 10px' } }, h('strong', 'Elige una canción'), 'Haz clic en cualquier canción de la hoja para escuchar su preview.'));
      npPlayer = null;
      return;
    }
    const l = lib(selected);
    const t = tierOf(selected);
    const cover = h('div.cover', l && l.cover ? h('img.art', { src: 'https://gh3.covers/' + encodeURIComponent(l.cover), alt: '' }) : h('img.venue', { src: iconFor(t ? t.section.id : '', t ? t.index : 0), alt: '' }));
    const seek = h('div.seek', h('i'), h('b'));
    seek.addEventListener('mousedown', e => {
      const i = Player.info(); if (!i.dur) return;
      const set = ev => { const r = seek.getBoundingClientRect(); Player.seek(Math.max(0, Math.min(1, (ev.clientX - r.left) / r.width)) * i.dur); };
      set(e);
      const up = () => { window.removeEventListener('mousemove', set); window.removeEventListener('mouseup', up); };
      window.addEventListener('mousemove', set); window.addEventListener('mouseup', up);
    });
    const pbtn = h('button.pbtn', { title: 'Reproducir / pausar', onclick: () => { if (Player.info().key === 'song:' + selected) Player.toggle(); else playSelected(); } }, icon('play'));
    const time = h('span.time', '0:00 / 0:00');
    const vol = h('input', { type: 'range', min: 0, max: 1, step: 0.01, value: Player.info().volume, oninput: e => Player.volume(+e.target.value) });
    npPlayer = { pbtn, time, seek };
    const dl = [['Álbum', s.album || (l && l.album)], ['Año', s.year], ['Charter', s.charter || (l && l.charter)], ['Duración', l && l.lengthMs ? App.fmtTime(l.lengthMs) : null],
      ['Lista', t ? (t.section.id === 'setlist' ? t.tier.title : t.section.title) : null], ['Importada', l && l.installedAt], ['Origen', l && l.source], ['Nombre interno', s.name]];
    npEl.replaceChildren(
      cover,
      h('div', h('h2', s.title), h('div.by', (s.famousBy ? 'como lo hizo famoso ' : 'de ') + s.artist)),
      h('div.player',
        h('div.row', pbtn, seek),
        h('div.row', time,
          h('div.seg', { style: { marginLeft: 'auto' } },
            h('button' + (mode === 'preview' ? '.on' : ''), { onclick: () => { mode = 'preview'; renderNp(); playSelected(); } }, 'Preview'),
            h('button' + (mode === 'full' ? '.on' : ''), { onclick: () => { mode = 'full'; renderNp(); playSelected(); } }, 'Completa'))),
        h('div.row.vol', icon('vol'), vol)),
      h('dl', dl.filter(x => x[1]).map(([k, v]) => [h('dt', k), h('dd', String(v))])),
      t && t.section.id === 'download'
        ? h('button.btn.danger', { onclick: () => remove([selected]) }, icon('trash'), l ? 'Eliminar canción' : 'Eliminar DLC')
        : h('p.np-hint', 'Las canciones del Setlist y Bonus son parte del juego y no se pueden borrar ni mover.')
    );
    onPlayer(Player.info());
  }

  function onPlayer(i) {
    if (!root) return;
    scrollEl.querySelectorAll('.song.playing').forEach(x => { if ('song:' + x.dataset.id !== i.key || !i.playing) x.classList.remove('playing'); });
    if (i.key && i.key.startsWith('song:') && i.playing) { const r = scrollEl.querySelector(`[data-id="${CSS.escape(i.key.slice(5))}"]`); if (r) r.classList.add('playing'); }
    if (!npPlayer) return;
    const mine = i.key === 'song:' + selected;
    const ic = mine && (i.playing || i.loading) ? 'pause' : 'play';
    if (npPlayer.ic !== ic) { npPlayer.ic = ic; npPlayer.pbtn.replaceChildren(icon(ic)); }
    const pct = mine && i.dur ? i.pos / i.dur * 100 : 0;
    npPlayer.seek.querySelector('i').style.width = pct + '%';
    npPlayer.seek.querySelector('b').style.left = pct + '%';
    npPlayer.time.textContent = mine ? (i.loading ? 'Cargando…' : `${App.fmtTime(i.pos)} / ${App.fmtTime(i.dur)}`) : '0:00 / 0:00';
  }

  return {
    mount,
    show: () => { if (!App.state.songs) App.refreshSongs(); },
    hide: () => { if ((Player.info().key || '').startsWith('song:')) Player.stop(); }
  };
})());
