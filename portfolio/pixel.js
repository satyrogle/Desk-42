/* ============================================================================
   DESK 42 — game chrome
   "PRESS START" boot curtain · operator HUD (sanity drains on scroll) ·
   achievement toasts. No CRT flicker, no canvas. Honours reduced-motion.
   ============================================================================ */
(function () {
  'use strict';
  var REDUCE = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var $ = function (s, c) { return (c || document).querySelector(s); };

  /* ── Boot · machine-entry ritual (diagnostic sequence → press start) ──── */
  (function boot() {
    var el = $('.boot');
    if (!el || el.hasAttribute('hidden')) return;
    var logEl = $('.boot__log', el), prompt = $('.boot__prompt', el), skip = $('.boot__skip', el);
    var dismissed = false;
    document.body.classList.add('booting');

    var LINES = [
      ['> POWER ............... ', 'ok', 'ON'],
      ['> RUMOR MILL BUS ...... ', 'ok', 'ONLINE'],
      ['> STATE MACHINE ....... ', 'ok', 'CALIBRATED'],
      ['> BEHAVIOUR TREES ..... ', 'ok', '8 NODES'],
      ['> ADAPTIVE TIDE ....... ', 'warn', 'RISING'],
      ['> SOUL INTEGRITY ...... ', 'ok', '100%'],
      ['> ENTITY 42 ........... ', 'err', 'AWAKE']
    ];
    function ready() { if (prompt) prompt.classList.add('show'); try { sessionStorage.setItem('desk42.seen', '1'); } catch (e) {} }
    function paint(done) {
      if (!logEl) return;
      logEl.innerHTML = LINES.slice(0, done).map(function (l) {
        return '<span class="dim">' + l[0] + '</span><span class="' + l[1] + '">' + l[2] + '</span>';
      }).join('\n');
    }
    if (REDUCE || !logEl) { paint(LINES.length); ready(); }
    else {
      var i = 0;
      (function tick() {
        if (dismissed) return;
        if (i >= LINES.length) { ready(); return; }
        i++; paint(i); setTimeout(tick, i === LINES.length ? 360 : 230);
      })();
    }

    function dismiss() {
      if (dismissed) return; dismissed = true;
      try { sessionStorage.setItem('desk42.seen', '1'); } catch (e) {}   // persist on any dismissal
      el.classList.add('is-out');
      setTimeout(function () { el.setAttribute('hidden', ''); document.body.classList.remove('booting');
        if (window.__desk42 && window.__desk42.toast) window.__desk42.toast('SYSTEM', 'OPERATOR CLOCKED IN'); }, REDUCE ? 0 : 760);
    }
    el.addEventListener('click', function (e) { if (e.target === skip) return; dismiss(); });
    if (skip) skip.addEventListener('click', dismiss);
    document.addEventListener('keydown', function (e) { if (el.hasAttribute('hidden')) return; if (e.key === 'Enter' || e.key === ' ' || e.key === 'Escape') { e.preventDefault(); dismiss(); } });
  })();

  /* ── Operator HUD — sanity drains as you descend ─────────────────────── */
  (function hud() {
    var hudEl = $('.hud'); if (!hudEl) return;
    var soul = $('.hud__bar--soul i', hudEl), san = $('.hud__bar--sanity i', hudEl), clr = $('.hud__bar--clear i', hudEl);
    var sanV = $('[data-sanity]', hudEl), depthV = $('[data-depth]', hudEl);
    function update() {
      var doc = document.documentElement, max = (doc.scrollHeight - window.innerHeight) || 1;
      var prog = Math.min(1, Math.max(0, window.scrollY / max));
      var sanity = Math.round(100 - prog * 88), soulV = Math.round(100 - prog * 40);
      if (soul) soul.style.width = soulV + '%';
      if (san) san.style.width = sanity + '%';
      if (clr) clr.style.width = (40 + Math.round(prog * 35)) + '%';
      if (sanV) sanV.textContent = sanity + '%';
      if (depthV) depthV.textContent = Math.round(prog * 100) + '%';
      hudEl.classList.toggle('low', sanity < 35);
    }
    var ticking = false;
    window.addEventListener('scroll', function () { if (!ticking) { ticking = true; requestAnimationFrame(function () { update(); ticking = false; }); } }, { passive: true });
    window.addEventListener('resize', update); update();
  })();

  /* ── Achievement toasts ──────────────────────────────────────────────── */
  (function toasts() {
    var host = $('.toasts'); if (!host) return;
    var ICON = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2l3 6 6 .9-4.5 4.3 1 6.3L12 17l-5.5 2.5 1-6.3L3 8.9 9 8z"/></svg>';
    function toast(kicker, label) {
      var t = document.createElement('div'); t.className = 'toast'; t.setAttribute('role', 'status');
      t.innerHTML = '<span class="toast__icon" aria-hidden="true">' + ICON + '</span><span><span class="toast__kicker">' + kicker + '</span><span class="toast__label">' + label + '</span></span>';
      host.appendChild(t);
      setTimeout(function () { t.classList.add('out'); setTimeout(function () { t.remove(); }, 360); }, 3200);
    }
    window.__desk42 = window.__desk42 || {}; window.__desk42.toast = toast;
    var MAP = { metrics: ['READOUT', 'METRICS SYNCED'], glance: ['DOCTRINE', 'DIRECTIVES FILED'], map: ['ARCHIVE', 'ROSTER UNLOCKED'], systems: ['CORE', 'SYSTEMS ONLINE'], practice: ['AUDIT', 'INSPECTION PASSED'], cross: ['DOSSIER', 'CROSS-REFERENCE LOGGED'] };
    if (!('IntersectionObserver' in window)) return;
    var fired = {};
    var io = new IntersectionObserver(function (es) { es.forEach(function (e) { var id = e.target.id; if (e.isIntersecting && MAP[id] && !fired[id]) { fired[id] = true; toast(MAP[id][0], MAP[id][1]); } }); }, { threshold: 0.4 });
    Object.keys(MAP).forEach(function (id) { var s = document.getElementById(id); if (s) io.observe(s); });
  })();

  /* ── Pause continuous animation when off-screen / tab hidden (perf) ───── */
  (function pauseOffscreen() {
    var stage = $('.stage');
    if (stage && 'IntersectionObserver' in window) {
      var io = new IntersectionObserver(function (es) {
        es.forEach(function (e) { stage.classList.toggle('paused', !e.isIntersecting); });
      }, { threshold: 0 });
      io.observe(stage);
    }
    document.addEventListener('visibilitychange', function () {
      document.body.classList.toggle('tab-hidden', document.hidden);
    });
  })();

  /* ── Control-room console — select a station to drive the main readout ── */
  (function controlRoom() {
    var list = $('.controlroom__rail[role="tablist"]'); if (!list) return;
    var tabs = Array.prototype.slice.call(list.querySelectorAll('.station[role="tab"]'));
    function select(tab, focus) {
      tabs.forEach(function (t) {
        var on = t === tab;
        t.setAttribute('aria-selected', String(on));
        t.tabIndex = on ? 0 : -1;
        var panel = document.getElementById(t.getAttribute('aria-controls'));
        if (panel) { if (on) panel.removeAttribute('hidden'); else panel.setAttribute('hidden', ''); }
      });
      if (focus) tab.focus();
    }
    tabs.forEach(function (tab, i) {
      tab.addEventListener('click', function () { select(tab); });
      tab.addEventListener('keydown', function (e) {
        var k = e.key, n = null;
        if (k === 'ArrowDown' || k === 'ArrowRight') n = tabs[(i + 1) % tabs.length];
        else if (k === 'ArrowUp' || k === 'ArrowLeft') n = tabs[(i - 1 + tabs.length) % tabs.length];
        else if (k === 'Home') n = tabs[0];
        else if (k === 'End') n = tabs[tabs.length - 1];
        if (n) { e.preventDefault(); select(n, true); }
      });
    });
  })();
})();
