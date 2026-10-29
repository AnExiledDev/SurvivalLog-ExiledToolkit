// Just enough of the game's web view for ui.js to run in Jint: the root window and the game's page frames, each with
// its own size and ResizeObserver, and panels whose drawn box is their own size times their zoom. Frames are drawn
// only when a test asks, and each window's observers hear the boxes that changed after each frame, as in a browser.
// Test code only; nothing here ships.
var window = this;

var frameCallbacks = [];
var views = [];
// Whether an observer counts a panel's own zoom as a change of size. Chromium's choice is not something a test can
// know, so the tests run both ways.
var observerSeesZoom = true;

function requestAnimationFrame(callback) {
  frameCallbacks.push(callback);

  return frameCallbacks.length;
}

// Draws one frame: what was put off until then runs, then the observers hear what changed size.
function nextFrame() {
  var due = frameCallbacks;

  frameCallbacks = [];
  due.forEach(function (callback) { callback(); });
  views.forEach(function (view) {
    view.observers.forEach(function (observer) { observer.deliver(); });
  });
}

// Draws frames until nothing more is due and returns how many it drew; a page that never settles stops at 50.
function settle() {
  var count = 0;

  do {
    nextFrame();
    count++;
  } while (frameCallbacks.length > 0 && count < 50);

  return count;
}

// ===== Windows =====

function makeObserver(view) {
  return class {
    constructor(callback) {
      this.callback = callback;
      this.watched = [];
      view.observers.push(this);
    }

    observe(el) {
      if (!this.watched.some(function (w) { return w.el === el; })) this.watched.push({ el: el, width: -1, height: -1 });
    }

    unobserve(el) {
      this.watched = this.watched.filter(function (w) { return w.el !== el; });
    }

    deliver() {
      var entries = [];

      this.watched.forEach(function (w) {
        var size = w.el.observedSize();

        if (size.width === w.width && size.height === w.height) return;

        w.width = size.width;
        w.height = size.height;
        entries.push({ target: w.el });
      });

      if (entries.length > 0) this.callback(entries);
    }
  };
}

function makeView(view, width, height) {
  view.innerWidth = width;
  view.innerHeight = height;
  view.observers = [];
  view.listeners = {};
  view.ResizeObserver = makeObserver(view);
  view.elements = [];
  view.document = { defaultView: view };
  view.document.body = new FakeElement(view, width, height);
  view.document.body.isBody = true;
  views.push(view);

  return view;
}

window.addEventListener = function (type, listener) {
  (window.listeners[type] = window.listeners[type] || []).push(listener);
};

// A game page: a full-screen frame in the root with a window of its own.
function addPage() {
  var view = makeView({}, window.innerWidth, window.innerHeight);

  view.frameElement = { isConnected: true, contentDocument: view.document };

  return view;
}

// The game loads the page again: the frame shows a new document and the old one's elements are left behind.
function reloadPage(view) {
  view.frameElement.contentDocument = { defaultView: view };
}

function closePage(view) {
  view.frameElement.isConnected = false;
}

// The game window changes size; every page fills it.
function resizeWindow(width, height) {
  views.forEach(function (view) {
    view.innerWidth = width;
    view.innerHeight = height;
  });
  (window.listeners.resize || []).forEach(function (listener) { listener({ type: 'resize' }); });
}

// ===== Elements =====

var FakeStyle = class {
  constructor() {
    this.value = '';
    this.zoomWrites = 0;
  }

  get zoom() { return this.value; }

  set zoom(value) {
    this.value = String(value);
    this.zoomWrites++;
  }
};

// Drawn at its own width and height times its zoom and every parent's; a filling element (a full-screen backdrop)
// takes its window's size whatever its zoom. A hidden one, one inside a hidden one, or one out of its page has no box.
var FakeElement = class {
  constructor(view, width, height) {
    this.view = view;
    this.width = width;
    this.height = height;
    this.parentNode = null;
    this.style = new FakeStyle();
    this.hidden = false;
    this.fills = false;
    this.tagName = 'DIV';
    this.className = '';
    view.elements.push(this);
  }

  get ownerDocument() { return this.view.document; }

  get isConnected() {
    for (var node = this; node; node = node.parentNode) {
      if (node.isBody) return true;
    }

    return false;
  }

  get currentCSSZoom() {
    var own = Number(this.style.zoom) || 1;

    return this.parentNode ? own * this.parentNode.currentCSSZoom : own;
  }

  // Elements inside this one with that tag, in the order they were made.
  querySelectorAll(tag) {
    var self = this;

    return this.view.elements.filter(function (el) {
      if (el === self || el.tagName !== tag.toUpperCase()) return false;

      for (var node = el.parentNode; node; node = node.parentNode) {
        if (node === self) return true;
      }

      return false;
    });
  }

  isShown() {
    return !this.hidden && (!this.parentNode || this.parentNode.isShown());
  }

  getBoundingClientRect() {
    if (!this.isConnected || !this.isShown()) return { left: 0, top: 0, width: 0, height: 0 };

    if (this.fills) return { left: 0, top: 0, width: this.view.innerWidth, height: this.view.innerHeight };

    var zoom = this.currentCSSZoom;

    return { left: 0, top: 0, width: this.width * zoom, height: this.heightAt ? this.heightAt(zoom) : this.height * zoom };
  }

  observedSize() {
    var box = this.getBoundingClientRect();
    var zoom = observerSeesZoom ? 1 : this.currentCSSZoom;

    return { width: box.width / zoom, height: box.height / zoom };
  }
};

// A panel a mod draws in this window (the root when none is given), shown and in its page.
function panel(width, height, view, parent) {
  var el = new FakeElement(view || window, width, height);

  el.parentNode = parent || el.view.document.body;

  return el;
}

// A game page that fits itself to its window (createViewportFit, webui-bag.js:1259) with a column a mod adds: the
// game's own panels, gameWidth across, and the column beside them, in one box the page measures at its own size. When
// the box is too big for the window less 24 px, fit() zooms the page's body down until it fits.
function selfFittingPage(gameWidth, columnWidth, height) {
  var view = addPage();
  var box = panel(0, height, view);
  var column = panel(columnWidth, height, view, box);

  Object.defineProperty(box, 'width', { get: function () { return gameWidth + columnWidth * (Number(column.style.zoom) || 1); } });
  view.box = box;
  view.column = column;
  view.fit = function () {
    var k = Math.min(1, (view.innerWidth - 24) / box.width, (view.innerHeight - 24) / box.height);

    view.document.body.style.zoom = k < 1 ? k : '';
  };

  return view;
}

// What a test reads: the zoom a panel was given and its drawn box, rounded.
function zoomOf(el) {
  return el.style.zoom;
}

function drawn(el) {
  var box = el.getBoundingClientRect();

  return Math.round(box.width) + 'x' + Math.round(box.height);
}

// The shim each mod carries, word for word, so the tests cover what the mods run.
function uiSize(el, options) {
  var ui = window.ExiledUi;
  if (ui) {
    ui.size(el, options);
    return;
  }
  var waiting = window.__exiledUiWait = window.__exiledUiWait || [];
  waiting.push([el, options]);
  if (waiting.length > 32) waiting.shift();
}

makeView(window, 1920, 1080);
window.frameElement = null;
