// Exiled's Toolkit's phone apps. Runs in Root.html, the document every game page iframe sits in. Mods add their apps
// to the phone's home screen (PhoneSMS.html); the game keeps its own apps in the dock at the bottom and leaves an empty
// grid in the middle, where the notifications also sit. This moves every app a mod marks into the dock, after the
// game's apps, and past four apps lays the dock out as rows of four, so the apps never stack over the home screen.
//
// A mod marks its app (the game's .app markup) with data-etk-phone="<order>" and puts it anywhere on the home screen,
// usually in .app-grid; without the Toolkit it simply stays there. Marked apps follow the game's, lowest order first.
// With four apps or fewer the game's own dock layout is left untouched.
(function () {
  if (window.__exiledtoolkitphone) return;

  var COLUMNS = 4;
  var POLL_MS = 250;
  var CHASE_FRAMES = 120;
  var DOCK_CLASS = 'etk-phone-dock';
  var STYLE_ID = 'etk-phone-style';
  var ORDER_ATTR = 'data-etk-phone';
  var PHONE_PAGE = /PhoneSMS\.html/;
  // Rows of four, each app in the middle of its column. The game's dock gap stays between columns; rows get a little
  // more room so the labels do not touch the icons below.
  var CSS = '.app-dock.' + DOCK_CLASS + '{display:grid;grid-template-columns:repeat(' + COLUMNS + ',1fr);justify-items:center;row-gap:14px}';

  function orderOf(node) {
    var value = parseFloat(node.getAttribute(ORDER_ATTR));

    return isNaN(value) ? 0 : value;
  }

  function isShown(node) {
    var view = node.ownerDocument.defaultView;

    return !view || view.getComputedStyle(node).display !== 'none';
  }

  // The marked apps in the order they belong: lowest order first, ties by where they were found.
  function markedApps(doc) {
    var found = doc.querySelectorAll('[' + ORDER_ATTR + ']');
    var list = [];

    for (var i = 0; i < found.length; i++) list.push({ node: found[i], at: i });

    list.sort(function (a, b) { return orderOf(a.node) - orderOf(b.node) || a.at - b.at; });

    return list.map(function (entry) { return entry.node; });
  }

  // Puts the marked apps at the end of the dock in their order. A node is only moved when it is out of place, so the
  // observer below does not wake itself; the game adding one of its own apps after them puts them back behind it.
  function arrange(dock, apps) {
    var children = dock.children;
    var start = children.length - apps.length;
    var isInPlace = start >= 0;

    for (var i = 0; isInPlace && i < apps.length; i++) {
      if (children[start + i] !== apps[i]) isInPlace = false;
    }

    if (isInPlace) return;

    for (var j = 0; j < apps.length; j++) dock.appendChild(apps[j]);
  }

  function layout(dock, apps) {
    arrange(dock, apps);

    var shown = 0;

    for (var i = 0; i < dock.children.length; i++) {
      if (isShown(dock.children[i])) shown++;
    }

    var isGrid = shown > COLUMNS;

    if (dock.classList.contains(DOCK_CLASS) !== isGrid) dock.classList.toggle(DOCK_CLASS, isGrid);
  }

  function addStyle(doc) {
    if (doc.getElementById(STYLE_ID)) return;

    var style = doc.createElement('style');
    style.id = STYLE_ID;
    style.textContent = CSS;
    (doc.head || doc.body).appendChild(style);
  }

  function layoutSafely(doc, dock) {
    try {
      layout(dock, markedApps(doc));
    } catch (e) {}
  }

  // Lays the dock out for one phone page, if it has its dock yet.
  function patch(doc) {
    var dock = doc.querySelector('.app-dock');

    if (!dock) return;

    addStyle(doc);
    layoutSafely(doc, dock);
  }

  // The game builds a new phone page every time the phone opens and shows it two frames after the page starts,
  // long before the next poll. So the page is watched from the moment it is built: the observer runs before the next
  // paint whenever the game or a mod changes the page, and only writes when an app is out of place.
  function watchPhone(frame) {
    try {
      var doc = frame.contentDocument;
      var win = doc && doc.defaultView;

      if (!doc || !win || !PHONE_PAGE.test(String(win.location.href))) return false;

      if (!doc.etkPhoneWatched && typeof win.MutationObserver !== 'undefined') {
        doc.etkPhoneWatched = true;
        new win.MutationObserver(function () {
          try {
            patch(doc);
          } catch (e) {}
        }).observe(doc, { childList: true, subtree: true });
      }

      patch(doc);

      return true;
    } catch (e) {
      // A page from another origin, or one being torn down: the poll tries again.
      return true;
    }
  }

  // A new frame first holds an empty document; the phone page replaces it a moment later. Look each frame until it is
  // there, for about two seconds at most.
  function chase(frame, framesLeft) {
    if (watchPhone(frame) || framesLeft <= 0 || typeof requestAnimationFrame === 'undefined') return;

    requestAnimationFrame(function () { chase(frame, framesLeft - 1); });
  }

  function onFrame(frame) {
    if (!PHONE_PAGE.test(String(frame.src || ''))) return;

    if (!frame.etkPhoneFrame) {
      frame.etkPhoneFrame = true;
      frame.addEventListener('load', function () { chase(frame, CHASE_FRAMES); });
    }

    chase(frame, CHASE_FRAMES);
  }

  // Every game page is an iframe in this document: a new one, or one sent to a new address, is looked at at once.
  function watchFrames() {
    if (typeof MutationObserver === 'undefined') return;

    new MutationObserver(function (records) {
      for (var i = 0; i < records.length; i++) {
        var record = records[i];

        if (record.type === 'attributes') {
          if (record.target.tagName === 'IFRAME') onFrame(record.target);

          continue;
        }

        for (var j = 0; j < record.addedNodes.length; j++) {
          var node = record.addedNodes[j];

          if (node.nodeType !== 1) continue;

          if (node.tagName === 'IFRAME') {
            onFrame(node);
            continue;
          }

          var inner = node.getElementsByTagName('iframe');

          for (var k = 0; k < inner.length; k++) onFrame(inner[k]);
        }
      }
    }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['src'] });
  }

  // The poll stays as the fallback for a page the watchers missed.
  function poll() {
    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      if (PHONE_PAGE.test(String(frames[i].src || ''))) watchPhone(frames[i]);
    }
  }

  watchFrames();
  setInterval(poll, POLL_MS);
  poll();

  window.__exiledtoolkitphone = { layout: layout, markedApps: markedApps };
})();
