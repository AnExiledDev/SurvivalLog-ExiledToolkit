// Exiled's Toolkit: the size mods draw their own windows and panels at, run in the game's root web document above
// every page. The player sets it as Interface > UiScale and the C# side sends it through ExiledUi.set. A mod hands
// each panel it draws to ExiledUi.size once, when it makes the panel, and the panel is zoomed to that size at once and
// again after every change. A panel that would then be bigger than its window is shrunk until it fits, but never below
// its own size. A panel taken out of its page is dropped, so a mod that puts one back hands it again. A mod whose
// script ran before this one leaves its panels in window.__exiledUiWait as [element, options], taken here. A mod's own
// UI size setting, sent through ExiledUi.mod, overrides the player's size for the panels that mod hands over with its
// name.
(function () {
  if (window.ExiledUi) return;

  // A shrunk panel keeps this share of its window, so its edges stay clear of the screen's.
  var MARGIN = 0.96;
  // A zoom this close to the one already set is not written again.
  var STEP = 0.005;
  // Passes in a row that change one panel's zoom before it is left as it is until the size or the window changes. A
  // panel sized by its window changes size with its own zoom and settles in a few; one that never settles must not
  // lay the page out every frame.
  var MAX_TRIES = 8;

  // { el, fit: the element measured or null, onZoom, pageFits, mod: the mod's name or '', zoom: as last written, tries,
  // room: the zoom it was last shrunk to, or 0 }
  var items = [];
  var scale = 1;
  // The size each mod with its own setting draws at, by the name it hands its panels over with.
  var mods = {};
  var isDue = false;

  var api = { version: 2, scale: scale, size: size, set: set, mod: setMod, scaleOf: scaleOf, zoomAt: zoomAt };

  window.ExiledUi = api;
  window.addEventListener('resize', windowResized);
  takeWaiting();

  // ===== What mods and the C# side call =====

  // options.fit: the element whose box must fit the window (the window inside a full-screen backdrop), or false for
  // a panel that is never shrunk. options.onZoom(zoom): runs after each change, say to have the game page fit itself
  // to its window again. options.mod: the mod's name, so the panel takes that mod's own size when it has one.
  // options.pageFits: true for a panel inside a game page that zooms its own body down when its
  // panels are too big for its window: the fit element is measured at the page's own size, so the panel grows only as
  // far as the page still shows at full size and the game's own panels keep theirs.
  function size(el, options) {
    if (!el || !el.style) return;

    var fit = options && options.fit === false ? null : isElement(options && options.fit) ? options.fit : el;
    var onZoom = options && typeof options.onZoom === 'function' ? options.onZoom : null;
    var pageFits = !!(options && options.pageFits);
    var mod = options && typeof options.mod === 'string' ? options.mod : '';
    var item = itemFor(el);

    if (item && item.fit === fit && item.mod === mod) {
      item.onZoom = onZoom;
      item.pageFits = pageFits;
      return;
    }

    if (!item) {
      item = { el: el, fit: null, onZoom: null, pageFits: false, mod: '', zoom: Number(el.style.zoom) || 1, tries: 0, room: 0 };
      items.push(item);
    }

    unwatch(item.fit);
    item.fit = fit;
    item.onZoom = onZoom;
    item.pageFits = pageFits;
    item.mod = mod;
    item.tries = 0;
    item.room = 0;
    watch(fit);
    schedule();
  }

  function set(value) {
    var next = clamped(value);

    if (next === scale) return;

    scale = next;
    api.scale = scale;
    retryAll();
  }

  // A mod's own size, as a factor; 0 or nothing has its panels follow the player's size again.
  function setMod(name, value) {
    if (typeof name !== 'string' || !name) return;

    var next = Number(value) > 0 ? clamped(value) : 0;

    if ((mods[name] || 0) === next) return;

    if (next) mods[name] = next;
    else delete mods[name];

    retryAll();
  }

  // The size a mod's panels are drawn at: its own when it has one, the player's otherwise.
  function scaleOf(name) {
    return (name && mods[name]) || scale;
  }

  // The zoom el is drawn at by the panels it sits in: the zooms this script wrote on el and the elements around it,
  // multiplied, and 1 outside any panel. A page's own zoom is not counted. A tooltip a mod draws on the page's body for
  // something inside a panel takes this zoom, so the two show at the same size.
  function zoomAt(el) {
    var zoom = 1;

    if (!items.length) return zoom;

    for (var node = el; node && node.style; node = node.parentNode) {
      var item = itemFor(node);

      if (item) zoom *= item.zoom;
    }

    return rounded(zoom);
  }

  // ===== When panels are fitted =====

  // A bigger or smaller window can let a shrunk panel grow back, or make one too big.
  function windowResized() {
    if (isAnyBig()) retryAll();
  }

  // A panel that changed size, say as it was shown or filled, is fitted again. The observer runs after layout and
  // before the frame is drawn, so fitting here, not on the next frame, keeps a panel that just opened from showing
  // one frame at the full size first.
  function resized(entries) {
    if (!isAnyBig()) return;

    for (var i = 0; i < entries.length; i++) {
      if (itemMeasuring(entries[i].target)) {
        pass();
        return;
      }
    }
  }

  function retryAll() {
    items.forEach(function (item) { item.tries = 0; });
    schedule();
  }

  function schedule() {
    if (isDue) return;

    isDue = true;
    window.requestAnimationFrame(pass);
  }

  // ===== The pass =====

  function pass() {
    var measured = [];

    isDue = false;

    for (var i = items.length - 1; i >= 0; i--) {
      var item = items[i];
      var own = scaleOf(item.mod);

      if (!isLive(item.el)) {
        unwatch(item.fit);
        items.splice(i, 1);
      } else if (own <= 1 || !item.fit) {
        // Smaller than the panel's own size always fits, and a panel never shrunk takes the size as it is.
        write(item, own);
      } else if (followFit(item) && item.tries < MAX_TRIES) {
        // A panel whose window was drawn again and not found keeps the zoom it has rather than taking the full size
        // unfitted.
        measured.push(item);
      }
    }

    // Every box is read before any zoom is written, so the pass lays the page out once.
    var boxes = measured.map(measure);
    var isAnyFitted = false;

    measured.forEach(function (item, k) {
      var box = boxes[k];
      var own = scaleOf(item.mod);

      // A hidden panel takes the size now and is fitted once it shows, which changes its size. One that had to be
      // shrunk before takes no more than that, so a big window closed and opened again does not show too big for a
      // frame first.
      if (!(box.width > 0 && box.height > 0)) {
        write(item, item.room ? Math.min(own, item.room) : own);
        return;
      }

      var zoom = fitted(item, box, item.fit.ownerDocument.defaultView, own);

      item.room = zoom < own ? zoom : 0;

      if (write(item, zoom)) {
        item.tries++;
        isAnyFitted = true;
      } else {
        item.tries = 0;
      }
    });

    // The next frame looks again, so a fit is known to have settled whether or not the new zoom changed the box.
    if (isAnyFitted) schedule();
  }

  // The zoom that shows the panel at its size (the player's or its mod's) or, when that would not fit its window, the
  // biggest that does, worked out from its box at the zoom it has now.
  function fitted(item, box, view, target) {
    var grow = target / item.zoom;
    var width = box.width * grow;
    var height = box.height * grow;
    var roomX = width > view.innerWidth ? view.innerWidth * MARGIN / width : 1;
    var roomY = height > view.innerHeight ? view.innerHeight * MARGIN / height : 1;

    return Math.min(target, Math.max(1, target * Math.min(roomX, roomY)));
  }

  // True when the zoom changed.
  function write(item, zoom) {
    zoom = rounded(zoom);

    if (Math.abs(zoom - item.zoom) < STEP) return false;

    item.zoom = zoom;
    item.el.style.zoom = zoom === 1 ? '' : String(zoom);

    if (item.onZoom) {
      try {
        item.onZoom(zoom);
      } catch (e) {
        // A mod's own follow-up must not stop the other panels.
      }
    }

    return true;
  }

  // ===== Helpers =====

  // The fit element's box in its window or, for a page that fits itself, at the page's own size: the box over the
  // zoom the page set on its body.
  function measure(item) {
    var box = item.fit.getBoundingClientRect();
    var zoom = item.pageFits ? bodyZoom(item.fit.ownerDocument) : 1;

    return zoom === 1 ? box : { width: box.width / zoom, height: box.height / zoom };
  }

  function bodyZoom(doc) {
    var zoom = Number(doc && doc.body && doc.body.style.zoom);

    return zoom > 0 ? zoom : 1;
  }

  function clamped(value) {
    return Math.max(0.5, Math.min(3, rounded(Number(value) || 1)));
  }

  // Whether any panel can be drawn above its own size, so a change of window or panel size can need a new fit.
  function isAnyBig() {
    if (scale > 1) return true;

    for (var name in mods) {
      if (mods[name] > 1) return true;
    }

    return false;
  }

  function rounded(n) {
    return Math.round(n * 1000) / 1000;
  }

  // The panel's document is still the one shown in its frame: a removed or reloaded frame keeps its old elements
  // connected to their old document, so isConnected alone does not tell.
  function isLive(el) {
    if (!el || !el.isConnected) return false;

    var doc = el.ownerDocument;
    var view = doc && doc.defaultView;

    if (!view) return false;
    if (view === window) return true;

    var frame = view.frameElement;

    return !!frame && frame.isConnected && frame.contentDocument === doc;
  }

  // A mod that draws its panel again replaces the window inside it, and the one it handed over is left out of the
  // page, measuring nothing. The new one is the element in the panel with the same tag and classes. False when the
  // fit element is gone and no such element is in the panel.
  function followFit(item) {
    if (item.fit === item.el || isLive(item.fit)) return true;

    var next = sameKindIn(item.el, item.fit);

    if (!next) return false;

    unwatch(item.fit);
    item.fit = next;
    item.tries = 0;
    watch(next);

    return true;
  }

  function sameKindIn(panel, old) {
    if (typeof panel.querySelectorAll !== 'function' || !old.tagName) return null;

    var found = panel.querySelectorAll(old.tagName);

    for (var i = 0; i < found.length; i++) {
      if (found[i].className === old.className) return found[i];
    }

    return null;
  }

  function isElement(value) {
    return !!value && typeof value.getBoundingClientRect === 'function' && !!value.style;
  }

  function itemFor(el) {
    for (var i = 0; i < items.length; i++) {
      if (items[i].el === el) return items[i];
    }

    return null;
  }

  function itemMeasuring(target) {
    for (var i = 0; i < items.length; i++) {
      if (items[i].fit === target) return items[i];
    }

    return null;
  }

  // One observer per window the panels live in, made from that window, so a frame's panels are watched by its own.
  function observerFor(el, isMade) {
    var view = el && el.ownerDocument && el.ownerDocument.defaultView;

    if (!view || typeof view.ResizeObserver !== 'function') return null;

    if (!view.__exiledUiWatch && isMade) view.__exiledUiWatch = new view.ResizeObserver(resized);

    return view.__exiledUiWatch || null;
  }

  function watch(el) {
    var observer = observerFor(el, true);

    if (observer) observer.observe(el);
  }

  function unwatch(el) {
    var observer = observerFor(el, false);

    if (observer) observer.unobserve(el);
  }

  function takeWaiting() {
    var waiting = window.__exiledUiWait;

    window.__exiledUiWait = null;

    if (!waiting || !waiting.length) return;

    waiting.forEach(function (entry) {
      if (entry && isLive(entry[0])) size(entry[0], entry[1]);
    });
  }
})();
