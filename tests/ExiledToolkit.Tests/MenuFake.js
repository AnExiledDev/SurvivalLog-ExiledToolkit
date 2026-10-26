// Just enough of the root document and its page iframes for the slmod-menu block to run in Jint: elements with ids,
// classes and attributes, querySelector for the simple selectors the block uses (tag, #id, .class, descendant and
// child combinators, comma lists), clicks, and intervals the tests fire by hand. Test code only; nothing here ships.
var window = this;
var intervals = [];

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

function clearInterval(id) {
  intervals[id - 1] = null;
}

function Element(doc, tag) {
  this.ownerDocument = doc;
  this.tagName = tag.toUpperCase();
  this.children = [];
  this.parentNode = null;
  this.attributes = {};
  this.listeners = {};
  this.id = '';
  this.title = '';
  this.style = { display: '' };
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

Object.defineProperty(Element.prototype, 'firstElementChild', {
  get: function () { return this.children[0] || null; }
});

Object.defineProperty(Element.prototype, 'isConnected', {
  get: function () {
    var node = this;

    while (node.parentNode) node = node.parentNode;

    return node === this.ownerDocument.root;
  }
});

Element.prototype.getAttribute = function (name) { return name in this.attributes ? this.attributes[name] : null; };
Element.prototype.setAttribute = function (name, value) { this.attributes[name] = String(value); };
Element.prototype.addEventListener = function (type, callback) { (this.listeners[type] = this.listeners[type] || []).push(callback); };
Element.prototype.removeEventListener = function () {};

Element.prototype.insertBefore = function (child, before) {
  if (child.parentNode) child.remove();

  var at = before ? this.children.indexOf(before) : -1;

  if (at < 0) this.children.push(child);
  else this.children.splice(at, 0, child);

  child.parentNode = this;

  return child;
};

Element.prototype.appendChild = function (child) { return this.insertBefore(child, null); };

Element.prototype.remove = function () {
  var parent = this.parentNode;

  if (!parent) return;

  parent.children = parent.children.filter(function (c) { return c !== this; }, this);
  this.parentNode = null;
};

Element.prototype.contains = function (other) {
  for (var node = other; node; node = node.parentNode) {
    if (node === this) return true;
  }

  return false;
};

Element.prototype.closest = function (selector) {
  for (var node = this; node && node.tagName; node = node.parentNode) {
    if (matches(node, selector)) return node;
  }

  return null;
};

Element.prototype.querySelector = function (selector) { return all(this, selector)[0] || null; };
Element.prototype.querySelectorAll = function (selector) { return all(this, selector); };

// One compound selector: an optional tag, then #id, .class and [attr] parts.
function matchesCompound(el, compound) {
  var parts = compound.match(/^[a-z]+|#[\w-]+|\.[\w-]+|\[[\w-]+\]/gi) || [];

  return parts.every(function (part) {
    if (part[0] === '#') return el.id === part.slice(1);
    if (part[0] === '.') return el.classList.contains(part.slice(1));
    if (part[0] === '[') return el.getAttribute(part.slice(1, -1)) !== null;

    return el.tagName === part.toUpperCase();
  });
}

// A complex selector read right to left: "a > b" needs b's parent to be a, "a b" any ancestor.
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

function makeDocument() {
  var doc = {};
  var root = new Element(doc, 'html');

  doc.root = root;
  doc.defaultView = { addEventListener: function () {}, removeEventListener: function () {}, frameElement: null };
  doc.createElement = function (tag) { return new Element(doc, tag); };
  doc.createElementNS = function (ns, tag) { return new Element(doc, tag); };
  doc.head = root.appendChild(new Element(doc, 'head'));
  doc.body = root.appendChild(new Element(doc, 'body'));
  doc.querySelector = function (selector) { return root.querySelector(selector); };
  doc.querySelectorAll = function (selector) { return root.querySelectorAll(selector); };
  doc.getElementById = function (id) { return all(root, '#' + id)[0] || null; };

  return doc;
}

var frames = [];
var document = makeDocument();
document.querySelectorAll = function (selector) { return selector === 'iframe' ? frames : []; };

// A page iframe at UI/<name>/<name>.html with an empty body.
function page(name) {
  var doc = makeDocument();
  var frame = { src: 'https://game/UI/' + name + '/' + name + '.html', contentDocument: doc, isConnected: true, style: { display: '' } };

  doc.defaultView.frameElement = frame;
  frames.push(frame);

  return doc;
}

// Adds an element under parent; spec is "tag#id.class.class", any part optional.
function el(parent, spec) {
  var tag = (spec.match(/^[a-z]+/i) || ['div'])[0];
  var id = spec.match(/#([\w-]+)/);
  var classes = spec.match(/\.[\w-]+/g) || [];
  var node = (parent.ownerDocument || parent).createElement(tag);

  if (id) node.id = id[1];

  node.className = classes.map(function (c) { return c.slice(1); }).join(' ');

  return (parent.body && !parent.tagName ? parent.body : parent).appendChild(node);
}

function click(node) {
  var event = { target: node, stopPropagation: function () {}, preventDefault: function () {} };

  for (var at = node; at; at = at.parentNode) {
    (at.listeners.click || []).forEach(function (callback) { callback(event); });
  }
}

// The inventory page's bag toolbar with the game's discard and sort buttons, on the given tab.
function inventory(tab) {
  var doc = page('BackpackUI');
  var app = el(doc, 'div#app');
  var bar = el(el(app, 'div.bag-slot'), 'div.toolbar');

  app._vnode = { component: { setupState: { curATab: tab, isPrologue: false } } };
  el(bar, 'button.action-btn.discard-btn');
  el(bar, 'button#sort.action-btn');

  return doc;
}

// The labels of the menu rows open in doc, comma separated.
function rows(doc) {
  return doc.querySelectorAll('.slmm-row').map(function (row) { return row.querySelector('.slmm-label').textContent; }).join(',');
}
