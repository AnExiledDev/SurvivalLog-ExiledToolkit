// Just enough of the game's root web document for toasts.js to run in Jint: elements with classes, text and events,
// a layout that places boxes by their inline styles, the game's page frames with a HUD to measure, a clock the tests
// move by hand, and the vuplex bridge, which can arrive after the script as it can in the game. Test code only;
// nothing here ships.
var window = this;

window.innerWidth = 1920;
window.innerHeight = 1080;

// ===== The clock and timers =====

var now = 1000000;
var timers = [];
var nextTimer = 1;

Date.now = function () { return now; };

function setTimeout(callback, ms) {
  timers.push({ id: nextTimer, due: now + (ms || 0), every: 0, callback: callback });

  return nextTimer++;
}

function setInterval(callback, ms) {
  timers.push({ id: nextTimer, due: now + ms, every: ms, callback: callback });

  return nextTimer++;
}

function clearTimeout(id) {
  timers = timers.filter(function (timer) { return timer.id !== id; });
}

// Moves the clock on, firing every timer that falls due on the way, earliest first, at the time it falls due.
function advance(ms) {
  var until = now + ms;

  for (;;) {
    var due = timers.filter(function (timer) { return timer.due <= until; });

    if (due.length === 0) break;

    due.sort(function (a, b) { return a.due - b.due || a.id - b.id; });

    var timer = due[0];
    now = timer.due;

    if (timer.every) timer.due += timer.every;
    else clearTimeout(timer.id);

    timer.callback();
  }

  now = until;
}

// ===== The vuplex bridge =====

var posted = [];
var windowListeners = {};

window.addEventListener = function (type, listener) {
  (windowListeners[type] = windowListeners[type] || []).push(listener);
};

// As in the game: the bridge object appears, then the window hears vuplexready.
function connectVuplex() {
  window.vuplex = { postMessage: function (message) { posted.push(String(message)); } };
  (windowListeners.vuplexready || []).forEach(function (listener) { listener({ type: 'vuplexready' }); });
}

// ===== Elements =====

// A box is 60 px tall, and 84 px with its text in full (etk-front); its width is whatever its style says.
var BOX_HEIGHT = 60;
var FRONT_HEIGHT = 84;

// An inline style length in px, or null when the style leaves it unset.
function styleLength(value) {
  return value === undefined || value === '' ? null : parseFloat(value);
}

var FakeElement = class {
  constructor(tagName) {
    var element = this;

    this.tagName = tagName;
    this.id = '';
    this.parentNode = null;
    this.children = [];
    this.text = '';
    this.classes = [];
    this.listeners = {};
    this.style = {};
    // How many times each class was added, so a test can tell an animation started again from one left running.
    this.added = {};
    this.classList = {
      add: function (name) {
        element.added[name] = (element.added[name] || 0) + 1;
        if (element.classes.indexOf(name) < 0) element.classes.push(name);
      },
      remove: function (name) {
        element.classes = element.classes.filter(function (other) { return other !== name; });
      },
      toggle: function (name, force) {
        var on = force === undefined ? !element.classList.contains(name) : !!force;

        if (on) element.classList.add(name);
        else element.classList.remove(name);

        return on;
      },
      contains: function (name) { return element.classes.indexOf(name) >= 0; }
    };
  }

  get firstChild() { return this.children.length ? this.children[0] : null; }

  get className() { return this.classes.join(' '); }

  set className(value) { this.classes = String(value).split(' ').filter(function (name) { return name !== ''; }); }

  get textContent() {
    return this.text + this.children.map(function (child) { return child.textContent; }).join('');
  }

  set textContent(value) {
    this.children.slice().forEach(function (child) { this.removeChild(child); }, this);
    this.text = value == null ? '' : String(value);
  }

  // Layout, as a browser does it for the zero-size fixed stack and the absolute boxes in it: the stack sits where its
  // left/right/top/bottom put it, and a box sits by its own sides, measured from the stack. Nothing else takes room.
  get isInStack() { return !!this.parentNode && this.parentNode.id === 'etk-stack'; }

  get offsetLeft() { return this.isInStack ? this.side('left', 'right', this.offsetWidth) : 0; }

  get offsetTop() { return this.isInStack ? this.side('top', 'bottom', this.offsetHeight) : 0; }

  get offsetWidth() { return this.isInStack ? styleLength(this.style.width) || 0 : 0; }

  get offsetHeight() {
    if (!this.isInStack) return 0;

    return this.classList.contains('etk-front') ? FRONT_HEIGHT : BOX_HEIGHT;
  }

  side(start, end, size) {
    var fromStart = styleLength(this.style[start]);
    var fromEnd = styleLength(this.style[end]);

    if (fromStart !== null) return fromStart;

    return fromEnd !== null ? -(fromEnd + size) : 0;
  }

  getBoundingClientRect() {
    if (this.tagName === 'iframe') return { left: 0, top: 0, width: window.innerWidth, height: window.innerHeight };

    if (this.id === 'etk-stack') {
      var left = styleLength(this.style.left);
      var top = styleLength(this.style.top);

      return {
        left: left !== null ? left : window.innerWidth - (styleLength(this.style.right) || 0),
        top: top !== null ? top : window.innerHeight - (styleLength(this.style.bottom) || 0),
        width: 0,
        height: 0
      };
    }

    return { left: 0, top: 0, width: this.offsetWidth, height: this.offsetHeight };
  }

  addEventListener(type, listener) {
    (this.listeners[type] = this.listeners[type] || []).push(listener);
  }

  appendChild(child) {
    if (child.parentNode) child.parentNode.removeChild(child);

    child.parentNode = this;
    this.children.push(child);

    return child;
  }

  insertBefore(child, before) {
    if (!before) return this.appendChild(child);

    if (child.parentNode) child.parentNode.removeChild(child);

    child.parentNode = this;
    this.children.splice(this.children.indexOf(before), 0, child);

    return child;
  }

  removeChild(child) {
    this.children = this.children.filter(function (other) { return other !== child; });
    child.parentNode = null;

    return child;
  }
};

