// Exiled's Toolkit's page API, run in the game's root web document above every page: the helpers each mod's page
// script used to carry its own copy of. window.ExiledToolkit holds them:
//
// - send(prefix, message): a message to the mod's C# side through the vuplex bridge.
// - addStyle(doc, id, css): one stylesheet per id in a document, kept up to date.
// - watchPages(pattern, fn, options): fn(doc, frame) for each game page whose address matches, once per document it
//   loads, from one shared check every quarter of a second.
// - addHudButton(options): a button in the HUD's tool column, put back whenever the HUD page is rebuilt.
//
// A mod's script can run before this one. It waits with
//   (window.ExiledToolkitReady = window.ExiledToolkitReady || []).push(function (etk) { ... });
// which this script drains when it loads, and which runs the function at once after that.
(function () {
  if (window.ExiledToolkit) return;

  var POLL_MS = 250;
  var HUD_PAGES = /CoreUI1\.html/;
  var PREP_HUD_PAGE = /CoreUI0\.html/;
  var HUD_STYLE_ID = 'etk-hud-style';
  var HUD_CLASS = 'etk-hud-btn';
  var BADGE_CLASS = 'etk-hud-badge';
  var ORDER_ATTR = 'data-etk-dock';
  var SVG_NS = 'http://www.w3.org/2000/svg';
  var CLASS_NAME = /^[A-Za-z_][\w-]*$/;
  var HUD_CSS = [
    '.' + HUD_CLASS + '{position:relative}',
    '.' + HUD_CLASS + ' .' + BADGE_CLASS + '{position:absolute;top:-6px;right:-6px;min-width:18px;height:18px;padding:0 5px;border-radius:9px;background:#e53935;color:#fff;font-size:11px;font-weight:700;line-height:18px;text-align:center;pointer-events:none;box-shadow:0 1px 4px rgba(0,0,0,.6)}'
  ].join('\n');

  var api = { version: 1, send: send, addStyle: addStyle, watchPages: watchPages, addHudButton: addHudButton };

  window.ExiledToolkit = api;
  takeWaiting();

  // ===== Load order =====

  // The functions mods queued before this script loaded run now, oldest first; from now on a push runs at once.
  function takeWaiting() {
    var waiting = window.ExiledToolkitReady;

    window.ExiledToolkitReady = { push: function (fn) { callReady(fn); return 0; } };

    if (!waiting || typeof waiting.length !== 'number') return;

    for (var i = 0; i < waiting.length; i++) callReady(waiting[i]);
  }

  // One mod's start that throws must not stop the next one's.
  function callReady(fn) {
    if (typeof fn !== 'function') return;

    try {
      fn(api);
    } catch (e) {}
  }

  // ===== send and addStyle =====

  // True when the bridge took the message; false while it is not there yet or it threw.
  function send(prefix, message) {
    var bridge = window.vuplex;

    if (!bridge) return false;

    try {
      bridge.postMessage(String(prefix) + message);
    } catch (e) {
      return false;
    }

    return true;
  }

  // The style element with this id in doc, made when missing and its text written only when it differs. The element
  // is kept on the document, so a call with it still in place searches nothing. Null for a document with no head or
  // body yet.
  function addStyle(doc, id, css) {
    var text = css === undefined || css === null ? '' : String(css);
    var styles = doc.__etkStyles || (doc.__etkStyles = {});
    var style = styles[id];

    if (!style || style.isConnected !== true) style = doc.getElementById(id);

    if (!style) {
      var parent = doc.head || doc.body;

      if (!parent) return null;

      style = doc.createElement('style');
      style.id = id;
      parent.appendChild(style);
    }

    if (style.textContent !== text) style.textContent = text;

    styles[id] = style;

    return style;
  }

  // ===== Page watch =====

  // { id, pattern, fn, isEveryCheck }
  var watchers = [];
  var nextWatcherId = 1;
  var isPolling = false;

  // fn(doc, frame) runs for each game page whose frame address matches pattern (a RegExp without the g flag): once
  // for each document the frame loads, as soon as the check finds it, never for the empty document a new frame holds
  // first. When fn returns false it is asked again at the next check, say until the game has drawn what it looks
  // for. options.everyCheck: true asks at every check, for a mod that keeps something in the page and checks it is
  // still there; keep that cheap. Returns { refresh, stop }: refresh() asks again now for every page shown, say after
  // the mod's own state changed; stop() ends the watch.
  function watchPages(pattern, fn, options) {
    if (!pattern || typeof pattern.test !== 'function') throw new TypeError('watchPages needs a RegExp');
    if (typeof fn !== 'function') throw new TypeError('watchPages needs a function');

    var watcher = { id: nextWatcherId++, pattern: pattern, fn: fn, isEveryCheck: !!(options && options.everyCheck) };

    watchers.push(watcher);
    startPolling();
    checkFor(watcher, false);

    return {
      refresh: function () { if (watchers.indexOf(watcher) >= 0) checkFor(watcher, true); },
      stop: function () { watchers = watchers.filter(function (w) { return w !== watcher; }); }
    };
  }

  function startPolling() {
    if (isPolling) return;

    isPolling = true;
    setInterval(poll, POLL_MS);
  }

  // One check for every watcher. With nothing to watch it returns at once; a page every watcher is done with costs a
  // property read per frame.
  function poll() {
    if (watchers.length === 0) return;

    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      var page = pageOf(frames[i]);

      if (!page) continue;

      for (var j = 0; j < watchers.length; j++) ask(watchers[j], page, false);
    }
  }

  function checkFor(watcher, isForced) {
    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      var page = pageOf(frames[i]);

      if (page) ask(watcher, page, isForced);
    }
  }

  // What the check knows about the document a frame holds now, made again when the frame loads a new one. Null while
  // the frame holds no page: none at all, one from another origin, or the empty one a new frame starts with.
  function pageOf(frame) {
    var doc = null;

    try {
      doc = frame.contentDocument;
    } catch (e) {}

    if (!doc || isBlank(doc)) return null;

    var page = frame.__etkPage;

    if (!page || page.doc !== doc) page = frame.__etkPage = { frame: frame, doc: doc, src: String(frame.src || ''), matches: {}, done: {} };

    return page;
  }

  function isBlank(doc) {
    var view = doc.defaultView;
    var href = view && view.location ? String(view.location.href) : '';

    return !href || href === 'about:blank';
  }

  function ask(watcher, page, isForced) {
    var isMatch = page.matches[watcher.id];

    // A document's address does not change, so each watcher tests it once.
    if (isMatch === undefined) isMatch = page.matches[watcher.id] = watcher.pattern.test(page.src);

    if (!isMatch) return;
    if (page.done[watcher.id] && !watcher.isEveryCheck && !isForced) return;

    var answer;

    try {
      answer = watcher.fn(page.doc, page.frame);
    } catch (e) {
      // A page torn down mid-change, or a game update that reshaped it: asked again at the next check.
      answer = false;
    }

    page.done[watcher.id] = answer !== false;
  }

  // The frame is still in the root page and still holds this document: a removed or reloaded frame keeps its old
  // elements connected to their old document, so isConnected alone does not tell.
  function isLive(frame, doc) {
    try {
      return frame.isConnected === true && frame.contentDocument === doc;
    } catch (e) {
      return false;
    }
  }

  // ===== HUD buttons =====

  // The buttons by id, so adding one with an id in use replaces it.
  var hudButtons = {};

  // A button in the HUD's tool column, drawn like the game's bag button, in every HUD page that matches
  // options.pages (default CoreUI1; add CoreUI0 for before the disaster, where it is drawn in that page's wrapper).
  // options: id (the button's class, unique), order (its data-etk-dock order), icon (an svg path's d on a 24 grid),
  // evenOdd, title, tip, key (shown as a keycap in the tip), badge (a count, or text; 0 or '' for none), badgeClass,
  // shown (default true), onClick(event), onPlace(node, doc). Returns { update(fields), remove() }: update writes the
  // fields given into every button shown, in place.
  function addHudButton(options) {
    var id = options && options.id;

    if (typeof id !== 'string' || !CLASS_NAME.test(id)) throw new TypeError('addHudButton needs an id that is a class name');

    if (hudButtons[id]) hudButtons[id].remove();

    var button = { id: id, state: hudState(options), placements: [] };

    button.watch = watchPages(options.pages || HUD_PAGES, function (doc, frame) { return placeIn(button, doc, frame); }, { everyCheck: true });
    button.update = function (fields) { updateButton(button, fields); };
    button.remove = function () { removeButton(button); };
    hudButtons[id] = button;

    return { update: button.update, remove: button.remove };
  }

  function hudState(options) {
    return {
      order: options.order === undefined || options.order === null ? '0' : String(options.order),
      icon: String(options.icon || ''),
      isEvenOdd: !!options.evenOdd,
      title: String(options.title || ''),
      tip: String(options.tip || ''),
      key: String(options.key || ''),
      badge: badgeText(options.badge),
      badgeClass: String(options.badgeClass || ''),
      isShown: options.shown !== false,
      onClick: typeof options.onClick === 'function' ? options.onClick : null,
      onPlace: typeof options.onPlace === 'function' ? options.onPlace : null
    };
  }

  function badgeText(badge) {
    if (typeof badge === 'number') return badge > 99 ? '99+' : badge > 0 ? String(Math.floor(badge)) : '';

    return badge === undefined || badge === null ? '' : String(badge);
  }

  function updateButton(button, fields) {
    if (hudButtons[button.id] !== button || !fields) return;

    var state = button.state;

    if ('title' in fields) state.title = String(fields.title || '');
    if ('tip' in fields) state.tip = String(fields.tip || '');
    if ('key' in fields) state.key = String(fields.key || '');
    if ('badge' in fields) state.badge = badgeText(fields.badge);
    if ('onClick' in fields) state.onClick = typeof fields.onClick === 'function' ? fields.onClick : null;

    var wasShown = state.isShown;

    if ('shown' in fields) state.isShown = fields.shown !== false;

    button.placements = livePlacements(button);
    button.placements.forEach(function (at) { draw(at, state); });

    if (state.isShown !== wasShown) button.watch.refresh();
  }

  function removeButton(button) {
    if (hudButtons[button.id] !== button) return;

    delete hudButtons[button.id];
    button.watch.stop();
    button.placements.forEach(takeOut);
    button.placements = [];
  }

  // The button in one HUD page: left alone while it is still in the column it was put in, so a check finds it with
  // no search; put back when the game rebuilt the column, and taken out while it is not to be shown. False while the
  // page has no column yet.
  function placeIn(button, doc, frame) {
    var at = placementIn(button, doc, frame);
    var state = button.state;

    if (!state.isShown) {
      takeOut(at);
      return true;
    }

    if (at.column && at.node.parentNode === at.column && at.column.isConnected === true) return true;

    var column = doc.querySelector('.vertical-tools');

    if (!column) return false;

    if (!at.node) build(at, button.id, state);

    column.insertBefore(at.node, column.firstChild);
    at.column = column;

    if (state.onPlace) {
      try {
        state.onPlace(at.node, doc);
      } catch (e) {}
    }

    return true;
  }

  // The record of this button in doc, made the first time the button is asked about that document. Buttons of the
  // same id an older copy of the mod's script left there are taken out then, once.
  function placementIn(button, doc, frame) {
    for (var i = 0; i < button.placements.length; i++) {
      if (button.placements[i].doc === doc) return button.placements[i];
    }

    // Only a page not seen before gets here, so the pages gone since are dropped here and not on every check.
    button.placements = livePlacements(button);

    var leftovers = doc.querySelectorAll('.' + button.id);

    for (var j = 0; j < leftovers.length; j++) detach(leftovers[j]);

    var at = { frame: frame, doc: doc, isPrep: PREP_HUD_PAGE.test(String(frame.src || '')), node: null, column: null, drawn: null };

    button.placements.push(at);

    return at;
  }

  function livePlacements(button) {
    return button.placements.filter(function (at) { return isLive(at.frame, at.doc); });
  }

  function takeOut(at) {
    if (at.node) detach(at.node);

    at.column = null;
  }

  function detach(node) {
    if (node.parentNode) node.parentNode.removeChild(node);
  }

  // CoreUI1's markup is the game's bag button: the icon, then the tip with its title, keycap and text. CoreUI0 wraps
  // each button with its tip beside it, and its tip has no keycap.
  function build(at, id, state) {
    var doc = at.doc;
    var button = make(doc, 'button', 'ui-btn icon-sq interactive-btn ' + HUD_CLASS + (at.isPrep ? '' : ' ' + id));

    addStyle(doc, HUD_STYLE_ID, HUD_CSS);
    button.setAttribute('data-interactive', '');
    button.appendChild(icon(doc, state));
    button.addEventListener('click', function (e) {
      if (e && e.stopPropagation) e.stopPropagation();

      var onClick = state.onClick;

      if (!onClick) return;

      try {
        onClick(e);
      } catch (err) {}
    });

    if (at.isPrep) {
      at.node = make(doc, 'div', 'tooltip-wrapper ' + id);
      at.tip = make(doc, 'div', 'btn-tooltip btn-tooltip--right');
      at.title = at.tip.appendChild(make(doc, 'div', 'tip-name'));
      at.body = at.tip.appendChild(make(doc, 'div', 'tip-desc'));
      at.key = null;
      at.node.appendChild(button);
      at.node.appendChild(at.tip);
    } else {
      at.node = button;
      at.tip = make(doc, 'div', 'entry-tip');
      at.title = at.tip.appendChild(make(doc, 'div', 'entry-tip-title'));
      at.body = at.tip.appendChild(make(doc, 'div', 'entry-tip-body'));
      at.key = make(doc, 'span', 'entry-tip-key');
      button.classList.add('has-entry-tip');
      button.appendChild(at.tip);
    }

    at.button = button;
    at.badge = make(doc, 'span', BADGE_CLASS + ' ' + state.badgeClass);
    at.node.setAttribute(ORDER_ATTR, state.order);
    at.drawn = { title: null, tip: null, key: null, badge: null };
    draw(at, state);
  }

  function icon(doc, state) {
    var svg = doc.createElementNS(SVG_NS, 'svg');
    var path = doc.createElementNS(SVG_NS, 'path');

    svg.setAttribute('class', 'icon-svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    path.setAttribute('d', state.icon);

    if (state.isEvenOdd) path.setAttribute('fill-rule', 'evenodd');

    svg.appendChild(path);

    return svg;
  }

  // Writes only what changed since the last draw, so the HUD page's own watchers, and the dock's, are not woken for
  // nothing.
  function draw(at, state) {
    var drawn = at.drawn;

    if (!drawn) return;

    if (drawn.title !== state.title) {
      at.title.textContent = state.title;
      drawn.title = state.title;
      // Writing the title's text took the keycap out with it.
      drawn.key = null;
    }

    if (at.key && drawn.key !== state.key) {
      at.key.textContent = state.key;
      drawn.key = state.key;

      if (state.key) at.title.appendChild(at.key);
      else detach(at.key);
    }

    if (drawn.tip !== state.tip) {
      at.body.textContent = state.tip;
      drawn.tip = state.tip;
    }

    if (drawn.badge !== state.badge) {
      at.badge.textContent = state.badge;
      drawn.badge = state.badge;

      if (!state.badge) detach(at.badge);
      else if (at.badge.parentNode !== at.button) at.button.insertBefore(at.badge, at.isPrep ? null : at.tip);
    }
  }

  // An element with these classes; classList, so nothing depends on how a page parses a class attribute.
  function make(doc, tag, classes) {
    var node = doc.createElement(tag);
    var names = classes.split(' ');

    for (var i = 0; i < names.length; i++) {
      if (names[i]) node.classList.add(names[i]);
    }

    return node;
  }
})();
