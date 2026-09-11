/*
  Draw the wordmark with ttfx, the way omarchy.org does.

  ttfx (github.com/omacom/ttfx, MIT) is a terminal engine: it takes text, runs
  an effect over a grid of character cells, and each frame hands back a symbol
  and a colour per cell. Nothing about it is pixels. So the wordmark bitmap
  goes in as text -- one full block per lit cell, blanks elsewhere -- and the
  cells that come back are painted onto the lattice the field already draws
  on. The effect's timing and its sparks are ttfx's own; the pixels are ours,
  and the finished word is the bitmap, cell for cell.

  A terminal cell is a character, though, not a square. Painted as solid
  squares a beam of slashes and a spray of dots turn to mush, so every cell is
  drawn as what it holds: a block fills, a slash is a thin diagonal, a block
  element covers the eighths it names, and anything else is a small square
  sized by how much ink the character carries.

  See wwwroot/ttfx/NOTICE for the licence and what is vendored.
*/
(function () {
  'use strict';

  var SCRIPT = '/ttfx/ttfx.js';
  var WASM = '/ttfx/all.wasm';

  /* Every effect in the build. The five in NOT_DRAWN are excluded the way
     omarchy.org excludes them: they animate the whole field rather than
     drawing the word, so on a wordmark they read as nothing happening. */
  var EFFECTS = [
    'beams', 'binarypath', 'blackhole', 'bouncyballs', 'bubbles', 'burn',
    'colorshift', 'crumble', 'decrypt', 'errorcorrect', 'expand', 'fireworks',
    'highlight', 'laseretch', 'middleout', 'orbittingvolley', 'overflow',
    'pour', 'print', 'randomsequence', 'scattered', 'slice', 'slide', 'smoke',
    'spray', 'sweep', 'synthgrid', 'unstable', 'vhstape', 'waves', 'wipe'
  ];

  /* Steps per second, per effect: the ones that would otherwise crawl or
     blur past. Taken from omarchy.org's own table. */
  var STEPS = {
    laseretch: 400, fireworks: 300, decrypt: 300, orbittingvolley: 300,
    print: 360, bubbles: 320, expand: 100, highlight: 100, middleout: 100,
    overflow: 60, wipe: 100, slide: 120, slice: 120, randomsequence: 150,
    scattered: 150, sweep: 150
  };
  var DEFAULT_STEPS = 240;

  /* Cells of blank field around the word the effect may throw sparks into. */
  var PAD_ROWS = 4, PAD_COLS = 6;
  var BLOCK = 0x2588;

  var lastPicked = '';
  function pickEffect() {
    var pool = EFFECTS.filter(function (n) { return n !== lastPicked; });
    lastPicked = pool[(Math.random() * pool.length) | 0];
    return lastPicked;
  }

  var loading = null;
  function load() {
    if (!loading) {
      loading = import(new URL(SCRIPT, window.location.origin).href)
        .then(function (mod) {
          return mod.default({ module_or_path: WASM }).then(function () { return mod; });
        });
    }
    return loading;
  }

  /* ---- what a cell holds, in pixel terms ---------------------------------- */

  var Q = 0.5;
  /* Unicode block elements are exact shapes: eighths from the bottom or the
     left, halves, quadrants and three shades. Each is the rectangles it
     covers, in cell units, so a wave's crest or a beam's tail is drawn as the
     shape it actually is. */
  var PARTS = {
    0x2580: [[0, 0, 1, Q]],
    0x2581: [[0, 7 / 8, 1, 1 / 8]],
    0x2582: [[0, 6 / 8, 1, 2 / 8]],
    0x2583: [[0, 5 / 8, 1, 3 / 8]],
    0x2584: [[0, Q, 1, Q]],
    0x2585: [[0, 3 / 8, 1, 5 / 8]],
    0x2586: [[0, 2 / 8, 1, 6 / 8]],
    0x2587: [[0, 1 / 8, 1, 7 / 8]],
    0x2589: [[0, 0, 7 / 8, 1]],
    0x258a: [[0, 0, 6 / 8, 1]],
    0x258b: [[0, 0, 5 / 8, 1]],
    0x258c: [[0, 0, Q, 1]],
    0x258d: [[0, 0, 3 / 8, 1]],
    0x258e: [[0, 0, 2 / 8, 1]],
    0x258f: [[0, 0, 1 / 8, 1]],
    0x2590: [[Q, 0, Q, 1]],
    0x2594: [[0, 0, 1, 1 / 8]],
    0x2595: [[7 / 8, 0, 1 / 8, 1]],
    0x2596: [[0, Q, Q, Q]],
    0x2597: [[Q, Q, Q, Q]],
    0x2598: [[0, 0, Q, Q]],
    0x259d: [[Q, 0, Q, Q]],
    0x2599: [[0, 0, Q, Q], [0, Q, Q, Q], [Q, Q, Q, Q]],
    0x259a: [[0, 0, Q, Q], [Q, Q, Q, Q]],
    0x259b: [[0, 0, Q, Q], [Q, 0, Q, Q], [0, Q, Q, Q]],
    0x259c: [[0, 0, Q, Q], [Q, 0, Q, Q], [Q, Q, Q, Q]],
    0x259e: [[Q, 0, Q, Q], [0, Q, Q, Q]],
    0x259f: [[Q, 0, Q, Q], [0, Q, Q, Q], [Q, Q, Q, Q]],
    /* The three shades become the classic dither: one, two or three quarters. */
    0x2591: [[0, 0, Q, Q]],
    0x2592: [[0, 0, Q, Q], [Q, Q, Q, Q]],
    0x2593: [[0, 0, Q, Q], [Q, 0, Q, Q], [0, Q, Q, Q]]
  };

  var LINES = {
    0x2f: 'up', 0x2571: 'up',
    0x5c: 'down', 0x2572: 'down',
    0x7c: 'bar', 0x2502: 'bar', 0x2503: 'bar',
    0x2d: 'dash', 0x2500: 'dash', 0x2501: 'dash', 0x5f: 'dash'
  };

  /* Roughly how much ink a character carries, so a cell of @ reads heavier
     than one holding a dot. */
  function weightOf(cp) {
    var ch = String.fromCodePoint(cp);
    if ('.,\'`·˙'.indexOf(ch) !== -1) return 0.22;
    if (':;^~-_"'.indexOf(ch) !== -1) return 0.32;
    if ('*+=<>oxsc'.indexOf(ch) !== -1) return 0.5;
    if ('#@%&8BMW0'.indexOf(ch) !== -1) return 0.8;
    return 0.6;
  }

  /* ---- the session -------------------------------------------------------- */

  function toText(rows, width, height) {
    var columns = width + PAD_COLS * 2;
    var blank = new Array(columns + 1).join(' ');
    var pad = new Array(PAD_COLS + 1).join(' ');
    var text = [];
    for (var r = 0; r < PAD_ROWS; r++) text.push(blank);
    for (r = 0; r < height; r++) {
      var line = pad;
      for (var c = 0; c < width; c++)
        line += rows[r].charAt(c) === '1' ? String.fromCodePoint(BLOCK) : ' ';
      text.push(line + pad);
    }
    for (r = 0; r < PAD_ROWS; r++) text.push(blank);
    return { text: text.join('\n'), columns: columns, lines: text.length };
  }

  /* An effect's output grid does not always sit where its input text did:
     some pad, some shift. Left uncorrected the finished word lands a few
     cells off -- visibly to one side of where the mask draws it.

     So the effect is run once, to the end, on a throwaway session, and the
     first full block in the result is compared with the first lit cell of
     the bitmap. The difference is the offset every cell is read back
     through. Cached per effect and size, since it never changes. */
  var offsets = {};
  function measureOffset(ttfx, text, columns, lines, rows, effect) {
    var key = effect + ':' + columns + 'x' + lines + ':' + rows.length;
    if (offsets[key]) return offsets[key];

    var probe = new ttfx.Session(text, effect, columns, lines, 0, DEFAULT_STEPS, null, null);
    for (var guard = 0; guard < 100000 && probe.step(); guard++) { /* to the end */ }
    var w = probe.width(), h = probe.height();
    var symbols = new Uint32Array(w * h);
    probe.fill(symbols, new Uint32Array(w * h), new Uint32Array(w * h), new Uint8Array(w * h));
    probe.free();

    var found = { dx: 0, dy: 0 };
    var at = -1;
    for (var i = 0; i < symbols.length; i++) if (symbols[i] === BLOCK) { at = i; break; }
    if (at >= 0) {
      var outRow = Math.floor(at / w), outCol = at % w;
      done: for (var r = 0; r < rows.length; r++) {
        for (var c = 0; c < rows[r].length; c++) {
          if (rows[r].charAt(c) === '1') {
            found = { dx: outCol - (c + PAD_COLS), dy: outRow - (r + PAD_ROWS) };
            break done;
          }
        }
      }
    }
    offsets[key] = found;
    return found;
  }

  /**
   * Start an effect over the bitmap.
   *
   * rows/width/height: the wordmark bitmap.
   * palette: colours the effect may use, as CSS hex strings.
   * Returns a promise for { advance(timeMs) -> bool, cells() -> [...], free() }.
   */
  function start(rows, width, height, palette, effect) {
    return load().then(function (ttfx) {
      var name = (!effect || effect === 'random') ? pickEffect() : effect;
      var built = toText(rows, width, height);
      var off = measureOffset(ttfx, built.text, built.columns, built.lines, rows, name);
      var perSecond = STEPS[name] || DEFAULT_STEPS;

      var session = new ttfx.Session(
        built.text, name, built.columns, built.lines,
        undefined, perSecond, palette.join(','), null);

      var symbols = new Uint32Array(0), fg = new Uint32Array(0);
      var bg = new Uint32Array(0), flags = new Uint8Array(0);
      var w = 0, h = 0, live = true, t0 = 0, stepped = 0;

      function read() {
        w = session.width(); h = session.height();
        var n = w * h;
        if (symbols.length < n) {
          symbols = new Uint32Array(n); fg = new Uint32Array(n);
          bg = new Uint32Array(n); flags = new Uint8Array(n);
        }
        session.fill(symbols, fg, bg, flags);
      }

      return {
        name: name,
        /* Step the effect up to `time`, at its own rate. False once done. */
        advance: function (time) {
          if (!live) return false;
          if (!t0) { t0 = time; live = session.step(); stepped = 1; read(); return live; }
          var want = Math.floor((time - t0) / 1000 * perSecond) + 1;
          var guard = 0;
          while (stepped < want && live && guard++ < 8) { live = session.step(); stepped++; }
          read();
          return live;
        },
        /* Every occupied cell of the current frame, in wordmark cell
           coordinates -- the padding is subtracted back off. */
        cells: function () {
          var out = [];
          for (var r = 0; r < h; r++) {
            for (var c = 0; c < w; c++) {
              var cp = symbols[r * w + c];
              if (!cp || cp === 0x20) continue;
              var cell = {
                col: c - PAD_COLS - off.dx,
                row: r - PAD_ROWS - off.dy,
                rgb: fg[r * w + c] & 0xffffff,
                symbol: cp
              };
              if (cp === BLOCK) cell.kind = 'block';
              else if (PARTS[cp]) { cell.kind = 'part'; cell.parts = PARTS[cp]; }
              else if (LINES[cp]) { cell.kind = 'line'; cell.line = LINES[cp]; }
              else { cell.kind = 'mark'; cell.weight = weightOf(cp); }
              out.push(cell);
            }
          }
          return out;
        },
        free: function () { try { session.free(); } catch (e) {} }
      };
    });
  }

  window.Etch = { start: start, effects: EFFECTS };
})();
