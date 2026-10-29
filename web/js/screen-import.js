/* Importar: drop Clone Hero songs, check and hear them, fix their details, install them into the
   game's Download list (the song tool compiles chart + audio, the launcher backs up first). */
App.register('import', (() => {
  const { h, icon } = App;
  let root, listEl, dropEl, scanEl, barEl;
  let items = [];
  let scanning = null;

  const STAGES = { chart: 'Chart', audio: 'Audio', install: 'Instalando', done: 'Listo' };
  const COUNTOFFS = ['HiHat01', 'HiHat02', 'HiHat03', 'Sticks_Huge', 'Sticks_Normal', 'Sticks_Tiny'];

  function mount(r) {
    root = r;
    dropEl = h('div.dropzone');
    scanEl = h('div.scan-line', { hidden: true }, h('span.spinner'), h('span'));
    listEl = h('div.queue');
    barEl = h('div.actionbar', { hidden: true });
    root.append(h('div.room.room-import'),
      h('div.scroll', h('div.page-wrap',
        h('div.head', h('h1', 'Importar'), h('div.sub', 'Canciones de Clone Hero → lista Download de Guitar Hero III')),
        dropEl, scanEl, listEl)),
      barEl);
    Bridge.on('import.item', it => { const i = items.findIndex(x => x.id === it.id); if (i >= 0) items[i] = it; else items.push(it); render(); });
    Bridge.on('import.scan', s => { scanning = s.busy ? s.text : null; renderScan(); });
    Bridge.on('import.progress', progress);
    Bridge.on('import.warning', w => App.toast('Aviso', w.message || ''));
    listEl.addEventListener('click', onCardClick);
    Player.onChange(onPlayer);
    load();
  }

  /* the whole card ticks/unticks the song (its buttons keep their own job) */
  function onCardClick(e) {
    const c = e.target.closest('.qi');
    if (!c || e.target.closest('button, a, select, input:not([type=checkbox]), .sing')) return;
    const it = items.find(i => i.id === c.dataset.id);
    if (!it || !selectable(it) || it.status === 'installing' || it.status === 'queued') return;
    update(it, { selected: !it.selected });
  }

  async function load() {
    try { items = await Bridge.call('import.list'); } catch (e) { App.fail(e); }
    render();
  }

  async function pick(kind) {
    try { const r = await Bridge.call('import.pick', { kind }); if (r && r.added === 0) App.toast('No se encontraron canciones', 'Elige .sng, .zip, .7z, .rar, o una carpeta con canciones de Clone Hero.'); }
    catch (e) { App.fail(e); }
  }

  function renderScan() {
    scanEl.hidden = !scanning;
    scanEl.lastChild.textContent = scanning || '';
  }

  const selectable = it => it.status === 'ready' || it.status === 'error';
  function render() {
    if (!root) return;
    const slim = items.length > 0;
    dropEl.className = 'dropzone' + (slim ? ' slim' : '');
    dropEl.replaceChildren(icon('import'),
      h('div', h('h3', slim ? 'Agregar más canciones' : 'Suelta aquí tus canciones'),
        h('p', slim ? 'Arrastra más archivos o carpetas a cualquier parte de la ventana.' : '.sng, .zip, .7z, .rar o carpetas de Clone Hero (con song.ini y notes.chart / notes.mid). También packs con muchas canciones.')),
      h('div.acts',
        h('button.btn' + (slim ? '' : '.primary'), { onclick: () => pick('files') }, icon('file'), 'Elegir archivos'),
        h('button.btn', { onclick: () => pick('folder') }, icon('folder'), 'Elegir carpeta')));
    listEl.replaceChildren(...groups().map(([tier, list]) => group(tier, list)));
    const ready = items.filter(selectable);
    const sel = ready.filter(i => i.selected);
    const done = items.filter(i => i.status === 'done').length;
    document.getElementById('badge-import').textContent = ready.length || '';
    barEl.hidden = !items.length;
    barEl.replaceChildren(
      h('span.count', `${sel.length} de ${ready.length} seleccionadas`),
      h('button.btn.sm.ghost', { onclick: () => setAll(true), disabled: !ready.length }, 'Todas'),
      h('button.btn.sm.ghost', { onclick: () => setAll(false), disabled: !ready.length }, 'Ninguna'),
      h('button.btn.sm.ghost', { onclick: () => moveTo(sel), disabled: !sel.length, title: 'Elegir la división de la lista Download donde quedarán las marcadas' }, icon('songs'), 'División…'),
      done ? h('button.btn.sm.ghost', { onclick: clearDone }, `Quitar instaladas (${done})`) : null,
      h('span.note', 'Van a la lista Download, cada una en su división. Antes se respaldan el setlist y tu partida guardada.'),
      h('button.btn.primary', { disabled: !sel.length, onclick: install }, icon('check'), sel.length === 1 ? 'Instalar 1 canción' : `Instalar ${sel.length} canciones`));
    onPlayer(Player.info());
  }

  /* ---- divisions: each pack is its own; loose songs go to "Lost and Cursed Songs" ---- */
  const DEFAULT_TIER = 'Lost and Cursed Songs';
  const tierOf = it => (it.tier || '').trim();
  function groups() {
    const map = new Map();
    for (const it of items) { const k = tierOf(it); if (!map.has(k)) map.set(k, []); map.get(k).push(it); }
    if (map.has('')) { const loose = map.get(''); map.delete(''); map.set('', loose); }   // the catch-all last
    return [...map];
  }
  function group(tier, list) {
    const pick = list.filter(selectable);
    const allOn = pick.length && pick.every(i => i.selected);
    return h('section.qgroup',
      h('div.qgroup-head',
        h('img', { src: 'assets/art/icon_download.webp', alt: '', draggable: false }),
        h('h3', { title: tier ? 'Así se llamará la división en el juego' : 'Las canciones sin división van aquí' }, tier || DEFAULT_TIER),
        h('button.btn.sm.ghost', { title: 'Cambiar el nombre de esta división', onclick: () => renameTier(tier, list) }, icon('edit')),
        h('small', list.length === 1 ? '1 canción' : list.length + ' canciones'),
        pick.length ? h('button.btn.sm.ghost', { onclick: () => setMany(pick, { selected: !allOn }) }, allOn ? 'Desmarcar' : 'Marcar todas') : null),
      list.map(card));
  }
  function knownTiers() {
    const s = new Set();
    const d = App.state.songs && App.state.songs.sections.find(x => x.id === 'download');
    if (d) d.tiers.forEach(t => t.title && s.add(t.title));
    items.forEach(i => tierOf(i) && s.add(tierOf(i)));
    s.add(DEFAULT_TIER);
    return [...s];
  }
  async function askTier(title, value) {
    const inp = h('input', { type: 'text', value: value || '', maxlength: 48, placeholder: DEFAULT_TIER, list: 'tier-names' });
    const body = h('div', h('label.full', 'Nombre de la división', inp),
      h('datalist#tier-names', knownTiers().map(t => h('option', { value: t }))),
      h('p.faint', { style: { marginTop: '10px' } }, 'Escribe una nueva o elige una que ya exista en la lista Download. Vacío = "' + DEFAULT_TIER + '".'));
    const ok = await App.modal({ title, body, actions: [{ label: 'Cancelar', value: false, kind: 'ghost' }, { label: 'Aceptar', value: true, kind: 'primary' }] });
    if (!ok) return null;
    const v = inp.value.trim();
    return v.toLowerCase() === DEFAULT_TIER.toLowerCase() ? '' : v;
  }
  async function renameTier(tier, list) {
    const v = await askTier('Nombre de la división', tier);
    if (v == null || v === tier) return;
    setMany(list, { tier: v });
  }
  async function moveTo(list) {
    if (!list.length) return;
    const same = list.every(i => tierOf(i) === tierOf(list[0]));
    const v = await askTier(list.length === 1 ? 'Mover a la división…' : `Mover ${list.length} canciones a la división…`, same ? tierOf(list[0]) : '');
    if (v == null) return;
    setMany(list, { tier: v });
  }
  async function setMany(list, fields) {
    list.forEach(i => Object.assign(i, fields));
    render();
    try { await Bridge.call('import.updateMany', { ids: list.map(i => i.id), fields }); } catch (e) { App.fail(e); }
  }

  function diff(n) {
    if (n == null || n < 0) return null;
    return h('span.diff', { title: 'Dificultad de guitarra: ' + n + '/6' }, [0, 1, 2, 3, 4, 5].map(i => h('i' + (i < n ? '.on' : ''))));
  }

  function stemsText(it) {
    const kinds = (it.stems || []).map(s => s.kind);
    if (!kinds.length) return 'Sin audio';
    const parts = [];
    if (kinds.includes('guitar')) parts.push('Guitarra');
    if (kinds.includes('rhythm')) parts.push('Bajo/ritmo');
    const back = kinds.filter(k => k === 'backing').length;
    if (back) parts.push(back === 1 && !parts.length ? 'Una sola pista' : `+${back} pista${back > 1 ? 's' : ''}`);
    return parts.join(' · ');
  }

  function card(it) {
    const busy = it.status === 'installing' || it.status === 'queued';
    const status = {
      done: h('span.chip.ok', icon('check'), 'Instalada' + (it.checksum ? '' : '')),
      error: h('span.chip.bad', { title: it.error || '' }, 'Error'),
      invalid: h('span.chip.bad', 'No se puede instalar')
    }[it.status];
    const playing = Player.info().key === 'imp:' + it.id;
    return h('div.qi' + (it.selected && selectable(it) ? '.sel' : '') + (it.status === 'done' ? '.done' : '') + (selectable(it) && !busy ? '.pickable' : ''), { 'data-id': it.id },
      h('input', { type: 'checkbox', checked: !!it.selected && selectable(it), disabled: !selectable(it) || busy, tabindex: -1 }),
      it.art ? h('img.art', { src: it.art, alt: '', loading: 'lazy', draggable: false }) : h('div.art'),
      h('div.info',
        h('div.t', it.title || '(sin título)'),
        h('div.a', [it.artist, it.album, it.year || null].filter(Boolean).join(' · ')),
        h('div.m',
          diff(it.diffGuitar),
          it.lengthMs ? h('span.chip', App.fmtTime(it.lengthMs)) : null,
          it.notesType ? h('span.chip', it.notesType === 'mid' ? 'MIDI' : 'CHART') : null,
          h('span.chip', stemsText(it)),
          it.charter ? h('span.chip', { title: 'Charter' }, it.charter) : null,
          it.duplicateOf ? h('span.chip.hot', { title: 'Ya hay una canción con ese título y artista: ' + it.duplicateOf }, 'Ya instalada') : null,
          (it.warnings || []).map(w => h('span.chip.warn', { title: w }, icon('warn'), w.length > 46 ? w.slice(0, 44) + '…' : w)),
          it.status === 'error' && it.error ? h('span.chip.bad', { title: it.error }, it.error.length > 60 ? it.error.slice(0, 58) + '…' : it.error) : null,
          h('span.src', { title: it.source || '' }, it.sourceName || ''))),
      busy
        ? h('div.prog', h('span', it.stageText || 'En cola'), h('div.bar', h('i', { style: { width: Math.round((it.pct || 0) * 100) + '%' } })))
        : h('div.acts',
          status,
          it.status !== 'done' && it.status !== 'invalid' ? singerPick(it) : null,
          (it.stems || []).length ? h('button.btn.sm.ghost', { title: 'Escuchar desde el preview', onclick: () => preview(it) }, icon(playing ? 'stop' : 'play')) : null,
          it.status !== 'done' && it.status !== 'invalid' ? h('button.btn.sm.ghost', { title: 'Editar datos', onclick: () => edit(it) }, icon('edit')) : null,
          h('button.btn.sm.ghost', { title: 'Quitar de la lista', onclick: () => removeItems([it.id]) }, icon('x'))));
  }

  /* who sings it on stage: the default singer, the female one, Bret Michaels, or nobody (instrumentals) */
  const SINGERS = [['Male', '♂', 'Cantante (el de siempre)'], ['Female', '♀', 'Cantante mujer'], ['Bret', 'B', 'Bret Michaels'], ['None', 'nomic', 'Sin cantante (canción sin voz)']];
  const FROM = { song: 'según el song.ini', web: 'según internet (MusicBrainz)', looking: 'buscando en internet…', user: 'elegido por ti', default: 'no se encontró: el de siempre', novocals: 'no tiene voces: sin cantante' };
  function singerPick(it) {
    const v = it.singer || 'Male';
    return h('div.sing' + (it.singerFrom === 'looking' ? '.looking' : ''), { title: 'Cantante en el escenario · ' + (FROM[it.singerFrom] || '') },
      SINGERS.map(([val, glyph, label]) => h('button' + (v === val ? '.on' : ''), { type: 'button', title: label, onclick: () => { if (v !== val || it.singerFrom === 'looking') update(it, { singer: val, singerFrom: 'user' }); } }, val === 'None' ? icon(glyph) : glyph)));
  }

  async function update(it, fields) {
    Object.assign(it, fields);
    render();
    try { Object.assign(it, await Bridge.call('import.update', { id: it.id, fields })); } catch (e) { App.fail(e); }
  }
  async function setAll(on) {
    for (const it of items.filter(selectable)) if (!!it.selected !== on) { it.selected = on; Bridge.call('import.update', { id: it.id, fields: { selected: on } }).catch(() => { }); }
    render();
  }
  async function removeItems(ids) {
    if (ids.some(id => Player.info().key === 'imp:' + id)) Player.stop();
    items = items.filter(i => !ids.includes(i.id));
    render();
    try { await Bridge.call('import.remove', { ids }); } catch (e) { App.fail(e); }
  }
  async function clearDone() {
    items = items.filter(i => i.status !== 'done');
    render();
    try { await Bridge.call('import.clearDone'); } catch (e) { App.fail(e); }
  }

  async function preview(it) {
    if (Player.info().key === 'imp:' + it.id) { Player.stop(); return; }
    try { await Player.play('imp:' + it.id, it.stems.filter(s => s.kind !== 'crowd').map(s => s.url), it.previewStart || 0); }
    catch (e) { App.fail(e); }
  }

  function onPlayer(i) {
    if (!listEl) return;
    listEl.querySelectorAll('.qi').forEach(c => {
      const btn = c.querySelector('.acts button[title^="Escuchar"]');
      if (!btn) return;
      const on = i.key === 'imp:' + c.dataset.id && (i.playing || i.loading);
      if (btn.dataset.on !== String(on)) { btn.dataset.on = String(on); btn.replaceChildren(icon(on ? 'stop' : 'play')); }
    });
  }

  /* ---- editing one song's details ---- */
  function msToText(ms) { const s = Math.max(0, Math.round((ms || 0) / 1000)); return Math.floor(s / 60) + ':' + String(s % 60).padStart(2, '0'); }
  function textToMs(t) { const m = String(t).trim().match(/^(\d+):(\d{1,2})$/); return m ? (+m[1] * 60 + +m[2]) * 1000 : Math.max(0, parseFloat(t) * 1000 || 0); }

  async function edit(it) {
    const f = {};
    const inp = (k, v, attrs = {}) => (f[k] = h('input', Object.assign({ type: 'text', value: v == null ? '' : v }, attrs)));
    const sel = (k, v, opts) => (f[k] = h('select', opts.map(([val, label]) => h('option', { value: val, selected: val === v }, label))));
    const prev = inp('previewStart', msToText(it.previewStart));
    const body = h('div.form-grid',
      h('label.full', 'Título', inp('title', it.title)),
      h('label', 'Artista', inp('artist', it.artist)),
      h('label', 'Álbum', inp('album', it.album)),
      h('label', 'Año', inp('year', it.year || '', { type: 'number', min: 0, max: 2100 })),
      h('label', 'Charter', inp('charter', it.charter)),
      h('label', 'Inicio del preview (m:ss)',
        h('div.edit-preview', prev,
          h('button.btn.sm', { type: 'button', onclick: () => { const i = Player.info(); if (i.key === 'imp:' + it.id) prev.value = msToText(i.pos); else Player.play('imp:' + it.id, it.stems.map(s => s.url), textToMs(prev.value)); } }, 'Usar / escuchar'))),
      h('label', 'Conteo inicial', sel('countoff', it.countoff || 'HiHat01', COUNTOFFS.map(c => [c, c.replace('_', ' ')]))),
      h('label', 'Cantante', sel('singer', it.singer || 'Male', [['Male', 'Hombre (el de siempre)'], ['Female', 'Mujer'], ['Bret', 'Bret Michaels'], ['None', 'Ninguno']])),
      h('label', 'División (lista Download)', inp('tier', it.tier || '', { placeholder: DEFAULT_TIER, maxlength: 48 })),
      h('label', 'HOPO (ticks, 170 = normal)', inp('hopoFrequency', it.hopoFrequency || 170, { type: 'number', min: 10, max: 2000 })),
      h('label.check.full', (f.isCover = h('input', { type: 'checkbox', checked: !!it.isCover })), 'Es un cover: en el juego dirá "como lo hizo famoso"'));
    const ok = await App.modal({ title: 'Editar canción', body, wide: true, actions: [{ label: 'Cancelar', value: false, kind: 'ghost' }, { label: 'Guardar', value: true, kind: 'primary' }] });
    if (!ok) return;
    update(it, {
      title: f.title.value.trim() || it.title, artist: f.artist.value.trim(), album: f.album.value.trim(), charter: f.charter.value.trim(),
      year: parseInt(f.year.value) || 0, previewStart: textToMs(prev.value), countoff: f.countoff.value,
      ...(f.singer.value !== (it.singer || 'Male') ? { singer: f.singer.value, singerFrom: 'user' } : {}),
      tier: f.tier.value.trim().toLowerCase() === DEFAULT_TIER.toLowerCase() ? '' : f.tier.value.trim(),
      hopoFrequency: Math.max(10, parseInt(f.hopoFrequency.value) || 170), isCover: f.isCover.checked
    });
  }

  /* ---- installing ---- */
  const ov = () => document.getElementById('busy-overlay');
  async function install() {
    const ids = items.filter(i => i.selected && selectable(i)).map(i => i.id);
    if (!ids.length) return;
    const dups = items.filter(i => ids.includes(i.id) && i.duplicateOf);
    let replace = false;
    if (dups.length) {
      const lib = (App.state.songs && App.state.songs.library) || {};
      const mine = dups.filter(i => Object.keys(lib).some(k => k.toLowerCase() === String(i.duplicateOf).toLowerCase())).length;
      const body = h('div',
        h('p.muted', { style: { fontSize: '16px', margin: '4px 0 8px' } }, `${dups.length} canción(es) tienen el mismo título y artista que una que ya está en el juego.`),
        mine ? h('p', { style: { margin: '0 0 8px' } }, `"Reemplazar" vuelve a compilar las ${mine} que importaste antes, en su mismo lugar (así reciben las mejoras nuevas, como el movimiento de boca del cantante). Las canciones originales del juego y los DLC nunca se reemplazan: esas se instalan como copia.`) : null);
      const choice = await App.modal({ title: 'Algunas ya están instaladas', body, actions: [{ label: 'Cancelar', value: null, kind: 'ghost' }, { label: 'Instalar como copia', value: 'copy', kind: mine ? 'ghost' : 'primary' }].concat(mine ? [{ label: 'Reemplazar', value: 'replace', kind: 'primary' }] : []) });
      if (!choice) return;
      replace = choice === 'replace';
    }
    Player.stop();
    try { await Bridge.call('import.install', { ids, replace }); }
    catch (e) { hideBusy(); App.fail(e); }
  }

  function showBusy() {
    const o = ov();
    o.hidden = false;
    const v = o.querySelector('video');
    v.preload = 'auto';
    v.play().catch(() => { });
    document.getElementById('busy-title').textContent = 'Instalando';
  }
  function hideBusy() { const o = ov(); o.hidden = true; o.querySelector('video').pause(); }

  function progress(p) {
    if (p.phase === 'start') {
      showBusy();
      document.getElementById('busy-sub').textContent = `${p.count} canción(es)`;
      document.getElementById('busy-bar').style.width = '0%';
      document.getElementById('busy-detail').textContent = 'Respaldando el setlist y tu partida…';
    } else if (p.phase === 'song') {
      const it = items.find(i => i.id === p.id);
      if (it) { it.status = p.stage === 'done' ? it.status : 'installing'; it.pct = p.pct; it.stageText = STAGES[p.stage] || p.stage; }
      const overall = ((p.index - 1) + (p.pct || 0)) / Math.max(1, p.count);
      document.getElementById('busy-sub').textContent = `${p.index} de ${p.count} · ${p.title || ''}`;
      document.getElementById('busy-bar').style.width = Math.round(overall * 100) + '%';
      document.getElementById('busy-detail').textContent = {
        chart: 'Convirtiendo el chart a formato GH3…', audio: 'Mezclando y codificando el audio (lo más lento)…',
        install: 'Copiando al juego y agregando a la lista Download…', done: 'Lista.'
      }[p.stage] || '';
      render();
    } else if (p.phase === 'end') {
      hideBusy();
      if (p.installed) App.toast(p.installed === 1 ? '¡Canción instalada!' : `¡${p.installed} canciones instaladas!`, 'Están en Quickplay › Download. ' + (p.failed ? `${p.failed} fallaron (mira el detalle en la lista).` : ''), p.failed ? '' : 'ok');
      else App.toast('No se instaló nada', 'Revisa los errores en la lista.', 'bad');
      App.refreshSongs();
      render();
    }
  }

  return { mount, show: () => { if (root) render(); } };
})());
