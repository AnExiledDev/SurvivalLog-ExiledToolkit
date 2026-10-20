// Exiled's Toolkit's HUD dock. Runs in Root.html, the document every game page iframe sits in. Mods put their buttons
// in the HUD's tool column (CoreUI1's, and CoreUI0's before the disaster), above the game's phone and bag. Past four
// buttons that column climbs the screen, so the dock lays it out as a grid instead: the last four buttons stay in the
// first column where the column always was, the game's own at the bottom, and the rest fill columns to the right, top
// to bottom, four to a column.
//
// A mod marks its button with data-etk-dock="<order>"; marked buttons come first, lowest order first, so the layout
// does not depend on which mod happened to load first. Unmarked buttons (the game's, and mods that do not mark theirs)
// follow in the order the page has them. With four buttons or fewer the game's own layout is left untouched.
(function () {
  if (window.__exiledtoolkitdock) return;

  var ROWS = 4;
  var POLL_MS = 250;
  var DOCK_CLASS = 'etk-dock';
  var STYLE_ID = 'etk-dock-style';
  var ORDER_ATTR = 'data-etk-dock';
  var HUD_PAGES = /CoreUI[01]\.html/;
  // The grid keeps the column's own gap between rows and columns. A hovered button rises above its neighbours, so its
  // tip, which opens to the right, is not drawn under the next column's buttons.
  var CSS = '.vertical-tools.' + DOCK_CLASS + '{display:grid !important;grid-auto-columns:max-content;align-items:center;justify-items:center}' +
    '.vertical-tools.' + DOCK_CLASS + '>*:hover{z-index:30}';

  // Where each of count buttons goes, in the order they are given: { row, column }, both from 1. The last
  // min(count, ROWS) take the first column, bottom aligned; the rest fill columns 2, 3, ... top to bottom.
  function slots(count) {
    var rows = Math.min(count, ROWS);
    var overflow = count - rows;
    var result = [];

    for (var i = 0; i < count; i++) {
      if (i >= overflow) result.push({ row: i - overflow + 1, column: 1 });
      else result.push({ row: i % ROWS + 1, column: 2 + Math.floor(i / ROWS) });
    }

    return result;
  }

  function orderOf(node) {
    var value = parseFloat(node.getAttribute(ORDER_ATTR));

    return isNaN(value) ? 0 : value;
  }

  // The buttons that show, in dock order: marked ones by their order (ties keep page order), then the rest.
  function dockOrder(column) {
    var marked = [];
    var rest = [];
    var children = column.children;

    for (var i = 0; i < children.length; i++) {
      var child = children[i];
      var view = child.ownerDocument.defaultView;

      if (view && view.getComputedStyle(child).display === 'none') continue;

      if (child.hasAttribute(ORDER_ATTR)) marked.push({ node: child, at: i });
      else rest.push(child);
    }

    marked.sort(function (a, b) { return orderOf(a.node) - orderOf(b.node) || a.at - b.at; });

    return marked.map(function (entry) { return entry.node; }).concat(rest);
  }

  // Writes a style only when it differs, so the game's own watcher of these attributes is not woken for nothing.
  function setStyle(node, name, value) {
    if (node.style[name] !== value) node.style[name] = value;
  }

  function release(column) {
    if (column.classList.contains(DOCK_CLASS)) column.classList.remove(DOCK_CLASS);

    setStyle(column, 'gridTemplateRows', '');

    for (var i = 0; i < column.children.length; i++) {
      setStyle(column.children[i], 'gridRow', '');
      setStyle(column.children[i], 'gridColumn', '');
    }
  }

  function layout(column) {
    var buttons = dockOrder(column);

    if (buttons.length <= ROWS) {
      release(column);

      return;
    }

    if (!column.classList.contains(DOCK_CLASS)) column.classList.add(DOCK_CLASS);

    setStyle(column, 'gridTemplateRows', 'repeat(' + ROWS + ', auto)');

    var places = slots(buttons.length);

    for (var i = 0; i < buttons.length; i++) {
      setStyle(buttons[i], 'gridRow', String(places[i].row));
      setStyle(buttons[i], 'gridColumn', String(places[i].column));
    }
  }

  function addStyle(doc) {
    if (doc.getElementById(STYLE_ID)) return;

    var style = doc.createElement('style');
    style.id = STYLE_ID;
    style.textContent = CSS;
    (doc.head || doc.body).appendChild(style);
  }

  // Lays the column out again the moment a button is added, removed, shown or hidden, so a new button never shows
  // in a tall column first. Changes deep inside a button wake it too, but find nothing to move and write nothing.
  function watch(column) {
    if (column.etkDockWatched || typeof MutationObserver === 'undefined') return;

    column.etkDockWatched = true;
    new MutationObserver(function () { layoutSafely(column); }).observe(column, { childList: true, attributes: true, subtree: true, attributeFilter: ['style', 'class'] });
  }

  function layoutSafely(column) {
    try {
      layout(column);
    } catch (e) {}
  }

  function poll() {
    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      if (!HUD_PAGES.test(String(frames[i].src || ''))) continue;

      try {
        var doc = frames[i].contentDocument;
        var column = doc && doc.querySelector('.vertical-tools');

        if (!column) continue;

        addStyle(doc);
        watch(column);
        layout(column);
      } catch (e) {}
    }
  }

  setInterval(poll, POLL_MS);
  poll();

  window.__exiledtoolkitdock = { slots: slots, layout: layout };
})();
