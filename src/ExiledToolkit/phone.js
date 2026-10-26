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

  // Four apps or fewer can never make rows, so their styles are not read at all.
  function layout(dock, apps) {
    arrange(dock, apps);

    var children = dock.children;
    var shown = 0;

    for (var i = 0; children.length > COLUMNS && i < children.length; i++) {
      if (isShown(children[i])) shown++;
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

  // One look at a phone page: finds its dock if the one it had is gone, and lays it out. Only what this pass moved
  // itself is waiting for the observer by then, so that is dropped and the pass does not wake the watcher again.
  function pass(watcher) {
    watcher.isDue = false;

    try {
      var dock = watcher.dock;

      if (!dock || !dock.isConnected) dock = watcher.dock = watcher.doc.querySelector('.app-dock');

      if (dock) {
        addStyle(watcher.doc);
        layout(dock, markedApps(watcher.doc));
      }
    } catch (e) {}

    try {
      if (watcher.observer && watcher.observer.takeRecords) watcher.observer.takeRecords();
    } catch (e) {}
  }

  // One pass per page per frame however many changes came in, on the root's frames: the root always draws, while a
  // page that is not shown yet may not be given frames of its own.
  function passSoon(watcher) {
    if (watcher.isDue) return;

    if (typeof requestAnimationFrame === 'undefined') {
      pass(watcher);

      return;
    }

    watcher.isDue = true;
    requestAnimationFrame(function () { pass(watcher); });
  }

  function hasMarkedApp(node) {
    return node.hasAttribute(ORDER_ATTR) || !!node.querySelector('[' + ORDER_ATTR + ']');
  }

  function isOrHasDock(node) {
    return node.classList.contains('app-dock') || !!node.querySelector('.app-dock');
  }

  // Whether a batch of changes can move an app: the dock gained or lost a child, a marked app was put somewhere, or
  // the page has no dock yet and one came in. Text, the clock and everything else the page redraws is passed over
  // without reading the page.
  function matters(watcher, records) {
    var dock = watcher.dock;
    var hasDock = !!dock && dock.isConnected;

    for (var i = 0; i < records.length; i++) {
      var record = records[i];

      if (hasDock && record.target === dock) return true;

      var added = record.addedNodes;

      for (var j = 0; j < added.length; j++) {
        var node = added[j];

        if (node.nodeType !== 1) continue;

        if (hasMarkedApp(node) || (!hasDock && isOrHasDock(node))) return true;
      }
    }

    return false;
  }

  // The game builds a new phone page every time the phone opens and shows it two frames after the page starts,
  // long before the next poll. So the page is watched from the moment it is built and laid out at once; after that a
  // change that can move an app has the dock laid out before the next paint. True once the page is watched.
  function watchPhone(frame) {
    try {
      var doc = frame.contentDocument;
      var win = doc && doc.defaultView;

      if (!doc || !win || !PHONE_PAGE.test(String(win.location.href))) return false;

      var watcher = doc.etkPhoneWatcher;

      if (watcher) {
        // Without an observer the poll is all there is, so it lays the page out each time.
        if (!watcher.observer) pass(watcher);

        return true;
      }

      watcher = doc.etkPhoneWatcher = { doc: doc, dock: null, isDue: false, observer: null };

      if (typeof win.MutationObserver !== 'undefined') {
        watcher.observer = new win.MutationObserver(function (records) {
          try {
            if (matters(watcher, records)) passSoon(watcher);
          } catch (e) {}
        });
        watcher.observer.observe(doc, { childList: true, subtree: true });
      }

      pass(watcher);

      return true;
    } catch (e) {
      // A page from another origin, or one being torn down: the poll tries again.
      return true;
    }
  }

  // A new frame first holds an empty document; the phone page replaces it a moment later. Look each frame until it is
  // there, for about two seconds at most. A frame has one such look going at a time; being told about it again only
  // gives that look its two seconds again.
  function chase(frame) {
    if (watchPhone(frame)) return;

    frame.etkChaseLeft = CHASE_FRAMES;

    if (frame.etkIsChased || typeof requestAnimationFrame === 'undefined') return;

    frame.etkIsChased = true;
    requestAnimationFrame(function next() {
      frame.etkChaseLeft--;

      if (watchPhone(frame) || frame.etkChaseLeft <= 0) {
        frame.etkIsChased = false;

        return;
      }

      requestAnimationFrame(next);
    });
  }

  function onFrame(frame) {
    if (!PHONE_PAGE.test(String(frame.src || ''))) return;

    if (!frame.etkPhoneFrame) {
      frame.etkPhoneFrame = true;
      frame.addEventListener('load', function () { chase(frame); });
    }

    chase(frame);
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

  // The poll stays as the fallback for a page the watchers missed. A page already watched is left to its watcher.
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
