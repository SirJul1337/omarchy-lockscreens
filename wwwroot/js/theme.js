/* Site behaviour, ported from omarchy.org's shape:
   - the theme is one [data-theme] attribute on <html>; the stylesheet holds
     every palette, so switching is a single attribute write
   - a picker in the bar, remembered in localStorage
   - the pixel field behind the hero and the footer
   - the sticky bar's hairline, and the mobile drawer */

(function () {
  'use strict';

  /* --------------------------------------------------------------- themes */

  // key, label, swatches (brand / text / surface-2) — palettes live in site.css.
  var THEMES = [
    ['tokyo-night', 'Tokyo Night', ['#9ece6a', '#c0caf5', '#24283b']],
    ['catppuccin', 'Catppuccin', ['#89b4fa', '#cdd6f4', '#313244']],
    ['catppuccin-latte', 'Catppuccin Latte', ['#1e66f5', '#4c4f69', '#e3e4e8']],
    ['everforest', 'Everforest', ['#7fbbb3', '#d3c6aa', '#343f44']],
    ['gruvbox', 'Gruvbox', ['#7daea3', '#d4be98', '#3c3836']],
    ['kanagawa', 'Kanagawa', ['#dcd7ba', '#dcd7ba', '#223249']],
    ['matte-black', 'Matte Black', ['#e68e0d', '#eaeaea', '#1e1e1e']],
    ['nord', 'Nord', ['#81a1c1', '#d8dee9', '#3b4252']],
    ['osaka-jade', 'Osaka Jade', ['#509475', '#f7e8b2', '#23372b']],
    ['ristretto', 'Ristretto', ['#f38d70', '#e6d9db', '#3d2f2a']],
    ['rose-pine', 'Rosé Pine', ['#56949f', '#575279', '#ede7e1']],
    ['white', 'White', ['#6e6e6e', '#000000', '#f5f5f5']]
  ];

  var STORE = 'omarchy-theme';
  // Hoisted: applyTheme needs to know whether the wipe started from the picker.
  var picker = null;
  var DEFAULT = 'tokyo-night';
  var root = document.documentElement;
  var reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

  function known(key) {
    for (var i = 0; i < THEMES.length; i++) if (THEMES[i][0] === key) return true;
    return false;
  }

  /* The tab icon follows the theme too. Browsers cache a favicon by element,
     so the link is replaced rather than edited -- the same move omarchy.org
     makes in its paintFavicon. */
  function paintFavicon() {
    var accent = getComputedStyle(root).getPropertyValue('--accent').trim();
    if (!accent || !window.MARK_PATHS) return;
    var svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="-50 -50 800 800">' +
      '<g fill="' + accent + '" shape-rendering="crispEdges">' + window.MARK_PATHS + '</g></svg>';
    var link = document.createElement('link');
    link.rel = 'icon';
    link.type = 'image/svg+xml';
    link.setAttribute('data-theme-icon', '');
    link.href = 'data:image/svg+xml,' + encodeURIComponent(svg);
    var old = document.querySelectorAll('link[rel="icon"][data-theme-icon]');
    for (var i = 0; i < old.length; i++) old[i].parentNode.removeChild(old[i]);
    document.head.appendChild(link);
  }

  /* A theme change goes through the View Transitions API so the swap happens
     behind omarchy.org's split wipe (see theme-transition.css). Without the
     API, and for anyone who asked for less motion, it lands in one frame --
     which is also why the old no-transitions class is still here: without a
     wipe to hide it, letting every colour tween independently smears the swap. */
  function swapTheme(key) {
    root.classList.add('no-transitions');
    root.dataset.theme = key;
    try { localStorage.setItem(STORE, key); } catch (e) {}
    requestAnimationFrame(function () {
      requestAnimationFrame(function () { root.classList.remove('no-transitions'); });
    });
  }

  function applyTheme(key, quiet) {
    if (!known(key)) key = DEFAULT;
    var announce = function () {
      paintFavicon();
      if (!quiet) window.dispatchEvent(new CustomEvent('themechange'));
    };

    if (quiet || reduced || typeof document.startViewTransition !== 'function') {
      swapTheme(key);
      announce();
      return;
    }

    // Frosted only when the wipe starts from the picker, where the page is
    // already behind that blur.
    var fromPicker = picker && picker.classList.contains('open');
    if (fromPicker) root.classList.add('theme-wipe-frosted');
    var clear = function () { root.classList.remove('theme-wipe-frosted'); };

    try {
      var vt = document.startViewTransition(function () { swapTheme(key); });
      if (vt && vt.finished) vt.finished.then(clear, clear); else clear();
      // The field re-reads its tokens as soon as the swap lands, not when the
      // wipe finishes, or it would repaint in the old colours behind the slit.
      if (vt && vt.updateCallbackDone) vt.updateCallbackDone.then(announce, announce);
      else announce();
    } catch (e) {
      swapTheme(key);
      clear();
      announce();
    }
  }

  function buildPicker() {
    var modal = document.getElementById('themeMenu');
    var stage = document.getElementById('themeStage');
    var nameEl = document.getElementById('themeName');
    picker = document.getElementById('themePicker');
    var btn = document.getElementById('themeBtn');
    if (!modal || !stage || !nameEl || !picker || !btn) return;

    var at = 0;   // which theme the coverflow is showing

    THEMES.forEach(function (t, i) {
      var card = document.createElement('button');
      card.type = 'button';
      card.className = 'picker-card picker-item';
      card.dataset.key = t[0];
      // The card carries its own theme, so everything inside comes out in that
      // palette without touching the page.
      card.dataset.theme = t[0];
      card.setAttribute('aria-label', 'Use ' + t[1]);
      card.innerHTML =
        '<div class="frame"><div class="inner">' +
          '<div class="mark" role="img" aria-hidden="true"></div>' +
          '<div class="swatches">' + t[2].map(function (c) {
            return '<i style="background:' + c + '"></i>';
          }).join('') + '</div>' +
          '<div class="chip">' + t[0].replace(/-/g, ' ') + '</div>' +
        '</div></div>';
      card.addEventListener('click', function () {
        if (i === at) { applyTheme(t[0]); setOpen(false); }
        else { at = i; layout(); }
      });
      stage.appendChild(card);
    });

    var cards = Array.prototype.slice.call(stage.querySelectorAll('.picker-card'));

    /* Place every card relative to the one in the middle. Anything more than
       two away is parked out of sight rather than removed, so moving through
       the list never rebuilds the DOM. */
    function layout() {
      for (var i = 0; i < cards.length; i++) {
        var d = i - at;
        if (d < -2 || d > 2) {
          cards[i].dataset.far = '';
          cards[i].removeAttribute('data-at');
          cards[i].tabIndex = -1;
        } else {
          cards[i].removeAttribute('data-far');
          cards[i].dataset.at = String(d);
          cards[i].tabIndex = d === 0 ? 0 : -1;
        }
      }
      nameEl.textContent = THEMES[at][1];
      if (modal.hidden === false) cards[at].focus({ preventScroll: true });
    }

    function move(delta) {
      at = Math.max(0, Math.min(THEMES.length - 1, at + delta));
      layout();
    }

    function setOpen(open) {
      modal.hidden = !open;
      picker.classList.toggle('open', open);
      btn.setAttribute('aria-expanded', String(open));
      document.documentElement.style.overflow = open ? 'hidden' : '';
      if (open) {
        // Open on whatever is currently in use, not on the first card.
        for (var i = 0; i < THEMES.length; i++)
          if (THEMES[i][0] === root.dataset.theme) at = i;
        layout();
      } else {
        btn.focus();
      }
    }

    btn.addEventListener('click', function (e) { e.stopPropagation(); setOpen(modal.hidden); });
    modal.querySelector('.picker-dim').addEventListener('click', function () { setOpen(false); });

    /* omarchy.org's keys: T anywhere opens it, the arrows walk the coverflow,
       Enter takes the one in the middle, Escape closes. Typing in a field is
       left alone -- searching for "tokyo" should not swap the theme. */
    document.addEventListener('keydown', function (e) {
      var open = !modal.hidden;

      if (e.key === 'Escape' && open) { e.preventDefault(); setOpen(false); return; }

      var el = document.activeElement;
      var typing = el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' ||
                          el.tagName === 'SELECT' || el.isContentEditable);
      if (!typing && !e.metaKey && !e.ctrlKey && !e.altKey &&
          e.key && e.key.toLowerCase() === 't') {
        e.preventDefault();
        setOpen(!open);
        return;
      }

      if (!open) return;
      if (e.key === 'ArrowRight' || e.key === 'ArrowDown') { e.preventDefault(); move(1); }
      else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') { e.preventDefault(); move(-1); }
      else if (e.key === 'Home') { e.preventDefault(); at = 0; layout(); }
      else if (e.key === 'End') { e.preventDefault(); at = THEMES.length - 1; layout(); }
      else if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        applyTheme(THEMES[at][0]);
        setOpen(false);
      }
    });

    layout();
  }

  /* ---------------------------------------------------------- pixel field */

  /* omarchy.org's hero field, ported from its HeroPixelField.tsx rather than
     approximated. The shape of it matters more than any single constant:

       - one 128x128 field of value noise: white noise put through two box
         passes so it comes out as soft blobs, then stretched back to 0..1
       - every cell reads that noise twice, at two scales drifting in
         different directions, so the texture never visibly repeats
       - luminance is quantised through an 8x8 Bayer matrix with a fixed
         per-cell jitter mixed in. Pure Bayer at this density lights the same
         low-index cells everywhere and reads as a regular lattice
       - the resting field is entirely --t-field-dim. mid and lit are earned
         by the cursor, never handed out at random

     That last point is what a scatter of random shades gets wrong: there,
     brightness is noise; here it is a response. */

  var BAYER = [
    0, 32, 8, 40, 2, 34, 10, 42, 48, 16, 56, 24, 50, 18, 58, 26, 12, 44, 4, 36,
    14, 46, 6, 38, 60, 28, 52, 20, 62, 30, 54, 22, 3, 35, 11, 43, 1, 33, 9, 41,
    51, 19, 59, 27, 49, 17, 57, 25, 15, 47, 7, 39, 13, 45, 5, 37, 63, 31, 55, 23,
    61, 29, 53, 21
  ];

  var NOISE_SIZE = 128;
  var CELLS_PER_NOISE = 9;   // grid cells per unit of noise: how big the blobs read
  var CURSOR_CELLS = 12;     // cursor reach, in cells
  var FIELD_DENSITY = 0.3;   // for fields that are not the hero

  function lcg(seed) {
    var state = seed >>> 0;
    return function () {
      state = (state * 1664525 + 1013904223) >>> 0;
      return state / 4294967296;
    };
  }

  function buildNoise(seed) {
    var size = NOISE_SIZE, random = lcg(seed), i;
    var field = new Float32Array(size * size);
    for (i = 0; i < field.length; i++) field[i] = random();

    for (var pass = 0; pass < 2; pass++) {
      var next = new Float32Array(size * size);
      for (var y = 0; y < size; y++) {
        for (var x = 0; x < size; x++) {
          var sum = 0;
          for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
              sum += field[(((y + dy + size) % size) * size) + ((x + dx + size) % size)];
          next[y * size + x] = sum / 9;
        }
      }
      field = next;
    }

    // Box blurring collapses the range, so stretch it back out.
    var min = Infinity, max = -Infinity;
    for (i = 0; i < field.length; i++) {
      if (field[i] < min) min = field[i];
      if (field[i] > max) max = field[i];
    }
    var span = (max - min) || 1;
    for (i = 0; i < field.length; i++) field[i] = (field[i] - min) / span;
    return field;
  }

  /* A fixed 64x64 tile of per-cell threshold offsets, tiled over the grid. */
  function buildJitter(seed) {
    var random = lcg(seed), tile = new Float32Array(64 * 64);
    for (var i = 0; i < tile.length; i++) tile[i] = random();
    return tile;
  }

  function sample(field, x, y) {
    var size = NOISE_SIZE;
    var xi = Math.floor(x), yi = Math.floor(y);
    var fx = x - xi, fy = y - yi;
    var x0 = ((xi % size) + size) % size, y0 = ((yi % size) + size) % size;
    var x1 = (x0 + 1) % size, y1 = (y0 + 1) % size;
    var sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
    var a = field[y0 * size + x0], b = field[y0 * size + x1];
    var c = field[y1 * size + x0], d = field[y1 * size + x1];
    return (a * (1 - sx) + b * sx) * (1 - sy) + (c * (1 - sx) + d * sx) * sy;
  }

  var NOISE = null, JITTER = null;

  function pixelField(host) {
    if (!NOISE) { NOISE = buildNoise(0x9e3779b9); JITTER = buildJitter(0x85ebca6b); }

    var canvas = document.createElement('canvas');
    host.prepend(canvas);
    var ctx = canvas.getContext('2d');
    var isHero = host.classList.contains('field-hero');
    var CELL = 7;                       // contiguous cells: the gaps are unlit cells
    var cols = 0, rows = 0, w = 0, h = 0, ramp = null;
    var dim = '#39482e', mid = '#678549', lit = '#9ece6a', ground = '#0e0e14';
    var pointer = { x: -1e9, y: -1e9 };
    var reach = CURSOR_CELLS * CELL;

    var palette = { dim: dim, mid: mid, lit: lit, hover: '#bbdd97', crest: '#daecc6' };

    function readColors() {
      var s = getComputedStyle(host);
      function token(name, fallback) { return s.getPropertyValue(name).trim() || fallback; }
      dim = palette.dim = token('--t-field-dim', '#39482e');
      mid = palette.mid = token('--t-field-mid', '#678549');
      lit = palette.lit = token('--t-field-lit', '#9ece6a');
      palette.hover = token('--t-field-hover', '#bbdd97');
      palette.crest = token('--t-field-crest', '#daecc6');
      ground = token('--t-field-bg', '#0e0e14');
    }

    /* How much field is allowed at each cell: nothing through an elliptical
       core, so the wordmark and the copy over it stay readable, climbing
       quadratically out to the edges and held back near the very top. */
    function buildRamp() {
      ramp = new Float32Array(cols * rows);
      for (var r = 0; r < rows; r++) {
        var y = (r + 0.5) * CELL;
        var ny = (y / h) * 2 - 1;
        var clear = isHero ? Math.min(1, Math.max(0.16, (y - 24) / 130)) : FIELD_DENSITY;
        for (var c = 0; c < cols; c++) {
          var x = (c + 0.5) * CELL;
          var nx = (x / w) * 2 - 1;
          var rr = Math.sqrt(nx * nx + ny * ny * 0.82);
          var eased = Math.min(1, Math.max(0, (rr - 0.42) / 0.85));
          ramp[r * cols + c] = (isHero ? eased * eased : 1) * clear;
        }
      }
    }

    function resize() {
      var box = host.getBoundingClientRect();
      if (!box.width || !box.height) return;
      w = box.width; h = box.height;
      var dpr = Math.min(window.devicePixelRatio || 1, 2);
      canvas.width = Math.ceil(w * dpr);
      canvas.height = Math.ceil(h * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      cols = Math.ceil(w / CELL);
      rows = Math.ceil(h / CELL);
      readColors();
      buildRamp();
    }

    function draw(time) {
      var t = reduced ? 0 : time / 1000;
      ctx.fillStyle = ground;
      ctx.fillRect(0, 0, w, h);

      for (var r = 0; r < rows; r++) {
        var cy = (r + 0.5) * CELL;
        for (var c = 0; c < cols; c++) {
          var shade = ramp[r * cols + c];
          var lum = 0;

          if (shade > 0.002) {
            var u = c / CELLS_PER_NOISE, v = r / CELLS_PER_NOISE;
            var base =
              0.6 * sample(NOISE, u + t * 0.14, v - t * 0.055) +
              0.4 * sample(NOISE, u * 0.55 - t * 0.08, v * 0.55 + t * 0.06);
            var twinkle =
              0.5 + 0.5 * Math.sin(t * 1.1 + JITTER[(r * 37 + c * 11) & 4095] * 6.283);
            lum = shade * (0.3 + 0.52 * base * base + 0.18 * twinkle) * 0.62;
          }

          var cx = (c + 0.5) * CELL;
          var dx = cx - pointer.x, dy = cy - pointer.y;
          var dist = Math.sqrt(dx * dx + dy * dy);
          var glow = 0;
          if (dist < reach) {
            var falloff = 1 - dist / reach;
            glow = falloff * falloff;
          }
          lum += glow * 0.6;

          var threshold =
            0.78 * ((BAYER[(r & 7) * 8 + (c & 7)] + 0.5) / 64) +
            0.22 * JITTER[(r & 63) * 64 + (c & 63)];
          if (lum <= threshold) continue;

          ctx.fillStyle = glow > 0.34 ? lit : glow > 0.1 ? mid : dim;
          ctx.fillRect(c * CELL, r * CELL, CELL, CELL);
        }
      }
    }

    var etch = null;
    var running = false;
    function frame(time) {
      draw(time);
      var etching = etch && etch.paint(time);
      if (running || etching) requestAnimationFrame(frame);
    }

    resize();
    draw(0);
    // Reduced motion keeps the word as it is: an effect that makes the word is
    // exactly the kind of thing that setting is asking not to happen.
    if (!reduced)
      etch = attachEtch(host, canvas, ctx, palette, function () {
        if (!running) requestAnimationFrame(frame);
      });
    window.addEventListener('resize', function () { resize(); if (reduced) draw(0); });
    window.addEventListener('themechange', function () { readColors(); if (reduced) draw(0); });

    host.addEventListener('pointermove', function (e) {
      var box = host.getBoundingClientRect();
      pointer.x = e.clientX - box.left;
      pointer.y = e.clientY - box.top;
    });
    host.addEventListener('pointerleave', function () { pointer.x = -1e9; pointer.y = -1e9; });

    if (!reduced) {
      // Only while it is on screen: a field scrolled past has no business
      // holding a frame loop open.
      var io = new IntersectionObserver(function (entries) {
        var visible = entries[0].isIntersecting;
        if (visible === running) return;
        running = visible;
        if (running) requestAnimationFrame(frame);
      }, { rootMargin: '120px' });
      io.observe(host);

    }
  }

  /* ----------------------------------------------------------------- etch */

  /* Click the wordmark and it gets drawn rather than shown, by ttfx -- the
     same engine omarchy.org uses, vendored in /ttfx (MIT, see its NOTICE).
     A different one of its effects plays each time, so this is not one
     animation but thirty-odd.

     The word and the field share one grid, so the effect's cells land exactly
     on the lattice the field already draws on, and the finished frame is the
     wordmark bitmap cell for cell. While it plays the CSS-mask word is
     hidden: what you see is the word being made, not a word you already had. */

  var ETCH_SETTLE_MS = 220;

  function attachEtch(host, canvas, ctx, palette, wake) {
    var word = host.querySelector('.page-wordmark');
    if (!word || !window.WORDMARK || !window.Etch) return null;

    var wm = window.WORDMARK;
    var run = null;      // the live ttfx session
    var box = null;      // where the word sits, in field pixels
    var safety = 0;
    var starting = false;

    function measure() {
      var wb = word.getBoundingClientRect(), hb = host.getBoundingClientRect();
      box = {
        x: wb.left - hb.left, y: wb.top - hb.top,
        cw: wb.width / wm.width, ch: wb.height / wm.height
      };
    }

    function stop() {
      clearTimeout(safety);
      if (run) { run.etch.free(); run = null; }
      word.style.visibility = '';
    }

    function start() {
      if (run || starting) return;
      starting = true;
      measure();
      word.style.visibility = 'hidden';
      // The engine is ~600 KB and only fetched on the first click, so the
      // word must not sit hidden if that never lands.
      clearTimeout(safety);
      safety = setTimeout(stop, 12000);

      var shades = [palette.crest, palette.hover, palette.lit, palette.mid, palette.dim];
      window.Etch.start(wm.rows, wm.width, wm.height, shades, 'random')
        .then(function (etch) {
          starting = false;
          run = { etch: etch, t0: 0, settledAt: 0 };
          clearTimeout(safety);
          safety = setTimeout(stop, 20000);
          if (wake) wake();
        })
        .catch(function (e) {
          starting = false;
          stop();
          console.warn('etch unavailable', e);
        });
    }

    /* ttfx picks its own colours, and they are not this theme's. What survives
       is the *shape* of them: how light or dark the effect wanted a cell to be.
       So each cell's luma chooses the nearest field colour, the way
       omarchy.org's themeInk does, and every pixel lands on-theme. */
    var inks = [];
    function buildInks() {
      inks = ['dim', 'mid', 'lit', 'hover', 'crest'].map(function (k) {
        var c = parseColour(palette[k]);
        return { css: palette[k], l: c ? 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2] : 128 };
      });
    }
    function parseColour(css) {
      var m = /^#([0-9a-f]{6})$/i.exec(String(css).trim());
      if (m) {
        var n = parseInt(m[1], 16);
        return [n >> 16, (n >> 8) & 255, n & 255];
      }
      m = /rgba?\(([^)]+)\)/.exec(String(css));
      if (m) {
        var p = m[1].split(',').map(parseFloat);
        return [p[0] | 0, p[1] | 0, p[2] | 0];
      }
      return null;
    }
    function themeInk(packed) {
      if (!inks.length) buildInks();
      var l = 0.2126 * (packed >> 16) + 0.7152 * ((packed >> 8) & 255) + 0.0722 * (packed & 255);
      var best = inks[0];
      for (var i = 1; i < inks.length; i++)
        if (Math.abs(inks[i].l - l) < Math.abs(best.l - l)) best = inks[i];
      return best.css;
    }
    /* What the word wears at rest, per row: the same five stops the mask's
       gradient uses. */
    function restInk(row) {
      var v = row / wm.height;
      return v < 0.26316 ? palette.crest : v < 0.36842 ? palette.hover
        : v < 0.57895 ? palette.lit : v < 0.73684 ? palette.mid : palette.dim;
    }
    function mixColour(from, to, t) {
      if (t <= 0) return from;
      if (t >= 1) return to;
      var a = parseColour(from), b = parseColour(to);
      if (!a || !b) return t < 0.5 ? from : to;
      return 'rgb(' + Math.round(a[0] + (b[0] - a[0]) * t) + ',' +
                      Math.round(a[1] + (b[1] - a[1]) * t) + ',' +
                      Math.round(a[2] + (b[2] - a[2]) * t) + ')';
    }

    /* Drawn after the field each frame, so the word sits over it the way the
       mask element does. Returns false once it has finished. */
    function paint(now) {
      if (!run) return false;
      if (!run.t0) { run.t0 = now; buildInks(); }

      var alive = run.etch.advance(now);
      if (!alive && !run.settledAt) run.settledAt = now;
      var settle = run.settledAt ? Math.min(1, (now - run.settledAt) / ETCH_SETTLE_MS) : 0;
      if (settle >= 1) { stop(); return false; }

      var cw = box.cw, ch = box.ch;
      var stroke = Math.max(1, Math.round(cw / 5));
      var cells = run.etch.cells();
      ctx.save();
      ctx.lineCap = 'butt';

      for (var i = 0; i < cells.length; i++) {
        var cell = cells[i];
        var xLeft = box.x + cell.col * cw;
        var yTop = box.y + cell.row * ch;
        // Width taken from the next rounded edge, so cells tile seamlessly.
        // Rounding each cell's width on its own leaves seams, and a word made
        // of seams reads as a grid rather than as letters.
        var x = Math.round(xLeft), y = Math.round(yTop);
        var w = Math.round(xLeft + cw) - x;
        var h = Math.round(yTop + ch) - y;

        var effectInk = themeInk(cell.rgb);
        var ink = cell.kind === 'block'
          ? mixColour(effectInk, restInk(cell.row), settle)
          : effectInk;

        if (cell.kind === 'block') {
          ctx.fillStyle = ink;
          ctx.fillRect(x, y, w, h);
          continue;
        }
        // Once the word starts settling, the sparks and beams are gone: only
        // the word itself is left to arrive.
        if (settle > 0) continue;

        ctx.fillStyle = ink;
        if (cell.kind === 'part') {
          for (var p = 0; p < cell.parts.length; p++) {
            var q = cell.parts[p];
            var px = Math.round(xLeft + q[0] * cw), py = Math.round(yTop + q[1] * ch);
            ctx.fillRect(px, py,
              Math.max(1, Math.round(xLeft + (q[0] + q[2]) * cw) - px),
              Math.max(1, Math.round(yTop + (q[1] + q[3]) * ch) - py));
          }
        } else if (cell.kind === 'line') {
          ctx.strokeStyle = ink;
          ctx.lineWidth = stroke;
          ctx.beginPath();
          if (cell.line === 'up') { ctx.moveTo(x, y + h); ctx.lineTo(x + w, y); }
          else if (cell.line === 'down') { ctx.moveTo(x, y); ctx.lineTo(x + w, y + h); }
          else if (cell.line === 'bar') { ctx.moveTo(x + w / 2, y); ctx.lineTo(x + w / 2, y + h); }
          else { ctx.moveTo(x, y + h / 2); ctx.lineTo(x + w, y + h / 2); }
          ctx.stroke();
        } else {
          var s = Math.max(1, Math.round(w * cell.weight));
          ctx.fillRect(x + ((w - s) >> 1), y + ((h - s) >> 1), s, s);
        }
      }
      ctx.restore();
      return true;
    }

    word.style.cursor = 'pointer';
    word.setAttribute('tabindex', '0');
    word.setAttribute('role', 'button');
    word.setAttribute('aria-label', 'Lockscreens — press to redraw');
    word.addEventListener('click', start);
    word.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); start(); }
    });
    window.addEventListener('resize', function () { if (run) measure(); });

    return { paint: paint, running: function () { return !!run; } };
  }

  /* --------------------------------------------------------------- chrome */

  function chrome() {
    var el = document.querySelector('.bar');
    if (el) {
      var onScroll = function () { el.classList.toggle('scrolled', window.scrollY > 4); };
      onScroll();
      window.addEventListener('scroll', onScroll, { passive: true });
    }

    var toggle = document.getElementById('navToggle');
    var drawer = document.getElementById('navDrawer');
    if (toggle && drawer) toggle.addEventListener('click', function () {
      var open = drawer.classList.toggle('open');
      toggle.setAttribute('aria-expanded', String(open));
    });

    // Copy-the-command boxes, wherever they appear.
    document.querySelectorAll('.cmd .copy').forEach(function (b) {
      b.addEventListener('click', function () {
        var el = document.getElementById(b.dataset.copy);
        if (!el || !navigator.clipboard) return;
        navigator.clipboard.writeText(el.textContent.trim()).then(function () {
          var was = b.textContent;
          b.textContent = 'copied';
          setTimeout(function () { b.textContent = was; }, 1200);
        }).catch(function () {});
      });
    });
  }

  /* ----------------------------------------------------------------- boot */

  buildPicker();
  var saved = null;
  try { saved = localStorage.getItem(STORE); } catch (e) {}
  applyTheme(saved || DEFAULT, true);
  chrome();
  document.querySelectorAll('.field').forEach(pixelField);
})();
