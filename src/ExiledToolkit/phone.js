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

  // Lays the dock out again the moment the game shows or hides one of its apps (it adds two of them later in a run).
  function watch(doc, dock) {
    if (dock.etkPhoneWatched || typeof MutationObserver === 'undefined') return;

    dock.etkPhoneWatched = true;
    new MutationObserver(function () { layoutSafely(doc, dock); }).observe(dock, { childList: true });
  }

  function poll() {
    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      if (!PHONE_PAGE.test(String(frames[i].src || ''))) continue;

      try {
        var doc = frames[i].contentDocument;
        var dock = doc && doc.querySelector('.app-dock');

        if (!dock) continue;

        addStyle(doc);
        watch(doc, dock);
        layoutSafely(doc, dock);
      } catch (e) {}
    }
  }

  setInterval(poll, POLL_MS);
  poll();

  window.__exiledtoolkitphone = { layout: layout, markedApps: markedApps };
})();
