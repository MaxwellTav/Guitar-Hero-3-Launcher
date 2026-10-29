/* Herramientas: the apps and scripts that came with BetterGH3, folders, backups, logs. */
App.register('tools', (() => {
  const { h, icon } = App;
  let root, body, backups = null;

  function mount(r) {
    root = r;
    body = h('div');
    root.append(h('div.room.room-tools'), h('div.scroll', h('div.page-wrap',
      h('div.head', h('h1', 'Herramientas'), h('div.sub', 'Apps de la carpeta, reparaciones, respaldos')), body)));
  }

  async function show() {
    try { backups = await Bridge.call('backups.list'); } catch (e) { backups = { qb: [], save: [] }; }
    render();
  }

  async function run(id, confirmText) {
    if (confirmText && !await App.confirm(confirmText[0], confirmText[1], confirmText[2] || 'Continuar')) return;
    try {
      const r = await Bridge.call('tools.run', { id });
      if (r && r.created) App.toast('Acceso directo creado', 'Está en tu escritorio.', 'ok');
    } catch (e) { App.fail(e); }
  }

  const tool = (ic, title, desc, id, opts = {}) =>
    h('button.tool', { onclick: () => run(id, opts.confirm) }, icon(ic), h('div', h('b', title, opts.admin ? h('span.adm', 'ADMIN') : null), h('span', desc)));

  async function restore(kind, b) {
    const what = kind === 'qb' ? (b.id === 'original' ? 'el setlist original (sin canciones importadas)' : 'el setlist del ' + b.id.replace('_', ' ')) : 'la partida guardada del ' + b.id.replace('_', ' ');
    if (!await App.confirm('¿Restaurar?', `Se restaurará ${what}. Antes se respalda lo actual.` + (kind === 'qb' ? ' Las canciones importadas después de esa fecha dejarán de aparecer (sus archivos quedan en DATA).' : ''), 'Restaurar', 'danger')) return;
    try { backups = await Bridge.call('backups.restore', { kind, id: b.id }); App.toast('Restaurado', '', 'ok'); render(); }
    catch (e) { App.fail(e); }
  }

  function render() {
    const app = App.state.app || {};
    const bk = (kind, list) => list.length ? h('div.bk-list', list.slice(0, 8).map(b => h('div.bk',
      h('b', b.id === 'original' ? 'Original (antes del launcher)' : b.id.replace('_', ' · ').replace(/-/g, (m, i) => i > 10 ? ':' : '-')),
      h('span', (b.reason || (kind === 'save' ? 'partida guardada' : 'setlist')) + ' · ' + App.fmtSize(b.size)),
      h('button.btn.sm', { onclick: () => restore(kind, b) }, 'Restaurar')))) : h('p.faint', 'Todavía no hay respaldos: se crean solos antes de instalar o eliminar canciones.');

    // what this folder has (BetterGH3 brings the scripts and the online account link)
    const has = app.tools || {};
    body.replaceChildren(
      h('div.section-title', 'Apps incluidas'),
      h('div.tool-grid',
        has.honeycombGui && tool('songs', 'Honeycomb', 'El compilador de canciones que usa el launcher, con todas sus opciones avanzadas (necesita .NET 8 Desktop Runtime).', 'honeycomb'),
        has.x360ce && tool('controls', 'x360ce', 'Donde se configuran tus guitarras y controles: sus botones y quién es PAD 1, 2, 3 o 4.', 'x360ce'),
        has.onlineAccount && tool('home', 'Cuenta online', 'Crea tu cuenta para jugar en línea (Warehouse).', 'online-account'),
        tool('import', 'Honeycomb en GitHub', 'Página de descargas de Honeycomb.', 'honeycomb-web')),
      h('div.section-title', { style: { marginTop: '26px' } }, 'Reparar e instalar'),
      h('div.tool-grid',
        has.fixes && tool('warn', 'Arreglar problemas de inicio', 'Borra la config gráfica, repara el registro, instala DirectX 9 y VC++ x86.', 'fix-startup',
          { admin: true, confirm: ['Arreglar problemas de inicio', 'Este script de BetterGH3 borra tu configuración gráfica (resolución, etc.), cierra la sesión online, pone el juego en inglés y abre los instaladores de DirectX y Visual C++. Tu partida guardada no se toca.', 'Ejecutar'] }),
        has.fixes && tool('check', 'Permitir en el Firewall', 'Reglas para GH3.exe y el puerto 3074 (online).', 'firewall', { admin: true }),
        tool('play', 'Acceso directo del launcher', 'Crea "Guitar Hero III Launcher" en tu escritorio.', 'launcher-shortcut'),
        has.fixes && tool('file', 'Acceso directo de GH3', 'El script original: acceso directo a GH3.exe (sin launcher).', 'gh3-shortcut', { admin: true }),
        h('button.tool', { onclick: async () => { try { App.toast('Cargando…'); await Bridge.call('assets.ensure'); location.reload(); } catch (e) { App.fail(e); } } },
          icon('refresh'), h('div', h('b', 'Arte y sonidos del juego'), h('span', 'Vuelve a leer el arte, el video de carga y los sonidos de tu copia del juego.'))),
        h('button.tool', { 'data-sfx': 'back', onclick: () => run('uninstall') },
          icon('trash'), h('div', h('b', 'Desinstalar GH3 Launcher'), h('span', 'Quita el launcher y lo que agregó su instalador. El juego, tus partidas y tus canciones se quedan.')))),
      h('div.section-title', { style: { marginTop: '26px' } }, 'Carpetas'),
      h('div.tool-grid',
        tool('folder', 'Juego', app.root || '', 'folder:game'),
        tool('folder', 'Partidas guardadas', 'Documentos\\Aspyr\\Guitar Hero III\\Save', 'folder:save'),
        tool('folder', 'Charts (DATA\\SONGS)', 'Un _song.pak.xen por canción.', 'folder:songs'),
        tool('folder', 'Audio (DATA\\MUSIC)', 'Un .fsb.xen por canción.', 'folder:music'),
        tool('folder', 'Plugins', 'PLUGINS y PLUGINS\\Disabled.', 'folder:plugins'),
        tool('folder', 'Respaldos', 'Setlist y partidas que guardó el launcher.', 'folder:backups')),
      h('div.opt-grid', { style: { marginTop: '26px' } },
        h('div.card', h('h2', 'Respaldos del setlist'), bk('qb', (backups && backups.qb) || [])),
        h('div.card', h('h2', 'Respaldos de la partida'), bk('save', (backups && backups.save) || []),
          h('button.btn.sm', { style: { marginTop: '10px' }, onclick: async () => { try { backups = await Bridge.call('backups.saveNow'); App.toast('Partida respaldada', '', 'ok'); render(); } catch (e) { App.fail(e); } } }, icon('plus'), 'Respaldar ahora'))),
      h('div.card', { style: { marginTop: '14px' } }, h('h2', 'Acerca de'),
        h('dl.kv', h('dt', 'Launcher'), h('dd', 'GH3 Launcher ' + (app.version || '')), h('dt', 'Carpeta'), h('dd', app.root || ''),
          h('dt', 'Registros'), h('dd', h('button.link', { onclick: () => run('log') }, 'launcher.log'), '  ·  ', h('button.link', { onclick: () => run('songtool-log') }, 'songtool.log'), '  ·  ', h('button.link', { onclick: () => run('folder:launcher') }, 'carpeta Launcher')))));
  }

  return { mount, show };
})());
