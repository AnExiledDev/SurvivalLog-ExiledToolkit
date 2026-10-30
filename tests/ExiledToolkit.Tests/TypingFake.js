// Just enough of the root document and its frames for typing.js to run in Jint: windows that hear focus events only
// from their own document, as a browser's frames do, elements that take and lose focus, and timers the tests run by
// hand. Removing or hiding an element sends no event, as in Chromium. Test code only; nothing here ships.
var window = this;
var posted = [];
var intervals = [];
var timeouts = [];

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

function setTimeout(callback) {
  timeouts.push(callback);

  return timeouts.length;
}

// One round of every poll, as the intervals would run it.
function tick() {
  intervals.forEach(function (callback) { callback(); });
}

// The timeouts due now, and those they set.
function runTimers() {
  while (timeouts.length > 0) timeouts.shift()();
}

function connectVuplex() {
  window.vuplex = { postMessage: function (text) { posted.push(text); } };
}

function addListeners(target) {
  target.listeners = {};
  target.addEventListener = function (type, callback) {
    (target.listeners[type] = target.listeners[type] || []).push(callback);
  };
}

function fire(win, type, el) {
  var event = { type: type, target: el, composedPath: function () { return [el]; } };

  (win.listeners[type] || []).forEach(function (callback) { callback(event); });
}

function Doc(win) {
  this.defaultView = win;
  this.frames = [];
  this.body = new Element(this, 'body', null);
  this.activeElement = this.body;
}

Doc.prototype.querySelectorAll = function (selector) {
  if (selector !== 'iframe') throw new Error('the fake only finds iframes');

  return this.frames.filter(function (frame) { return frame.isConnected; });
};

function Element(doc, tag, type) {
  this.ownerDocument = doc;
  this.tagName = tag.toUpperCase();
  this.nodeType = 1;
  this.type = type;
  this.disabled = false;
  this.readOnly = false;
  this.isContentEditable = false;
  this.isConnected = true;
  this.isHidden = false;
}

Element.prototype.getAttribute = function (name) {
  return name === 'type' ? this.type : null;
};

Element.prototype.getRootNode = function () {
  return this.ownerDocument;
};

Element.prototype.getClientRects = function () {
  return this.isHidden ? [] : [{}];
};

Element.prototype.blur = function () {
  var doc = this.ownerDocument;

  if (doc.activeElement !== this) return;

  doc.activeElement = doc.body;
  fire(doc.defaultView, 'focusout', this);
};

// Focus leaves whatever had it, in any frame, first.
Element.prototype.focus = function () {
  if (focused && focused !== this) focused.blur();

  focused = this;
  this.ownerDocument.activeElement = this;
  fire(this.ownerDocument.defaultView, 'focusin', this);
};

var focused = null;

addListeners(window);
var document = new Doc(window);

// A game page in a frame of the root, or of another frame.
function openFrame(parentDoc) {
  var win = {};

  addListeners(win);

  var frame = new Element(parentDoc || document, 'iframe', null);

  frame.contentWindow = win;
  frame.contentDocument = win.document = new Doc(win);
  (parentDoc || document).frames.push(frame);

  return frame;
}

// The frame loads another page: a new window and document, listening to nothing yet.
function navigate(frame) {
  var win = {};

  addListeners(win);
  frame.contentWindow = win;
  frame.contentDocument = win.document = new Doc(win);

  return frame.contentDocument;
}

function field(doc, tag, type) {
  return new Element(doc, tag || 'input', type === undefined ? 'text' : type);
}

// The frame is taken out of the page: its document loses its window, and no box in it hears a thing.
function removeFrame(frame) {
  frame.isConnected = false;
  frame.contentDocument.defaultView = null;
}
