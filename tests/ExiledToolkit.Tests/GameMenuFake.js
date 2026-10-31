// Just enough of the root document, the pause menu page and the title screen page for gamemenu.js to run in Jint:
// elements with ids, classes, attributes and inline styles, querySelector for tag, #id, .class, [attr], child and
// descendant combinators, mutation observers the tests flush by hand, timers fired by hand, and a box model that lays
// the two menus out the way the game's CSS does, every size times --slet-fit, so the fit can be checked. Test code
// only; nothing here ships.
var window = this;
var intervals = [];
var timeouts = [];
var posted = [];
var layoutReads = 0;
var observerRounds = 0;
var docs = [];

window.vuplex = { postMessage: function (message) { posted.push(message); } };

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

function setTimeout(callback) {
  timeouts.push(callback);

  return timeouts.length;
}

function poll() {
  intervals.forEach(function (callback) { if (callback) callback(); });
}

function runTimeouts() {
  var due = timeouts;

  timeouts = [];
  due.forEach(function (callback) { callback(); });
}

function Style() {
  this.display = '';
  this.vars = {};
}

Style.prototype.setProperty = function (name, value) { this.vars[name] = String(value); };
Style.prototype.removeProperty = function (name) { delete this.vars[name]; };
Style.prototype.getPropertyValue = function (name) { return name in this.vars ? this.vars[name] : ''; };

function Element(doc, tag) {
  this.ownerDocument = doc;
  this.tagName = tag.toUpperCase();
  this.nodeType = 1;
  this.children = [];
  this.parentNode = null;
  this.attributes = {};
  this.listeners = {};
  this.id = '';
  this.style = new Style();
  this._class = '';
  this._text = '';
}

Object.defineProperty(Element.prototype, 'className', {
  get: function () { return this._class; },
  set: function (value) { this._class = String(value); }
});

Object.defineProperty(Element.prototype, 'classList', {
  get: function () {
    var el = this;
    var list = function () { return el._class.split(/\s+/).filter(function (c) { return c; }); };

    return {
      contains: function (name) { return list().indexOf(name) >= 0; },
      add: function (name) { if (list().indexOf(name) < 0) el._class = list().concat([name]).join(' '); },
      remove: function (name) { el._class = list().filter(function (c) { return c !== name; }).join(' '); }
    };
  }
});

Object.defineProperty(Element.prototype, 'textContent', {
  get: function () { return this._text + this.children.map(function (c) { return c.textContent; }).join(''); },
  set: function (value) { this.children.forEach(function (c) { c.parentNode = null; }); this.children = []; this._text = String(value); }
});

Object.defineProperty(Element.prototype, 'nextElementSibling', {
  get: function () {
    if (!this.parentNode) return null;

    var siblings = this.parentNode.children;

    return siblings[siblings.indexOf(this) + 1] || null;
  }
});

Object.defineProperty(Element.prototype, 'lastElementChild', {
  get: function () { return this.children[this.children.length - 1] || null; }
});

Object.defineProperty(Element.prototype, 'isConnected', {
  get: function () {
    var node = this;

    while (node.parentNode) node = node.parentNode;

    return node === this.ownerDocument.documentElement;
  }
});

Element.prototype.getAttribute = function (name) { return name in this.attributes ? this.attributes[name] : null; };
Element.prototype.setAttribute = function (name, value) { this.attributes[name] = String(value); };
Element.prototype.addEventListener = function (type, callback) { (this.listeners[type] = this.listeners[type] || []).push(callback); };

function record(parent, added, removed) {
  var doc = parent.ownerDocument;

  if (doc.observers.length && parent.isConnected) doc.pending.push({ addedNodes: added, removedNodes: removed });
}

Element.prototype.insertBefore = function (child, before) {
  if (child.parentNode) child.remove();

  var at = before ? this.children.indexOf(before) : -1;

  if (at < 0) this.children.push(child);
  else this.children.splice(at, 0, child);

  child.parentNode = this;
  record(this, [child], []);

  return child;
};