// ===== The game's pages =====

// The root holds one full-screen iframe per game page, its id the page id, as Root.html makes them.
var frames = [];

var document = {
  head: new FakeElement('head'),
  body: new FakeElement('body'),
  documentElement: new FakeElement('html'),
  createElement: function (tagName) { return new FakeElement(tagName); },
  getElementsByTagName: function (tagName) { return tagName === 'iframe' ? frames : []; },
  getElementById: function (id) { return frames.filter(function (frame) { return frame.id === id; })[0] || null; }
};

// A part of a page, laid out at this box in page pixels; a zero box is a part the game hid.
var FakePart = class {
  constructor(selector, left, top, right, bottom) {
    this.selector = selector;
    this.box = { left: left, top: top, right: right, bottom: bottom, width: right - left, height: bottom - top };
  }

  getBoundingClientRect() { return this.box; }

  closest(selector) { return selector === this.selector ? this : null; }
};

var FakePageDocument = class {
  constructor() {
    this.readyState = 'complete';
    this.parts = {};
    this.listeners = {};
  }

  querySelector(selector) { return this.parts[selector] || null; }

  addEventListener(type, listener) {
    (this.listeners[type] = this.listeners[type] || []).push(listener);
  }
};

function addFrame(id, src, zIndex) {
  var frame = new FakeElement('iframe');

  frame.id = id;
  frame.src = src;
  frame.style.zIndex = String(zIndex);
  frame.style.display = 'block';
  frame.style.visibility = 'visible';
  frame.style.pointerEvents = 'auto';
  frame.contentDocument = new FakePageDocument();
  frames.push(frame);

  return frame;
}

function setPart(frame, selector, left, top, right, bottom) {
  frame.contentDocument.parts[selector] = new FakePart(selector, left, top, right, bottom);
}

// The main HUD: the tool column (phone, bag) at the left, the action panel with the queue under it.
function addHud(zIndex) {
  var hud = addFrame('CoreUI1', 'https://webui/UI/CoreUI1/CoreUI1.html?t=1', zIndex);

  setPart(hud, '.vertical-tools', 18, 600, 77, 963);
  setPart(hud, '.action-panel', 18, 980, 900, 1062);

  return hud;
}

// The pointer moves onto the part with this selector; a selector the page has no part for stands for any other part.
function pointAt(frame, selector) {
  var doc = frame.contentDocument;
  var target = doc.parts[selector] || new FakePart(selector, 0, 0, 0, 0);

  (doc.listeners.mouseover || []).forEach(function (listener) { listener({ type: 'mouseover', target: target }); });
}

// Root.html's own function, which the game's pages call with their clickable areas.
var syncedPages = [];

window.syncInteractiveRects = function (pageId, rects, clip) {
  syncedPages.push(pageId);
};

// A click goes to the element, then up through its parents unless a listener stops it; the pointer entering or
// leaving reaches only the element.
function dispatch(element, type) {
  var stopped = false;
  var event = {
    type: type,
    stopPropagation: function () { stopped = true; },
    preventDefault: function () { }
  };

  for (var target = element; target && !stopped; target = type === 'click' ? target.parentNode : null) {
    (target.listeners[type] || []).slice().forEach(function (listener) { listener(event); });
  }
}

// ===== What the tests look at =====

function stack() {
  return document.body.children.filter(function (element) { return element.id === 'etk-stack'; })[0];
}

// Every box in the stack, top first, leaving ones included.
function boxes() {
  return stack().children;
}

// The boxes not on their way out.
function shown() {
  return boxes().filter(function (box) { return !box.classList.contains('etk-out'); });
}

function part(element, className) {
  if (element.classList.contains(className)) return element;

  for (var i = 0; i < element.children.length; i++) {
    var found = part(element.children[i], className);

    if (found) return found;
  }

  return null;
}

// What the player reads on a box: its title and the count badge, if one shows.
function label(box) {
  var count = part(box, 'etk-count');
  var badge = count && count.style.display !== 'none' ? ' ' + count.textContent : '';

  return part(box, 'etk-title').textContent + badge;
}
