// Just enough of the game's root web document for toasts.js to run in Jint: elements with classes, text and events,
// a layout that stacks boxes 90 px apart in the top-right corner, a clock the tests move by hand, and the vuplex
// bridge, which can arrive after the script as it can in the game. Test code only; nothing here ships.
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

var STACK_LEFT = 1600;
var STACK_TOP = 20;
var BOX_WIDTH = 300;
var BOX_HEIGHT = 80;
var BOX_STEP = 90;

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

  // Layout: a box in the stack is 300 x 80, one under another 90 px apart; nothing else takes room.
  get isInStack() { return !!this.parentNode && this.parentNode.id === 'etk-stack'; }

  get offsetLeft() { return 0; }

  get offsetTop() { return this.isInStack ? this.parentNode.children.indexOf(this) * BOX_STEP : 0; }

  get offsetWidth() { return this.isInStack ? BOX_WIDTH : 0; }

  get offsetHeight() { return this.isInStack ? BOX_HEIGHT : 0; }

  getBoundingClientRect() {
    if (this.id === 'etk-stack') return { left: STACK_LEFT, top: STACK_TOP, width: BOX_WIDTH, height: 0 };

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

var document = {
  head: new FakeElement('head'),
  body: new FakeElement('body'),
  documentElement: new FakeElement('html'),
  createElement: function (tagName) { return new FakeElement(tagName); }
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
