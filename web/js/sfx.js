/* GH3's menu sounds in the launcher, the game's own (read from the player's copy on the first start,
   assets\sfx\): ui_sfx_select when something is chosen (one of three, like the game), the "back"
   ones when something is cancelled or closed, ui_sfx_scroll + _add when the pointer moves over the
   menu, Menu_Warning when something goes wrong. Launcher settings: uiSounds (on/off),
   uiSoundsVolume (0-1). */
const Sfx = (() => {
  // the game's relative volumes (its "vol" of each sound) as gains
  const SETS = {
    select: [['ui_sound_01', 0.75], ['ui_sound_05', 1], ['ui_sound_06', 1]],
    back: [['ui_sound_09', 1], ['ui_sound_10', 0.5]],
    scroll: [['scroll', 0.55], ['ui_scroll_add2', 0.35]],
    warning: [['menu_warning_01', 0.7]]
  };
  const base = {};
  const last = {};
  let lastSelect = -1;

  const cfg = () => {
    const L = (App.state.app && App.state.app.launcher) || {};
    return { on: L.uiSounds !== false, volume: typeof L.uiSoundsVolume === 'number' ? L.uiSoundsVolume : 0.5 };
  };

  function load() {
    for (const set of Object.values(SETS))
      for (const [name] of set)
        if (!base[name]) { const a = new Audio('assets/sfx/' + name + '.wav'); a.preload = 'auto'; base[name] = a; }
  }

  function sound(name, gain) {
    const b = base[name];
    if (!b || b.error) return;
    const a = b.cloneNode();
    a.volume = Math.max(0, Math.min(1, cfg().volume * gain));
    a.play().catch(() => { });
  }

  function play(kind) {
    const c = cfg();
    if (!c.on || !SETS[kind]) return;
    const now = performance.now();
    if (now - (last[kind] || 0) < (kind === 'scroll' ? 70 : 40)) return;   // not a machine gun
    last[kind] = now;
    if (!Object.keys(base).length) load();
    if (kind === 'scroll') { SETS.scroll.forEach(([n, g]) => sound(n, g)); return; }
    let i = Math.floor(Math.random() * SETS[kind].length);
    if (kind === 'select' && SETS.select.length > 1 && i === lastSelect) i = (i + 1) % SETS.select.length;   // like the game: not the same twice
    if (kind === 'select') lastSelect = i;
    sound(...SETS[kind][i]);
  }

  // what counts as "going back": closing, cancelling, discarding
  const BACK = /^(cancelar|cancel|cerrar|close|salir|exit|descartar|discard|no por ahora|not now|quitar|remove|×)$/i;
  const CLICKABLE = 'button, .tab, .song, .qi.pickable, .mode-card, .style-opt, .tool, summary, label.toggle, .seg button, .recent button, [data-go]';
  const HOVER = '#nav .menu button, .tab, .mode-card, .style-opt, .tool, .dev, .seg button';

  function start() {
    load();
    document.addEventListener('click', e => {
      const el = e.target.closest(CLICKABLE);
      if (!el || el.disabled || el.matches('[data-sfx="none"]')) return;
      if (el.dataset.sfx) return play(el.dataset.sfx);
      const label = (el.textContent || '').trim();
      play(BACK.test(label) || el.closest('.modal-back') && el.matches('.ghost') && BACK.test(label) ? 'back' : 'select');
    }, true);
    // the pointer moving onto another menu entry, like moving the highlight in the game
    let over = null;
    document.addEventListener('pointerover', e => {
      const el = e.target.closest(HOVER);
      if (!el || el === over || el.disabled) return;
      over = el;
      play('scroll');
    });
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && document.querySelector('.modal-back')) play('back'); }, true);
  }

  return { start, play };
})();
