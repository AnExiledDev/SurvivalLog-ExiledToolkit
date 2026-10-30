// Exiled's Toolkit's HUD dock. Runs in Root.html, the document every game page iframe sits in. Mods put their buttons
// in the HUD's tool column (CoreUI1's, and CoreUI0's before the disaster), above the game's phone and bag. Past four
// buttons that column climbs the screen, so the dock lays it out as a grid instead: the last four buttons stay in the
// first column where the column always was, the game's own at the bottom, and the rest fill columns to the right, top
// to bottom, four to a column.
//
// A mod marks its button with data-etk-dock="<order>"; marked buttons come first, lowest order first, so the layout
// does not depend on which mod happened to load first. Unmarked buttons (the game's, and mods that do not mark theirs)
// follow in the order the page has them. With four buttons or fewer the game's own layout is left untouched. The whole
// column, the game's buttons with the mods', is drawn at the player's HUD button size (Interface > HudButtonSize),
// which the C# side sends through size().
//
// The dock also draws every button in the column in the player's button style (Interface > HudButtonStyle, sent
// through look()): a class on the column picks one stylesheet for the game's buttons and the mods' alike. The phone,
// the bag and the known mod buttons get a new icon: the game's own svg stays where it is, hidden by the stylesheet,
// and the dock's svg.etk-ic goes in beside it, so Vue keeps owning its markup. Classic takes all of it off again.
(function () {
  if (window.__exiledtoolkitdock) return;

  var ROWS = 4;
  var POLL_MS = 250;
  var DOCK_CLASS = 'etk-dock';
  var STYLE_ID = 'etk-dock-style';
  var ORDER_ATTR = 'data-etk-dock';
  var HUD_PAGES = /CoreUI[01]\.html/;
  // The HUD button size as a factor, as last sent; 1 is the game's own size.
  var buttonZoom = 1;
  // The grid keeps the column's own gap between rows and columns. A hovered button rises above its neighbours, so its
  // tip, which opens to the right, is not drawn under the next column's buttons. The display rule stays without
  // !important: two classes already beat the game's flex rule, and the game hides the column in plan mode with an
  // inline display:none that an !important rule would override.
  var CSS = '.vertical-tools.' + DOCK_CLASS + '{display:grid;grid-auto-columns:max-content;align-items:center;justify-items:center}' +
    '.vertical-tools.' + DOCK_CLASS + '>*:hover{z-index:30}';

  // Where each of count buttons goes, in the order they are given: { row, column }, both from 1. The last
  // min(count, ROWS) take the first column, bottom aligned; the rest fill columns 2, 3, ... top to bottom.
  function slots(count) {
    var rows = Math.min(count, ROWS);
    var overflow = count - rows;
    var result = [];

    for (var i = 0; i < count; i++) {
      if (i >= overflow) result.push({ row: i - overflow + 1, column: 1 });
      else result.push({ row: i % ROWS + 1, column: 2 + Math.floor(i / ROWS) });
    }

    return result;
  }

  function orderOf(node) {
    var value = parseFloat(node.getAttribute(ORDER_ATTR));

    return isNaN(value) ? 0 : value;
  }

  // The buttons that show, in dock order: marked ones by their order (ties keep page order), then the rest.
  function dockOrder(column) {
    var marked = [];
    var rest = [];
    var children = column.children;

    for (var i = 0; i < children.length; i++) {
      var child = children[i];
      var view = child.ownerDocument.defaultView;

      if (view && view.getComputedStyle(child).display === 'none') continue;

      if (child.hasAttribute(ORDER_ATTR)) marked.push({ node: child, at: i });
      else rest.push(child);
    }

    marked.sort(function (a, b) { return orderOf(a.node) - orderOf(b.node) || a.at - b.at; });

    return marked.map(function (entry) { return entry.node; }).concat(rest);
  }

  // Writes a style only when it differs, so the game's own watcher of these attributes is not woken for nothing.
  function setStyle(node, name, value) {
    if (node.style[name] !== value) node.style[name] = value;
  }

  function release(column) {
    if (column.classList.contains(DOCK_CLASS)) column.classList.remove(DOCK_CLASS);

    setStyle(column, 'gridTemplateRows', '');

    for (var i = 0; i < column.children.length; i++) {
      setStyle(column.children[i], 'gridRow', '');
      setStyle(column.children[i], 'gridColumn', '');
    }
  }

  function layout(column) {
    var buttons = dockOrder(column);

    setStyle(column, 'zoom', buttonZoom === 1 ? '' : String(buttonZoom));

    if (buttons.length <= ROWS) {
      release(column);

      return;
    }

    if (!column.classList.contains(DOCK_CLASS)) column.classList.add(DOCK_CLASS);

    setStyle(column, 'gridTemplateRows', 'repeat(' + ROWS + ', auto)');

    var places = slots(buttons.length);

    for (var i = 0; i < buttons.length; i++) {
      setStyle(buttons[i], 'gridRow', String(places[i].row));
      setStyle(buttons[i], 'gridColumn', String(places[i].column));
    }
  }

  function addStyle(doc) {
    if (doc.getElementById(STYLE_ID)) return;

    var style = doc.createElement('style');
    style.id = STYLE_ID;
    style.textContent = CSS + themeCss();
    (doc.head || doc.body).appendChild(style);
  }

  // ===== Button styles =====

  var SVG_NS = 'http://www.w3.org/2000/svg';
  var ICON_ATTR = 'data-etk-ic';
  var ICON_CLASS = 'etk-ic';
  var MONO_CLASS = 'etk-mono';
  var EXTRAS_CLASS = 'etk-x';
  // The column class of each style the C# side can name. Classic, or any name not here, is the game's look: no class.
  var STYLE_CLASSES = { Lamplight: 'etk-st-lamplight', FieldKit: 'etk-st-fieldkit', Signal: 'etk-st-signal' };
  // The look as last sent: the style's column class ('' for Classic), one icon colour, Field kit's extras.
  var look = { cls: '', mono: false, extras: true };

  // The mods' HUD buttons by the class each gives its button.
  var MOD_KINDS = [
    ['th-hud-btn', 'trap'], ['pb-hud-btn', 'package'], ['fg-hud-btn', 'guide'], ['fh-hud-btn', 'farm'],
    ['dm-hud-btn', 'defense'], ['mso-hud-btn', 'machines'], ['pm-hud-btn', 'power'], ['sh-hud-btn', 'storage']
  ];
  // The game's phone and bag icons, by how their path starts. CoreUI1's phone has an id, CoreUI0's a class; the bag has
  // neither in either page, and its path differs between them after "M20 6h" (-4V4 in CoreUI1, -3V4 in CoreUI0).
  var PHONE_PATH = 'M17 1.01L7 1c';
  var BAG_PATH = 'M20 6h';
  // Each tool's colour. Buttons the dock does not know take the column's amber.
  var AMBER = '#f5b82e';
  var HUES = {
    trap: '#ee7a3e', package: '#d9b27c', guide: '#b9a2f0', farm: '#8fd35f', defense: '#6fb0ee',
    machines: '#f2a03d', power: '#f5dc4a', storage: '#52cdb9', phone: '#efe7d6', bag: '#e0965a'
  };
  // The icons on a 24 grid, layers in draw order: b is the dim mass, f the lit detail; a third value of 1 draws that
  // layer even-odd.
  var ICONS = {
    trap: [
      ['b', 'M2 1.8h20v1.9H2zM2 20.1h20v2.1H2zM2.6 3.7h1.6v16.4H2.6zM19.8 3.7h1.6v16.4h-1.6z'],
      ['f', 'M7 16.2a5 4 0 1 0 10 0a5 4 0 1 0-10 0zM9 11.2a3 3 0 1 0 6 0a3 3 0 1 0-6 0zM9.4 6.6a1.1 3 0 1 0 2.2 0a1.1 3 0 1 0-2.2 0zM12.4 6.6a1.1 3 0 1 0 2.2 0a1.1 3 0 1 0-2.2 0z'],
      ['b', 'M6.6 3.7h1.1v16.4H6.6zM16.3 3.7h1.1v16.4h-1.1z']
    ],
    package: [
      ['b', 'M3 9.2h18V21H3zM2 5.4h20v2.8H2z'],
      ['f', 'M10.4 5.4h3.2v2.8h-3.2zM7.6 11.2h8.8v3.2H7.6zM6.8 14h10.4v2.8H6.8zM5.2 12.6h2.4v5.2H5.2zM16.4 12.6h2.4v5.2h-2.4zM6.6 17.8h1.4v1.6H6.6zM16 17.8h1.4v1.6H16z']
    ],
    guide: [
      ['b', 'M5.6 1.8h13a1.4 1.4 0 0 1 1.4 1.4v15.6H6.9a1.3 1.3 0 0 1-1.3-1.3z'],
      ['f', 'M5.6 18.9a1.3 1.3 0 0 1 1.3-1.3H20v2.8H6.9a1.5 1.5 0 0 1-1.3-1.5zM14.3 13l1.2-1.2 3.2 3.2-1.2 1.2z'],
      ['f', 'M12.2 4.6a4 4 0 1 0 0 8a4 4 0 1 0 0-8zM12.2 6.2a2.4 2.4 0 1 1 0 4.8a2.4 2.4 0 1 1 0-4.8z', 1]
    ],
    farm: [
      ['b', 'M4 14.4h16v2.6H4zM5.4 17h13.2l-1.3 4.6a1 1 0 0 1-1 .8H7.7a1 1 0 0 1-1-.8z'],
      ['f', 'M11.2 13C6.8 13 3.6 10.2 3.6 5.2c4.8 0 7.6 3 7.6 7.8zM12.8 11.2c0-4.8 3-8 7.6-8 0 4.8-3 8-7.6 8zM11.2 9.6h1.6v4.8h-1.6z']
    ],
    defense: [
      ['b', 'M3.4 7.2L5.6 3.4l2.2 3.8V21.6H3.4zM9.8 5.2L12 1.4l2.2 3.8v16.4H9.8zM16.2 7.2l2.2-3.8 2.2 3.8v14.4h-4.4z'],
      ['f', 'M2 9.4h20v2.8H2zM2 15.4h20v2.8H2z']
    ],
    machines: [
      ['b', 'M3.4 9h13.2a1 1 0 0 1 1 1v12H2.4V10a1 1 0 0 1 1-1zM12.6 4h3.2v5h-3.2z'],
      ['f', 'M10 20.6c-2.4 0-4-1.5-4-3.6 0-2 1.5-2.9 2.1-4.9 1 .8 1.5 1.8 1.5 2.7.6-.5.9-1.2.9-2.1 2.1 1.3 3.6 3.1 3.6 4.6 0 1.9-1.7 3.3-4.1 3.3zM18.4 3.4a1.7 1.7 0 1 1 0 3.4a1.7 1.7 0 0 1 0-3.4zM20.6.6a1.2 1.2 0 1 1 0 2.4a1.2 1.2 0 0 1 0-2.4z']
    ],
    power: [
      ['b', 'M2 12a10 10 0 1 0 20 0a10 10 0 1 0-20 0z'],
      ['f', 'M13.8 3.4L6.6 13.4h4.8l-1.3 7.2 7.3-10.2h-4.9z']
    ],
    storage: [
      ['b', 'M2.6 11.4h18.8v9a1.2 1.2 0 0 1-1.2 1.2H3.8a1.2 1.2 0 0 1-1.2-1.2zM2.6 10.2V8.2A5 5 0 0 1 7.6 3.2h8.8a5 5 0 0 1 5 5v2z'],
      ['f', 'M5.8 3.6h2v18H5.8zM16.2 3.6h2v18h-2zM10.2 8.4h3.6v5.4h-3.6z']
    ],
    phone: [
      ['b', 'M7.6 1.4h8.8A2.6 2.6 0 0 1 19 4v16a2.6 2.6 0 0 1-2.6 2.6H7.6A2.6 2.6 0 0 1 5 20V4a2.6 2.6 0 0 1 2.6-2.6z'],
      ['f', 'M7.2 4.4h9.6v13H7.2zM10.6 19h2.8v1.6h-2.8z'],
      ['b', 'M8.6 6.4h6.8a.9.9 0 0 1 .9.9v3.8a.9.9 0 0 1-.9.9h-3.8l-2.2 2v-2h-.8a.9.9 0 0 1-.9-.9V7.3a.9.9 0 0 1 .9-.9z']
    ],
    bag: [
      ['b', 'M4.6 10a5 5 0 0 1 5-5h4.8a5 5 0 0 1 5 5v10.4a1.6 1.6 0 0 1-1.6 1.6H6.2a1.6 1.6 0 0 1-1.6-1.6z'],
      ['f', 'M9 5.2V3.4A1.4 1.4 0 0 1 10.4 2h3.2A1.4 1.4 0 0 1 15 3.4v1.8h-1.6V3.6h-2.8v1.6zM7.4 10.6h9.2v1.6H7.4zM7.4 14h9.2v5a1 1 0 0 1-1 1H8.4a1 1 0 0 1-1-1z']
    ]
  };

  // "#rrggbb" as [r, g, b].
  function rgbOf(hex) {
    return [parseInt(hex.substr(1, 2), 16), parseInt(hex.substr(3, 2), 16), parseInt(hex.substr(5, 2), 16)];
  }

  function hex2(n) {
    var text = Math.round(n).toString(16);

    return text.length < 2 ? '0' + text : text;
  }

  // share of a mixed with the rest of b, as CSS color-mix(in srgb) would, worked out here so the page needs no
  // color-mix.
  function mix(a, share, b) {
    var x = rgbOf(a);
    var y = rgbOf(b);

    return '#' + hex2(x[0] * share + y[0] * (1 - share)) + hex2(x[1] * share + y[1] * (1 - share)) + hex2(x[2] * share + y[2] * (1 - share));
  }

  // The colours a style draws one tool in: the hue, the hue as "r,g,b" for see-through uses, and the dim and lit icon
  // layers.
  var TONES = {
    lamplight: function (hue) { return [hue, mix(hue, 0.52, '#1c1510'), hue]; },
    signal: function (hue) { return [hue, mix(hue, 0.58, '#15120e'), hue]; },
    fieldkit: function (hue) { return [hue, mix(hue, 0.4, '#8c875c'), mix(hue, 0.62, '#f3ebd1')]; }
  };

  function tones(style, hue) {
    var t = TONES[style](hue);

    return '{--etk-hue:' + t[0] + ';--etk-rgb:' + rgbOf(hue).join(',') + ';--etk-b:' + t[1] + ';--etk-f:' + t[2] + '}';
  }

  // "prefix a,prefix b,...", one selector per item.
  function each(prefix, items) {
    return items.map(function (item) { return prefix + item; }).join(',');
  }

  var TIPS = ['.entry-tip', '.ability-tip', '.btn-tooltip'];
  var TIP_TITLES = ['.entry-tip-title', '.ability-tip-title', '.tip-name'];
  var TIP_BODIES = ['.entry-tip-body', '.ability-tip-body', '.tip-desc'];
  var TIP_KEYS = ['.entry-tip-key'];
  var BADGES = ['.phone-badge', '.bag-badge', '.th-hud-badge', '.fh-hud-badge', '.mso-hud-needs'];
  var NOISE = 'url("data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' width=\'64\' height=\'64\'><filter id=\'n\'><feTurbulence type=\'fractalNoise\' baseFrequency=\'1.1\' numOctaves=\'2\' stitchTiles=\'stitch\'/><feColorMatrix values=\'0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0.45 0\'/></filter><rect width=\'100%\' height=\'100%\' filter=\'url(%23n)\'/></svg>")';

  // Field kit's extras, which Lamplight and Signal wear with HudButtonExtras on and Field kit always: hazard tape on a
  // button that needs you, stamped badges, paper tag tips (with the tool's colour on the edge when edge is set).
  function extrasCss(p, radius, edge) {
    var b = ' .ui-btn';

    return [
      p + b + '.has-unread::after{content:"";position:absolute;pointer-events:none;inset:auto -1px -1px -1px;height:6px;border:0;border-radius:' + radius + ';box-shadow:none;opacity:1;transform:none;background:repeating-linear-gradient(-45deg,#f2c230 0 4px,#1b1b14 4px 8px);background-size:16px 6px;animation:etk-stripes .9s linear infinite;z-index:3}',
      each(p + ' ', BADGES) + '{border-radius:2px;background:#e8772e;color:#1a1208;border:1px solid #1a1208;transform:rotate(-6deg);box-shadow:0 1px 0 rgba(0,0,0,.6);font-weight:800}',
      p + ' .mso-hud-needs{background:#f2c230}',
      p + ' .mso-hud-working{border-radius:2px;background:#d9d2b8;color:#23221a;border:1px solid #1a1208;transform:rotate(4deg);line-height:16px}',
      p + ' .phone-badge.is-dot{border-radius:50%;transform:none}',
      each(p + ' ', TIPS) + '{background:linear-gradient(180deg,#efe5c8,#ddd0ac);color:#2a261a;border-radius:2px;border:1px solid #b9ab84;' + (edge ? 'border-left:4px solid var(--etk-hue);' : '') + 'box-shadow:0 4px 10px rgba(0,0,0,.55);padding:7px 11px 7px 18px}',
      each(p + ' ', TIPS.map(function (t) { return t + '::before'; })) + '{content:"";position:absolute;left:6px;top:50%;width:6px;height:6px;margin-top:-3px;border-radius:50%;background:#3e3d28;box-shadow:inset 0 1px 1px rgba(0,0,0,.6)}',
      each(p + ' ', TIP_TITLES) + '{color:#23221a;text-transform:uppercase;letter-spacing:.08em}',
      each(p + ' ', TIP_KEYS) + '{background:#3e3d28;color:#f3ebd1;border:0;border-radius:2px}',
      each(p + ' ', TIP_BODIES) + '{color:#4e4835}'
    ].join('\n');
  }

  // The rules every restyled column shares: the hues, the icons, and the game's own icons hidden where the dock put one.
  function sharedCss(style, p, monoHue) {
    var rules = [p + tones(style, AMBER)];

    for (var kind in HUES) rules.push(p + ' [' + ICON_ATTR + '="' + kind + '"]' + tones(style, HUES[kind]));

    if (monoHue) rules.push(p + '.' + MONO_CLASS + ',' + p + '.' + MONO_CLASS + ' .ui-btn' + tones(style, monoHue));

    rules.push(
      p + ' [' + ICON_ATTR + ']>svg.icon-svg{display:none}',
      p + ' .ui-btn>svg.icon-svg{fill:var(--etk-f)}',
      p + ' .ui-btn>svg.' + ICON_CLASS + '{width:25px;height:25px;display:block;flex:none;position:relative;z-index:2;transition:transform .16s ease,filter .16s ease}',
      p + ' .' + ICON_CLASS + ' .b{fill:var(--etk-b)}',
      p + ' .' + ICON_CLASS + ' .f{fill:var(--etk-f)}'
    );

    return rules.join('\n');
  }

  function lamplightCss() {
    var p = '.vertical-tools.' + STYLE_CLASSES.Lamplight;
    var x = p + '.' + EXTRAS_CLASS;
    var b = ' .ui-btn';
    var shadow = 'box-shadow:inset 0 1px 0 rgba(255,214,140,.14),inset 0 -10px 14px rgba(0,0,0,.35),0 2px 6px rgba(0,0,0,.55)';
    var keyShadow = 'box-shadow:inset 0 1px 0 rgba(255,214,140,.14),inset 0 -10px 14px rgba(0,0,0,.35),0 3px 0 #120d08,0 5px 8px rgba(0,0,0,.5)';

    return [
      sharedCss('lamplight', p, AMBER),
      p + b + '{border-radius:7px;background:linear-gradient(180deg,#34291c 0%,#1c1510 100%);border:1px solid rgba(245,184,46,.3);' + shadow + ';color:var(--etk-f)}',
      p + b + '>svg{filter:drop-shadow(0 1px 0 rgba(0,0,0,.7))}',
      p + b + '::before{content:"";position:absolute;left:24%;right:24%;bottom:3px;height:2px;border-radius:2px;background:var(--etk-hue);transform:scaleX(0);transition:transform .18s ease;z-index:2;pointer-events:none}',
      p + b + ':hover{border-color:rgba(var(--etk-rgb),.8);background:linear-gradient(180deg,#45351f 0%,#241a11 100%);transform:translateY(-1px);filter:none;' + shadow + '}',
      p + b + ':hover::before{transform:scaleX(1)}',
      p + b + ':active{transform:translateY(1px) scale(.97);background:#17110b;box-shadow:inset 0 3px 7px rgba(0,0,0,.75);filter:none}',
      p + b + '.has-unread::after{inset:-4px;border-radius:10px;border:1.5px solid #f5b82e;box-shadow:0 0 12px 1px rgba(245,184,46,.45);transform:none;animation:etk-breath 2.4s ease-in-out infinite}',
      each(p + ' ', BADGES) + '{border-color:#1c1510}',
      each(p + ' ', TIPS) + '{background:linear-gradient(180deg,#2a2118,#1a140e);border:1px solid rgba(245,184,46,.4);border-left:3px solid var(--etk-hue);color:#ece3d2;box-shadow:0 6px 16px rgba(0,0,0,.6)}',
      each(p + ' ', TIP_TITLES) + '{color:var(--etk-hue);letter-spacing:.04em}',
      each(p + ' ', TIP_KEYS) + '{border:1px solid rgba(245,184,46,.6);color:#f5b82e}',
      each(p + ' ', TIP_BODIES) + '{color:#bfb29b}',
      x + b + ',' + x + b + ':hover{' + keyShadow + '}',
      x + b + ':active{transform:translateY(3px);box-shadow:inset 0 3px 6px rgba(0,0,0,.7),0 0 0 #120d08}',
      extrasCss(x, '0 0 7px 7px', true)
    ].join('\n');
  }

  function signalCss() {
    var p = '.vertical-tools.' + STYLE_CLASSES.Signal;
    var x = p + '.' + EXTRAS_CLASS;
    var b = ' .ui-btn';
    var shadow = 'box-shadow:inset 0 2px 0 var(--etk-hue),inset 0 3px 8px rgba(var(--etk-rgb),.25),0 3px 7px rgba(0,0,0,.55)';
    var keyShadow = 'box-shadow:inset 0 2px 0 var(--etk-hue),inset 0 3px 8px rgba(var(--etk-rgb),.25),0 3px 0 #120d08,0 5px 8px rgba(0,0,0,.5)';

    // Signal keeps one colour per tool whatever HudIconColours says: the colour is the style.
    return [
      sharedCss('signal', p, ''),
      p + b + '{border-radius:9px;background:radial-gradient(circle at 50% 46%,rgba(var(--etk-rgb),.26) 0%,transparent 64%),linear-gradient(180deg,#24201b,#15120e);border:1px solid rgba(255,255,255,.08);' + shadow + ';color:var(--etk-hue)}',
      p + b + '>svg{filter:drop-shadow(0 0 4px rgba(var(--etk-rgb),.55))}',
      p + b + ':hover{transform:translateY(-2px);border-color:rgba(var(--etk-rgb),.6);background:radial-gradient(circle at 50% 46%,rgba(var(--etk-rgb),.42) 0%,transparent 70%),linear-gradient(180deg,#2b261f,#17130f);filter:none;' + shadow + '}',
      p + b + ':hover>svg{transform:scale(1.08)}',
      p + b + ':active{transform:translateY(1px) scale(.96);background:#110e0b;box-shadow:inset 0 2px 0 var(--etk-hue),inset 0 3px 8px rgba(0,0,0,.8);filter:none}',
      p + b + '.has-unread::after{inset:-4px;border-radius:12px;border:1.5px solid var(--etk-hue);box-shadow:0 0 14px 2px rgba(var(--etk-rgb),.6);transform:none;animation:etk-breath 2s ease-in-out infinite}',
      each(p + ' ', TIPS) + '{background:rgba(18,15,12,.97);border:1px solid rgba(255,255,255,.12);border-top:2px solid var(--etk-hue);color:#eee;box-shadow:0 6px 16px rgba(0,0,0,.6)}',
      each(p + ' ', TIP_TITLES) + '{color:var(--etk-hue)}',
      each(p + ' ', TIP_KEYS) + '{background:rgba(var(--etk-rgb),.22);color:var(--etk-hue);border-color:transparent}',
      each(p + ' ', TIP_BODIES) + '{color:#bdb4a6}',
      x + b + ',' + x + b + ':hover{' + keyShadow + '}',
      x + b + ':active{transform:translateY(3px);box-shadow:inset 0 2px 0 var(--etk-hue),inset 0 3px 8px rgba(0,0,0,.8),0 0 0 #120d08}',
      extrasCss(x, '0 0 9px 9px', true)
    ].join('\n');
  }

  function fieldKitCss() {
    var p = '.vertical-tools.' + STYLE_CLASSES.FieldKit;
    var b = ' .ui-btn';

    // Field kit's extras are part of the style, so it has them whatever HudButtonExtras says.
    return [
      sharedCss('fieldkit', p, '#f3ebd1'),
      p + b + '{border-radius:4px;background:' + NOISE + ',repeating-linear-gradient(0deg,rgba(255,255,230,.04) 0 1px,transparent 1px 3px),repeating-linear-gradient(90deg,rgba(0,0,0,.08) 0 1px,transparent 1px 3px),linear-gradient(180deg,#5b5a3c,#3e3d28);border:1px solid #1f1e15;box-shadow:inset 0 1px 0 rgba(255,255,230,.14),0 3px 0 #1a1910,0 5px 8px rgba(0,0,0,.5);color:#f1e9cf}',
      p + b + '::before{content:"";position:absolute;inset:3px;border:1px dashed rgba(236,227,200,.38);border-radius:2px;pointer-events:none;z-index:1;transition:border-color .15s ease}',
      p + b + '>svg{filter:drop-shadow(0 1px 0 rgba(0,0,0,.7))}',
      p + b + ':hover{filter:brightness(1.15);transform:translateY(-1px);box-shadow:inset 0 1px 0 rgba(255,255,230,.2),0 4px 0 #1a1910,0 7px 10px rgba(0,0,0,.5)}',
      p + b + ':hover::before{border-color:#f2c230}',
      p + b + ':active{transform:translateY(3px);filter:brightness(.92);box-shadow:inset 0 2px 4px rgba(0,0,0,.5),0 0 0 #1a1910,0 1px 2px rgba(0,0,0,.5)}',
      extrasCss(p, '0 0 4px 4px', false)
    ].join('\n');
  }

  var themeText = null;

  // Built once, the first time a HUD page needs it.
  function themeCss() {
    if (themeText === null) {
      themeText = [
        '@keyframes etk-breath{0%,100%{opacity:.15}50%{opacity:1}}',
        '@keyframes etk-stripes{to{background-position:16px 0}}',
        lamplightCss(),
        fieldKitCss(),
        signalCss(),
        // The game stills its own ring on low quality; the styles' rings and tape do the same.
        'html.quality-low .vertical-tools .ui-btn.has-unread::after{animation:none;opacity:.55}',
        'html.quality-low .vertical-tools.etk-x .ui-btn.has-unread::after,html.quality-low .vertical-tools.etk-st-fieldkit .ui-btn.has-unread::after{opacity:1}'
      ].join('\n');
    }

    return themeText;
  }

  // Which icon a button gets: a mod button by its class, the phone and bag by their own icon. '' for every other
  // button. Kept on the button, so each is looked at once.
  function kindOf(button) {
    if (typeof button.etkKind === 'string') return button.etkKind;

    var kind = '';

    for (var i = 0; i < MOD_KINDS.length && !kind; i++) {
      if (button.classList.contains(MOD_KINDS[i][0])) kind = MOD_KINDS[i][1];
    }

    if (!kind && (button.id === 'hudPhoneBtn' || button.classList.contains('phone-btn-c0'))) kind = 'phone';

    if (!kind) {
      var path = button.querySelector('path');
      var d = path ? String(path.getAttribute('d') || '') : '';

      if (d.indexOf(PHONE_PATH) === 0) kind = 'phone';
      else if (d.indexOf(BAG_PATH) === 0) kind = 'bag';
    }

    button.etkKind = kind;

    return kind;
  }

  function makeIcon(doc, kind) {
    var svg = doc.createElementNS(SVG_NS, 'svg');
    var layers = ICONS[kind];

    svg.setAttribute('class', ICON_CLASS);
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('aria-hidden', 'true');

    for (var i = 0; i < layers.length; i++) {
      var path = doc.createElementNS(SVG_NS, 'path');

      path.setAttribute('class', layers[i][0]);
      path.setAttribute('d', layers[i][1]);

      if (layers[i][2]) path.setAttribute('fill-rule', 'evenodd');

      svg.appendChild(path);
    }

    return svg;
  }

  // The dock's icon in a known button. The mark is an attribute, not a class: Vue rewrites a button's whole class list
  // whenever one of its bound classes changes (the phone's has-unread), but leaves attributes it does not own alone.
  function dress(button) {
    var kind = kindOf(button);

    if (!kind) return;

    if (button.getAttribute(ICON_ATTR) !== kind) button.setAttribute(ICON_ATTR, kind);

    if (button.etkIcon && button.etkIcon.parentNode === button) return;

    button.etkIcon = makeIcon(button.ownerDocument, kind);
    button.appendChild(button.etkIcon);
  }

  function undress(button) {
    if (button.etkIcon && button.etkIcon.parentNode === button) button.removeChild(button.etkIcon);

    button.etkIcon = null;

    if (button.hasAttribute(ICON_ATTR)) button.removeAttribute(ICON_ATTR);
  }

  function setClass(node, name, isOn) {
    if (node.classList.contains(name) !== isOn) {
      if (isOn) node.classList.add(name);
      else node.classList.remove(name);
    }
  }

  // The column in the current look. A column never restyled is left without a search while the look is Classic.
  function theme(column) {
    var cls = look.cls;

    for (var name in STYLE_CLASSES) setClass(column, STYLE_CLASSES[name], STYLE_CLASSES[name] === cls);

    setClass(column, MONO_CLASS, !!cls && look.mono);
    setClass(column, EXTRAS_CLASS, !!cls && look.extras);

    if (!cls && !column.etkDressed) return;

    var buttons = column.getElementsByClassName('ui-btn');

    for (var i = 0; i < buttons.length; i++) {
      if (cls) dress(buttons[i]);
      else undress(buttons[i]);
    }

    column.etkDressed = !!cls;
  }

  // Whether a change inside a button needs the column restyled: the dock's icon was taken out (the button's content
  // redrawn), or a new button came into a wrapper (CoreUI0 wraps its buttons).
  function needsDressing(column, record) {
    if (!column.etkDressed) return false;

    var target = record.target;

    if (target.etkIcon && target.etkIcon.parentNode !== target) return true;

    var added = record.addedNodes || [];

    for (var i = 0; i < added.length; i++) {
      if (added[i].nodeType === 1 && added[i].classList && added[i].classList.contains('ui-btn')) return true;
    }

    return false;
  }

  // What about a button can show or hide it: its inline display, its classes and the hidden flag. Cheap to read; the
  // page does not have to work out its styles for it.
  function looksOf(node) {
    return node.style.display + '|' + node.getAttribute('class') + '|' + node.hasAttribute('hidden');
  }

  function remember(column) {
    var children = column.children;

    for (var i = 0; i < children.length; i++) children[i].etkDockLooks = looksOf(children[i]);
  }

  // Whether a batch of changes can move or restyle a button: one added to or taken from the column, one whose display,
  // classes or hidden flag changed since the last layout, or a restyled button that lost the dock's icon. Anything
  // else a button redraws inside itself, and the dock's own grid places, are passed over without reading the page.
  function matters(column, records) {
    for (var i = 0; i < records.length; i++) {
      var record = records[i];
      var target = record.target;

      if (record.type === 'childList') {
        if (target === column || needsDressing(column, record)) return true;

        continue;
      }

      if (target.parentNode === column && target.etkDockLooks !== looksOf(target)) return true;
    }

    return false;
  }

  // One layout of the column. What it wrote itself is waiting for the observer by then, so that is dropped.
  function pass(column) {
    column.etkDockDue = false;

    try {
      layout(column);
    } catch (e) {}

    try {
      theme(column);
    } catch (e) {}

    try {
      remember(column);
    } catch (e) {}

    try {
      if (column.etkDockObserver && column.etkDockObserver.takeRecords) column.etkDockObserver.takeRecords();
    } catch (e) {}
  }

  // One layout per column per frame however many changes came in, before the frame is drawn.
  function passSoon(column) {
    if (column.etkDockDue) return;

    if (typeof requestAnimationFrame === 'undefined') {
      pass(column);

      return;
    }

    column.etkDockDue = true;
    requestAnimationFrame(function () { pass(column); });
  }

  // Lays the column out again in the frame a button is added, removed, shown or hidden, so a new button never shows
  // in a tall column first.
  function watch(column) {
    if (column.etkDockObserver || typeof MutationObserver === 'undefined') return;

    column.etkDockObserver = new MutationObserver(function (records) {
      try {
        if (matters(column, records)) passSoon(column);
      } catch (e) {}
    });
    column.etkDockObserver.observe(column, { childList: true, attributes: true, subtree: true, attributeFilter: ['style', 'class', 'hidden'] });
  }

  // A column still in its page with its watcher on is left to the watcher.
  function isWatched(frame, doc) {
    var column = frame.etkDockColumn;

    return !!column && !!column.etkDockObserver && column.ownerDocument === doc && column.isConnected === true;
  }

  // The poll finds the column of a HUD page that is not watched yet: one just loaded, or a new column in place of
  // the last. Without observers it is all there is, so it lays the column out each time.
  function poll() {
    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      if (!HUD_PAGES.test(String(frames[i].src || ''))) continue;

      try {
        var doc = frames[i].contentDocument;

        if (!doc || isWatched(frames[i], doc)) continue;

        var column = doc.querySelector('.vertical-tools');

        if (!column) continue;

        frames[i].etkDockColumn = column;
        addStyle(doc);
        watch(column);
        pass(column);
      } catch (e) {}
    }
  }

  // A new HUD button size, drawn at once on every column found so far.
  function size(value) {
    var next = Math.max(0.25, Math.min(2, Math.round((Number(value) || 1) * 1000) / 1000));

    if (next === buttonZoom) return;

    buttonZoom = next;

    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      var column = frames[i].etkDockColumn;

      if (column && column.isConnected) pass(column);
    }
  }

  // A new button look, drawn at once on every column found so far. style is HudButtonStyle's value; mono draws every
  // icon in one colour; extras puts Field kit's extras on Lamplight and Signal.
  function setLook(style, mono, extras) {
    var next = { cls: STYLE_CLASSES.hasOwnProperty(style) ? STYLE_CLASSES[style] : '', mono: !!mono, extras: !!extras };

    if (next.cls === look.cls && next.mono === look.mono && next.extras === look.extras) return;

    look = next;

    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      var column = frames[i].etkDockColumn;

      if (column && column.isConnected) pass(column);
    }
  }

  setInterval(poll, POLL_MS);
  poll();

  window.__exiledtoolkitdock = { slots: slots, layout: layout, size: size, look: setLook };
})();
