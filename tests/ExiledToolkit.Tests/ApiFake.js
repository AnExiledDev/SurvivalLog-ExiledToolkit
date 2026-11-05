// What api.js needs on top of PhoneFake.js: svg elements, insertBefore and firstChild, the vuplex bridge, clicks, and
// HUD pages whose tool column the game can rebuild. Test code only; nothing here ships.
var posted = [];

Element.prototype.insertBefore = function (child, ref) {
  if (ref === null || ref === undefined) return this.appendChild(child);

  if (child.parentNode) child.parentNode.removeChild(child);

  var at = this.children.indexOf(ref);

  child.parentNode = this;
  this.children.splice(at < 0 ? this.children.length : at, 0, child);
  report({ type: 'childList', target: this, addedNodes: [child] });

  return child;
};

Object.defineProperty(Element.prototype, 'firstChild', {
  get: function () { return this.children[0] || null; }
});

var makePlainDocument = makeDocument;

makeDocument = function (href) {
  var doc = makePlainDocument(href);

  doc.createElementNS = function (ns, tag) { return new Element(doc, tag); };

  return doc;
};

document.createElementNS = function (ns, tag) { return new Element(document, tag); };

function connectVuplex() {
  window.vuplex = { postMessage: function (message) { posted.push(message); } };
}

// A click on el, as the page would deliver it; true when the listener stopped it reaching the game's handlers.
function click(el) {
  var isStopped = false;
  var event = { target: el, stopPropagation: function () { isStopped = true; } };

  (el.listeners.click || []).forEach(function (callback) { callback(event); });

  return isStopped;
}

var COREUI1 = 'file:///WebUI/UI/CoreUI1/CoreUI1.html';
var COREUI0 = 'file:///WebUI/UI/CoreUI0/CoreUI0.html';

// A page arrives in the frame: a new document with what build puts in it, if anything.
function loadPage(frame, build) {
  var doc = makeDocument(frame.src);

  if (build) build(doc);

  frame.contentDocument = doc;

  return doc;
}

// The HUD's tool column as the game draws it, with the phone and bag buttons.
function buildColumn(doc) {
  var column = doc.createElement('div');

  column.classList.add('vertical-tools');
  ['phone', 'bag'].forEach(function (name) {
    var button = column.appendChild(doc.createElement('button'));

    button.name = name;
  });

  return doc.body.appendChild(column);
}

function columnOf(doc) {
  return doc.documentElement.querySelector('.vertical-tools');
}

// The game drawing its tool column again: the old one goes, a new one with only its own buttons takes its place.
function rebuildColumn(doc) {
  var old = columnOf(doc);

  old.parentNode.removeChild(old);

  return buildColumn(doc);
}

// The names of what is in the column, top first: a mod's button by its first class after the game's, the game's by
// name.
function columnNames(doc) {
  return columnOf(doc).children.map(function (el) { return el.name || modClass(el); }).join(',');
}

function modClass(el) {
  var known = ['th-hud-btn', 'fh-hud-btn', 'pb-hud-btn', 'fg-hud-btn'];

  for (var i = 0; i < known.length; i++) {
    if (el.classList.contains(known[i])) return known[i];
  }

  return '?';
}

function find(doc, cls) {
  return doc.documentElement.querySelector('.' + cls);
}
