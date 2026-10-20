// Just enough of the root document and one HUD frame for dock.js to run in Jint: the HUD page's tool column with
// buttons that can be hidden the way the game's v-show hides them, and an interval the tests fire by hand. Test code
// only; nothing here ships.
var window = this;
var intervals = [];

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

// One round of the dock's poll, as the interval would run it.
function tick() {
  intervals.forEach(function (callback) { callback(); });
}

function Element(doc, tag) {
  var classes = [];

  this.ownerDocument = doc;
  this.tagName = tag;
  this.children = [];
  this.style = { display: '', gridRow: '', gridColumn: '', gridTemplateRows: '' };
  this.attributes = {};
  this.classList = {
    contains: function (name) { return classes.indexOf(name) >= 0; },
    add: function (name) { if (classes.indexOf(name) < 0) classes.push(name); },
    remove: function (name) { classes = classes.filter(function (c) { return c !== name; }); }
  };
}

Element.prototype.getAttribute = function (name) { return name in this.attributes ? this.attributes[name] : null; };
Element.prototype.setAttribute = function (name, value) { this.attributes[name] = String(value); };
Element.prototype.hasAttribute = function (name) { return name in this.attributes; };
Element.prototype.appendChild = function (child) { this.children.push(child); return child; };
Element.prototype.insertFirst = function (child) { this.children.unshift(child); return child; };
Element.prototype.removeChild = function (child) { this.children = this.children.filter(function (c) { return c !== child; }); };

function makeDocument() {
  var doc = {};

  doc.defaultView = { getComputedStyle: function (el) { return { display: el.style.display === 'none' ? 'none' : 'block' }; } };
  doc.createElement = function (tag) { return new Element(doc, tag); };
  doc.head = new Element(doc, 'head');
  doc.body = new Element(doc, 'body');
  doc.getElementById = function (id) {
    return doc.head.children.filter(function (el) { return el.id === id; })[0] || null;
  };

  return doc;
}

var hud = makeDocument();
var column = hud.createElement('div');
hud.body.appendChild(column);
hud.querySelector = function (selector) { return selector === '.vertical-tools' ? column : null; };

var frames = [{ src: 'https://game/UI/CoreUI1/CoreUI1.html', contentDocument: hud }];
var document = { getElementsByTagName: function (tag) { return tag === 'iframe' ? frames : []; } };

// A button in the column, named so tests can find it; order marks it the way a mod tags its HUD button.
function button(name, order, isFirst) {
  var el = hud.createElement('div');
  el.name = name;

  if (order !== undefined && order !== null) el.setAttribute('data-etk-dock', order);

  return isFirst ? column.insertFirst(el) : column.appendChild(el);
}

function find(name) {
  return column.children.filter(function (el) { return el.name === name; })[0];
}

// Where a button sits, as "column:row", or "-" when the dock left it to the game's own layout.
function placeOf(name) {
  var el = find(name);

  return el.style.gridColumn === '' ? '-' : el.style.gridColumn + ':' + el.style.gridRow;
}

function isDocked() {
  return column.classList.contains('etk-dock');
}