Element.prototype.appendChild = function (child) { return this.insertBefore(child, null); };

Element.prototype.remove = function () {
  var parent = this.parentNode;

  if (!parent) return;

  record(parent, [], [this]);
  parent.children = parent.children.filter(function (c) { return c !== this; }, this);
  this.parentNode = null;
};

Element.prototype.querySelector = function (selector) { return all(this, selector)[0] || null; };
Element.prototype.querySelectorAll = function (selector) { return all(this, selector); };

function matchesCompound(el, compound) {
  var parts = compound.match(/^[a-z]+|#[\w-]+|\.[\w-]+|\[[\w-]+\]/gi) || [];

  return parts.every(function (part) {
    if (part[0] === '#') return el.id === part.slice(1);
    if (part[0] === '.') return el.classList.contains(part.slice(1));
    if (part[0] === '[') return el.getAttribute(part.slice(1, -1)) !== null;

    return el.tagName === part.toUpperCase();
  });
}

function matchesComplex(el, tokens) {
  if (!matchesCompound(el, tokens[tokens.length - 1])) return false;
  if (tokens.length === 1) return true;

  var combinator = tokens[tokens.length - 2];
  var rest = tokens.slice(0, -2);

  if (combinator === '>') return !!el.parentNode && !!el.parentNode.tagName && matchesComplex(el.parentNode, rest);

  for (var node = el.parentNode; node && node.tagName; node = node.parentNode) {
    if (matchesComplex(node, rest)) return true;
  }

  return false;
}

function tokenize(selector) {
  return selector.replace(/\s*>\s*/g, ' > ').trim().split(/\s+/).reduce(function (tokens, token) {
    if (token !== '>' && tokens.length && tokens[tokens.length - 1] !== '>') tokens.push(' ');
    tokens.push(token);

    return tokens;
  }, []);
}

function matches(el, selector) {
  return selector.split(',').some(function (one) { return matchesComplex(el, tokenize(one)); });
}

function all(root, selector) {
  var found = [];

  (function walk(node) {
    node.children.forEach(function (child) {
      if (matches(child, selector)) found.push(child);
      walk(child);
    });
  })(root);

  return found;
}

// ===== The box model =====

function fitOf(doc) {
  return parseFloat(doc.documentElement.style.getPropertyValue('--slet-fit')) || 1;
}

function has(el, name) {
  return el.classList.contains(name);
}

function isShown(el) {
  return el.style.display !== 'none';
}

// The panel's padding, and each child's height and bottom margin, from the game's CSS times the fit.
function padOf(panel) {
  return 80 * fitOf(panel.ownerDocument);
}

function marginBelow(el) {
  var f = fitOf(el.ownerDocument);

  if (has(el, 'menu-header')) return 50 * f;
  if (has(el, 'header')) return 60 * f;

  return 0;
}

function gapOf(list) {
  return (has(list, 'menu-list') ? 15 : 16) * fitOf(list.ownerDocument);
}

function contentHeight(list) {
  var shown = list.children.filter(isShown);
  var sum = shown.reduce(function (total, child) { return total + heightOf(child); }, 0);

  return sum + Math.max(0, shown.length - 1) * gapOf(list);
}

// Heights round up to whole pixels, as text does, so the menu's height is only nearly a straight line in the fit.
function heightOf(el) {
  var f = fitOf(el.ownerDocument);

  if (!isShown(el)) return 0;
  if (has(el, 'main-btn')) return Math.ceil(60 * f);
  if (has(el, 'menu-header')) return Math.ceil(106 * f);
  if (has(el, 'icon-bar')) return Math.ceil(25 * f) + 46;
  if (has(el, 'menu-item')) return Math.ceil(34 * f) + 10;
  if (has(el, 'header')) return Math.ceil(196 * f);
  if (has(el, 'menu-list') || has(el, 'menu')) return grownHeight(el);

  return 0;
}

// The pause list stretches to fill the panel (flex-grow) unless the toolkit is measuring it; it never shrinks below
// what it holds unless it scrolls.
function grownHeight(list) {
  var panel = list.parentNode;
  var content = contentHeight(list);
  var isPause = has(list, 'menu-list');

  if (!panel || has(panel, 'slet-scroll')) return Math.min(content, roomFor(list));
  if (!isPause || has(panel, 'slet-measure')) return content;

  return Math.max(content, roomFor(list));
}

function roomFor(list) {
  var panel = list.parentNode;
  var others = panel.children.filter(function (c) { return c !== list; });
  var taken = others.reduce(function (total, c) { return total + heightOf(c) + marginBelow(c); }, 0);

  return panel.clientHeightRaw() - 2 * padOf(panel) - taken;
}

Element.prototype.clientHeightRaw = function () {
  var frame = this.ownerDocument.defaultView.frameElement;

  return frame.style.display === 'none' ? 0 : this.ownerDocument.defaultView.innerHeight;
};

Object.defineProperty(Element.prototype, 'clientHeight', {
  get: function () {
    layoutReads++;

    return this.clientHeightRaw();
  }
});

Object.defineProperty(Element.prototype, 'offsetHeight', {
  get: function () {
    layoutReads++;

    return heightOf(this);
  }
});

Object.defineProperty(Element.prototype, 'offsetWidth', {
  get: function () { return 0; }
});

// Children of the panel stack from its top padding down.
Object.defineProperty(Element.prototype, 'offsetTop', {
  get: function () {
    layoutReads++;

    var parent = this.parentNode;
    var top = padOf(parent);

    for (var i = 0; i < parent.children.length && parent.children[i] !== this; i++) {
      top += heightOf(parent.children[i]) + marginBelow(parent.children[i]);
    }

    return top;
  }
});

// How tall the panel's content is at the current fit, as a person looking at the screen would see it.
function drawnHeight(panel) {
  var last = panel.lastElementChild;
  var reads = layoutReads;
  var height = last.offsetTop + last.offsetHeight + padOf(panel);

  layoutReads = reads;

  return height;
}

// ===== Documents and pages =====

function makeDocument(width, height) {
  var doc = { observers: [], pending: [] };
  var root = new Element(doc, 'html');

  doc.documentElement = root;
  doc.location = { href: 'about:blank' };
  doc.defaultView = {
    innerWidth: width || 1920,
    innerHeight: height || 1080,
    frameElement: null,
    listeners: {},
    addEventListener: function (type, callback) { (this.listeners[type] = this.listeners[type] || []).push(callback); },
    getComputedStyle: function (el) {
      layoutReads++;

      return { paddingBottom: (has(el, 'left-panel') || has(el, 'container') ? padOf(el) : 0) + 'px' };
    },
    MutationObserver: function (callback) {
      var observer = this;

      observer.observe = function () { doc.observers.push({ callback: callback, observer: observer }); };
      observer.takeRecords = function () { var taken = doc.pending; doc.pending = []; return taken; };
    }
  };
  doc.createElement = function (tag) { return new Element(doc, tag); };
  doc.head = root.appendChild(new Element(doc, 'head'));
  doc.body = root.appendChild(new Element(doc, 'body'));
  doc.querySelector = function (selector) { return root.querySelector(selector); };
  doc.querySelectorAll = function (selector) { return root.querySelectorAll(selector); };
  doc.getElementById = function (id) {
    var found = null;

    (function walk(node) {
      node.children.forEach(function (child) {
        if (!found && child.id === id) found = child;
        walk(child);
      });
    })(root);

    return found;
  };
  docs.push(doc);

  return doc;
}

// Delivers what the pages' observers saw, as the browser does before the next paint, until the pages are quiet.
function flush() {
  for (var round = 0; round < 20; round++) {
    var any = false;

    docs.forEach(function (doc) {
      if (!doc.pending.length) return;

      var records = doc.pending;

      doc.pending = [];
      any = true;
      doc.observers.forEach(function (o) { o.callback(records, o.observer); });
    });

    if (!any) return;

    observerRounds++;
  }
}

var frames = [];
var document = makeDocument();
document.getElementsByTagName = function (tag) { return tag === 'iframe' ? frames : []; };

// A page iframe at UI/<name>/<name>.html with an empty body.
function page(name, width, height) {
  var doc = makeDocument(width, height);
  var url = 'https://game/UI/' + name + '/' + name + '.html';
  var frame = { src: url, contentDocument: doc, isConnected: true, style: { display: '' }, listeners: {} };

  frame.addEventListener = function (type, callback) { (frame.listeners[type] = frame.listeners[type] || []).push(callback); };
  doc.location.href = url;
  doc.defaultView.frameElement = frame;
  frames.push(frame);

  return doc;
}

// Adds an element under parent; spec is "tag#id.class.class", any part optional.
function el(parent, spec, text) {
  var tag = (spec.match(/^[a-z]+/i) || ['div'])[0];
  var id = spec.match(/#([\w-]+)/);
  var classes = spec.match(/\.[\w-]+/g) || [];
  var node = (parent.ownerDocument || parent).createElement(tag);

  if (id) node.id = id[1];

  node.className = classes.map(function (c) { return c.slice(1); }).join(' ');

  if (text) node.textContent = text;

  return (parent.body && !parent.tagName ? parent.body : parent).appendChild(node);
}

function button(list, key, extra) {
  var node = el(list, 'div.main-btn' + (extra || ''), key);

  node.setAttribute('data-i18n', key);

  return node;
}

// The pause menu as OutSetting.html draws it, with the game's Restart button when withRestart.
function pauseMenu(withRestart, height) {
  var doc = page('OutSetting', 1920, height);
  var panel = el(el(doc, 'div#app'), 'div.left-panel');
  var list;

  el(panel, 'div.menu-header', 'PAUSE');
  list = el(panel, 'div.menu-list');
  ['back', 'settings', 'codex', 'achievement'].forEach(function (key) { button(list, key); });
  el(list, 'div.wishlist-container').style.display = 'none';

  if (withRestart) button(list, 'restartRound', '.btn-restart');

  button(list, 'saveExit', '.btn-exit');
  el(panel, 'div.icon-bar');

  return doc;
}

// The title screen as MainUI.html draws it.
function titleScreen(height) {
  var doc = page('MainUI', 1920, height);
  var container = el(el(doc, 'div#app'), 'div.container');
  var menu;

  el(container, 'div.header');
  menu = el(container, 'div.menu');
  ['continue', 'newGame', 'pureEndless', 'loadGame', 'settings', 'exit'].forEach(function (key) {
    var item = el(menu, 'div.menu-item');

    el(item, 'div.bar');
    el(item, 'div.menu-text', key).setAttribute('data-i18n', key);
  });

  return doc;
}

function click(node) {
  var event = { target: node, stopPropagation: function () {}, preventDefault: function () {} };

  for (var at = node; at; at = at.parentNode) {
    (at.listeners.click || []).forEach(function (callback) { callback(event); });
  }
}

// What the menu list holds, in order: the game's buttons by data-i18n, the toolkit's by their id.
function order(doc, selector) {
  return doc.querySelector(selector).children.map(function (child) {
    var text = child.querySelector('.menu-text');

    return child.getAttribute('data-slet-gm') || child.getAttribute('data-i18n') || (text && text.getAttribute('data-i18n')) || child.className;
  }).join(',');
}

function pauseOrder(doc) {
  return order(doc, '.menu-list');
}

function titleOrder(doc) {
  return order(doc, '.menu');
}

function fit(doc) {
  return fitOf(doc);
}

function resize(doc, height) {
  doc.defaultView.innerHeight = height;
  (doc.defaultView.listeners.resize || []).forEach(function (callback) { callback(); });
}

// n pause buttons, the first `top` of them after Settings and the rest at the bottom.
function pauseRows(n, top) {
  var list = [];

  for (var i = 0; i < n; i++) list.push({ id: 'mod.b' + i, label: 'Button ' + i, spot: i < top ? 'top' : 'bottom', order: i, accent: '', wanted: true });

  return list;
}
