/* Start-up: the shell, the language and the menu sounds, the game's art and sounds (read from the
   player's copy on the first start), the first screen, then the data every screen shares. */
(async () => {
  App.init();
  await App.refreshApp();
  const app = App.state.app || {};
  I18N.start(app.language || 'en');
  Sfx.start();
  if (app.assetsReady === false && !sessionStorageGet('skipPrep')) { await prepare(); return; }
  App.go('home');   // always opens on Inicio
  App.refreshInput();
  refreshHealth();
  await App.refreshSongs();
  // menu music: what's on, in the nav, with a skip button
  const box = App.$('#bgm');
  App.$('#bgm-skip').addEventListener('click', () => BgMusic.skip());
  const pauseBtn = App.$('#bgm-pause');
  pauseBtn.addEventListener('click', () => BgMusic.pause(!BgMusic.info().paused));
  BgMusic.onChange(i => {
    box.hidden = !i.on || !i.song;
    box.classList.toggle('on', !!i.playing);
    pauseBtn.querySelector('use').setAttribute('href', i.paused ? '#i-play' : '#i-pause');
    pauseBtn.title = i.paused ? 'Seguir con la música' : 'Pausar la música';
    if (i.song) { App.$('#bgm-text').textContent = i.song.title + (i.song.artist ? ' · ' + i.song.artist : ''); box.title = 'Música del menú: ' + i.song.title + (i.song.artist ? ' de ' + i.song.artist : '') + ' (se apaga en Opciones › Launcher)'; }
  });
  BgMusic.start();
})();

/* what the launcher found missing or wrong in the game folder (Health.cs), for Inicio */
async function refreshHealth() {
  try { App.state.health = await Bridge.call('health.state'); App.emit('health', App.state.health); } catch (e) { }
}

function sessionStorageGet(k) { try { return sessionStorage.getItem(k); } catch (e) { return null; } }

/* First start: the launcher reads the game's art, loading movie and menu sounds (GameAssets.cs).
   The page waits for it, then loads again with them. */
function prepare() {
  const ov = App.$('#prep-overlay'), bar = App.$('#prep-bar'), acts = App.$('#prep-actions');
  ov.hidden = false;
  let done = false;
  const finish = () => { if (!done) { done = true; location.reload(); } };
  const fail = msg => {
    App.$('#prep-title').textContent = 'No se pudo preparar el arte del juego';
    App.$('#prep-sub').textContent = msg || '';
    acts.hidden = false;
    acts.replaceChildren(
      App.h('button.btn.primary', { onclick: async () => {
        acts.hidden = true;
        App.$('#prep-title').textContent = 'Preparando el launcher';
        App.$('#prep-sub').textContent = 'Leyendo el arte y los sonidos de tu copia de Guitar Hero III (solo la primera vez)…';
        try { if (await Bridge.call('assets.ensure')) finish(); } catch (e) { fail(e.message); }
      } }, 'Reintentar'),
      App.h('button.btn.ghost', { onclick: () => { try { sessionStorage.setItem('skipPrep', '1'); } catch (e) { } location.reload(); } }, 'Seguir sin arte'));
  };
  Bridge.on('assets.progress', p => {
    bar.style.width = Math.round((p.pct || 0) * 100) + '%';
    if (p.stage === 'error') fail(p.error);
  });
  Bridge.on('assets.ready', finish);
  // in case it ended before this page listened: ask now and then
  const poll = setInterval(async () => {
    try {
      const a = await Bridge.call('app.state');
      if (a.assetsReady) { clearInterval(poll); finish(); }
      else if (a.assetsError && acts.hidden) fail(a.assetsError);
    } catch (e) { }
  }, 2000);
  return new Promise(() => { });   // the page reloads when it is ready
}
