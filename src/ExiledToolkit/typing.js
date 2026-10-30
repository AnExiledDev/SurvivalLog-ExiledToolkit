// Exiled's Toolkit's typing watch. Runs in Root.html, the document every game page iframe sits in. It tells the plugin
// while a text box has the keyboard, in any page and any mod's panel, so the plugin can keep the game's hotkeys and the
// camera keys off while the player types. No mod has to do anything for its boxes to count.
//
// Focus events do not leave their frame, so each frame's window is listened to on its own; a poll finds new frames.
// A box can lose the keyboard without telling anyone (its panel removed or hidden), so while one is held the poll also
// checks it still has it.
(function () {
  if (window.__exiledtoolkittyping) return;

  var PREFIX = 'slmod|toolkit|';
  var POLL_MS = 250;
  var WATCHED = '__etkTypingWatched';
  var TEXT_TYPES = { text: 1, search: 1, number: 1, email: 1, password: 1, url: 1, tel: 1, '': 1 };

  var field = null;
  var told = false;

  function isTextField(el) {
    if (!el || el.nodeType !== 1 || el.disabled || el.readOnly) return false;

    var tag = String(el.tagName).toUpperCase();

    if (tag === 'TEXTAREA') return true;

    if (tag === 'INPUT') return TEXT_TYPES[String(el.getAttribute('type') || '').toLowerCase()] === 1;

    return el.isContentEditable === true;
  }

  // Whether the box still holds the keyboard: in a page that is still there, focused in its document or shadow root,
  // and drawn.
  function hasKeyboard(el) {
    try {
      var doc = el.ownerDocument;

      if (!el.isConnected || !doc || !doc.defaultView) return false;

      var root = el.getRootNode ? el.getRootNode() : doc;

      if ((root.activeElement || doc.activeElement) !== el) return false;

      return el.getClientRects().length > 0;
    } catch (e) {
      return false;
    }
  }

  function send(text) {
    try {
      if (!window.vuplex) return false;

      window.vuplex.postMessage(PREFIX + text);

      return true;
    } catch (e) {
      return false;
    }
  }

  // Sent only on a change; one the bridge could not take is sent again on the next poll.
  function tell() {
    var isTyping = field !== null;

    if (isTyping === told) return;

    if (send(isTyping ? 'typing|1' : 'typing|0')) told = isTyping;
  }

  // The element that really has focus: inside a shadow root the event only names the host.
  function targetOf(event) {
    var path = event.composedPath ? event.composedPath() : null;

    return path && path.length > 0 ? path[0] : event.target;
  }

  function onFocusIn(event) {
    var el = targetOf(event);

    if (!isTextField(el)) return;

    field = el;
    tell();
  }

  // Moving from one box to another loses focus a moment before the next one gets it, so the change waits a turn.
  function onFocusOut(event) {
    if (targetOf(event) !== field) return;

    field = null;
    setTimeout(tell, 0);
  }

  function watch(win) {
    try {
      if (!win || win[WATCHED]) return;

      win[WATCHED] = true;
      win.addEventListener('focusin', onFocusIn, true);
      win.addEventListener('focusout', onFocusOut, true);
    } catch (e) {
      // A page from another origin, or one being torn down: the poll tries again.
    }
  }

  // The root's frames and the frames inside them, each once per poll.
  function eachFrame(doc, visit, depth) {
    if (!doc || depth > 4) return;

    var frames = doc.querySelectorAll('iframe');

    for (var i = 0; i < frames.length; i++) {
      try {
        var win = frames[i].contentWindow;

        if (!win) continue;

        visit(win);
        eachFrame(frames[i].contentDocument, visit, depth + 1);
      } catch (e) {}
    }
  }

  // The box that has focus now, looking through frames and shadow roots.
  function focusedField(doc) {
    var el = doc && doc.activeElement;

    for (var depth = 0; el && depth < 8; depth++) {
      if (String(el.tagName).toUpperCase() === 'IFRAME') {
        try {
          el = el.contentDocument && el.contentDocument.activeElement;
        } catch (e) {
          return null;
        }
      } else if (el.shadowRoot && el.shadowRoot.activeElement) {
        el = el.shadowRoot.activeElement;
      } else {
        break;
      }
    }

    return isTextField(el) ? el : null;
  }

  function poll() {
    try {
      eachFrame(document, watch, 0);

      if (field !== null && !hasKeyboard(field)) field = null;

      tell();
    } catch (e) {}
  }

  window.__exiledtoolkittyping = {
    // The plugin's call when the player clicks the world: the browser never hears that click, so the box keeps focus.
    blur: function () {
      var el = field;

      field = null;

      try {
        if (el && el.blur) el.blur();
      } catch (e) {}

      tell();
    },
  };

  watch(window);

  try {
    field = focusedField(document);

    if (field !== null && !hasKeyboard(field)) field = null;
  } catch (e) {}

  poll();
  setInterval(poll, POLL_MS);
})();
