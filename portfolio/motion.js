/* ============================================================================
   DESK 42 — motion engine
   Scroll reveals (rise / pop / scene sheen) · count-up · stat bars · parallax.
   Dependency-free. Honours prefers-reduced-motion.
   ============================================================================ */
(function () {
  'use strict';
  var root = document.documentElement;
  root.classList.add('has-js');
  var REDUCE = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var $$ = function (s, c) { return Array.prototype.slice.call((c || document).querySelectorAll(s)); };

  /* ── Reveals (robust: IO + scroll-sweep + safety net) ────────────────── */
  (function reveal() {
    var els = $$('[data-reveal], [data-rise], [data-pop], [data-stagger], .scene');
    if (!els.length) return;
    function show(e) { e.classList.add('in'); }
    if (REDUCE) { els.forEach(show); return; }

    var pending = els.slice();
    function sweep() {
      var vh = window.innerHeight || document.documentElement.clientHeight;
      pending = pending.filter(function (e) {
        var r = e.getBoundingClientRect();
        if (r.top < vh * 0.92 && r.bottom > 0) { show(e); return false; }
        return true;
      });
    }
    if ('IntersectionObserver' in window) {
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (en) { if (en.isIntersecting) { show(en.target); io.unobserve(en.target); } });
      }, { rootMargin: '0px 0px -8% 0px', threshold: 0 });
      els.forEach(function (e) { io.observe(e); });
    }
    sweep();
    window.addEventListener('scroll', sweep, { passive: true });
    window.addEventListener('resize', sweep);
    // never leave content hidden if observers/scroll never fire
    setTimeout(function () { els.forEach(show); }, 5000);
  })();

  /* ── Count-up numbers ────────────────────────────────────────────────── */
  (function countUp() {
    var nums = $$('[data-count]');
    if (!nums.length) return;
    function run(el) {
      var target = parseFloat(el.getAttribute('data-count')), suffix = el.getAttribute('data-suffix') || '';
      if (REDUCE) { el.textContent = target + suffix; return; }
      var dur = 1200, t0 = null;
      function step(ts) { if (!t0) t0 = ts; var p = Math.min(1, (ts - t0) / dur), e = 1 - Math.pow(1 - p, 3);
        el.textContent = Math.round(target * e) + suffix; if (p < 1) requestAnimationFrame(step); }
      requestAnimationFrame(step);
    }
    if (!('IntersectionObserver' in window)) { nums.forEach(run); return; }
    var io = new IntersectionObserver(function (es) { es.forEach(function (e) { if (e.isIntersecting) { run(e.target); io.unobserve(e.target); } }); }, { threshold: 0.6 });
    nums.forEach(function (n) { io.observe(n); });
  })();

  /* ── RPG stat bars ───────────────────────────────────────────────────── */
  (function statbars() {
    var fills = $$('[data-fill]'); if (!fills.length) return;
    function fill(el) { el.style.width = Math.max(0, Math.min(100, parseFloat(el.getAttribute('data-fill')))) + '%'; }
    if (REDUCE || !('IntersectionObserver' in window)) { fills.forEach(fill); return; }
    var io = new IntersectionObserver(function (es) { es.forEach(function (e) { if (e.isIntersecting) { fill(e.target); io.unobserve(e.target); } }); }, { threshold: 0.4 });
    fills.forEach(function (f) { io.observe(f); });
  })();

  /* ── Parallax (gentle, scroll-driven) ────────────────────────────────── */
  (function parallax() {
    var els = $$('[data-parallax]'); if (!els.length || REDUCE) return;
    var ticking = false;
    function frame() {
      var vh = window.innerHeight;
      els.forEach(function (el) {
        var r = el.getBoundingClientRect(), centre = r.top + r.height / 2, off = (centre - vh / 2) / vh;
        var f = parseFloat(el.getAttribute('data-parallax')) || 0.1;
        el.style.transform = 'translate3d(0,' + (off * f * -100).toFixed(1) + 'px,0)';
      });
      ticking = false;
    }
    window.addEventListener('scroll', function () { if (!ticking) { ticking = true; requestAnimationFrame(frame); } }, { passive: true });
    window.addEventListener('resize', frame); frame();
  })();
})();
