/* One player for the whole launcher. A song is one or more stems (Clone Hero's guitar/rhythm/song
   files, or the game's FSB stems); each stem is its own <audio>, started together and kept in step
   with the first one. Streaming <audio> keeps memory low even for 9-stem songs. */
const Player = (() => {
  let els = [], key = null, token = 0, timer = 0, loading = false;
  const subs = [];
  let vol = 0.8;
  try { const v = parseFloat(localStorage.getItem('volume')); if (!isNaN(v)) vol = v; } catch (e) { }

  const master = () => els.reduce((a, b) => (b.duration || 0) > (a.duration || 0) ? b : a, els[0]);
  function info() {
    const m = els.length ? master() : null;
    return { key, loading, playing: !!m && !m.paused && !loading, pos: m ? m.currentTime * 1000 : 0, dur: m && isFinite(m.duration) ? m.duration * 1000 : 0, volume: vol };
  }
  function notify() { const i = info(); subs.forEach(fn => { try { fn(i); } catch (e) { console.error(e); } }); }
  const once = (a, ev) => new Promise(r => { a.addEventListener(ev, r, { once: true }); a.addEventListener('error', r, { once: true }); });

  async function play(k, srcs, startMs = 0) {
    stop(true);
    const my = ++token;
    key = k; loading = true; notify();
    els = srcs.filter(Boolean).map(src => { const a = new Audio(); a.preload = 'auto'; a.volume = vol; a.src = src; return a; });
    if (!els.length) { loading = false; key = null; notify(); throw new Error('Esta canción no tiene audio'); }
    await Promise.all(els.map(a => a.readyState >= 1 ? 0 : once(a, 'loadedmetadata')));
    if (my !== token) return;
    const t = Math.max(0, startMs / 1000);
    els.forEach(a => { try { if (isFinite(a.duration) && t < a.duration - 1) a.currentTime = t; } catch (e) { } });
    await Promise.all(els.map(a => a.readyState >= 3 ? 0 : once(a, 'canplay')));
    if (my !== token) return;
    await Promise.all(els.map(a => a.play().catch(() => { })));
    if (my !== token) return;
    loading = false;
    const m = master();
    m.addEventListener('ended', () => { if (my === token) { els.forEach(a => a.pause()); notify(); } });
    clearInterval(timer);
    let n = 0;
    timer = setInterval(() => {
      if (my !== token) return;
      if (++n % 5 === 0 && els.length > 1 && !m.paused) els.forEach(a => { if (a !== m && !a.ended && Math.abs(a.currentTime - m.currentTime) > 0.06) a.currentTime = m.currentTime; });
      notify();
    }, 100);
    notify();
  }

  function toggle() {
    if (!els.length || loading) return;
    const m = master();
    if (m.paused) { if (m.ended) els.forEach(a => a.currentTime = 0); els.forEach(a => a.play().catch(() => { })); }
    else els.forEach(a => a.pause());
    notify();
  }
  function seek(ms) {
    if (!els.length) return;
    els.forEach(a => { try { a.currentTime = Math.max(0, ms / 1000); } catch (e) { } });
    notify();
  }
  function stop(silent) {
    token++;
    clearInterval(timer);
    els.forEach(a => { a.pause(); a.removeAttribute('src'); a.load(); });
    els = []; key = null; loading = false;
    if (!silent) notify();
  }
  function volume(v) {
    vol = Math.max(0, Math.min(1, v));
    els.forEach(a => a.volume = vol);
    try { localStorage.setItem('volume', String(vol)); } catch (e) { }
    notify();
  }

  return { play, toggle, seek, stop, volume, info, onChange: fn => subs.push(fn) };
})();
