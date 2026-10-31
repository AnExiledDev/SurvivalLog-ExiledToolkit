// Exiled's Toolkit's buttons in the game's own menus. Runs in Root.html, the document every game page iframe sits
// in. Puts the buttons mods add through ExiledToolkit.GameMenu into the pause (Esc) menu, OutSetting.html, and the
// title screen, MainUI.html, drawn like the game's own, and keeps them there when Vue redraws the menu. Then it shrinks
// the menu's buttons, gaps and text just enough that every button fits on the screen, however many mods add one. The
// plugin sends the whole list with window.__exiledtoolkitgamemenu.set(rows) whenever it changes; the page says which
// button was clicked through vuplex.postMessage("slmod|toolkitgamemenu|run|<id>").
(function () {
  if (window.__exiledtoolkitgamemenu) return;

  var PREFIX = 'slmod|toolkitgamemenu|';
  var POLL_MS = 400;
  // The smallest the menu shrinks to: half size. Past that the buttons scroll instead of getting unreadable.
  var MIN_FIT = 0.5;
  // Steps down from the worked-out size when text rounding still leaves the menu a pixel too tall.
  var FIT_STEP = 0.03;
  var MAX_FIT_STEPS = 10;
  // The title screen plays the game's own click animation before the button acts, as the game's items do.
  var TITLE_CLICK_MS = 300;
  var TITLE_CLICKED_MS = 450;

  // Every size the menu is drawn with, times --slet-fit. At 1 each is the game's own value, so a menu that already
  // fits looks exactly like the game's. slet-measure stops the button list from stretching to the bottom while the
  // fit measures how tall the menu wants to be, and stops the game's size animations, which would otherwise have the
  // fit measure the sizes from before it changed them; slet-scroll is the last resort, when even half size does not fit.
  // slet-sink takes the game's "sit at the bottom" spacing from Restart or Save & Exit, so the mod buttons placed
  // just above those sit at the bottom with them.
  var PAUSE_CSS = [
    ':root{--slet-fit:1}',
    '.left-panel{padding-top:calc(80px * var(--slet-fit));padding-bottom:calc(80px * var(--slet-fit))}',
    '.menu-header{font-size:calc(5rem * var(--slet-fit));margin-bottom:calc(50px * var(--slet-fit))}',
    '.menu-header::after{font-size:calc(1rem * var(--slet-fit));margin-top:calc(10px * var(--slet-fit))}',
    '.menu-list{gap:calc(15px * var(--slet-fit))}',
    '.menu-list .main-btn{height:calc(60px * var(--slet-fit));font-size:calc(1.4rem * var(--slet-fit))}',
    '.wishlist-container{margin:calc(20px * var(--slet-fit)) 0}',
    '.btn-exit{margin-bottom:calc(20px * var(--slet-fit))}',
    '.icon-bar{padding-top:calc(25px * var(--slet-fit))}',
    '.slet-measure *{transition:none!important}',
    '.slet-measure .menu-list{flex-grow:0}',
    '.slet-scroll .menu-list{overflow-y:auto;min-height:0;flex-shrink:1}',
    '.menu-list .slet-sink{margin-top:auto}',
    '.slet-sink ~ .btn-restart, .slet-sink ~ .btn-exit{margin-top:0}',
    '.slet-gm-text{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.main-btn.slet-accent:not(:hover){border-left-color:var(--slet-accent)}'
  ].join('');

  var TITLE_CSS = [
    ':root{--slet-fit:1}',
    '.container{padding-top:calc(80px * var(--slet-fit));padding-bottom:calc(80px * var(--slet-fit))}',
    '.header{margin-bottom:calc(60px * var(--slet-fit))}',
    '.sub-title{font-size:calc(1.2rem * var(--slet-fit))}',
    '.main-title{font-size:calc(6rem * var(--slet-fit))}',
    '.menu{gap:calc(16px * var(--slet-fit))}',
    '.menu-text{font-size:calc(1.85rem * var(--slet-fit))}',
    '.menu-item.primary .menu-text{font-size:calc(2.4rem * var(--slet-fit))}',
    '.bar{height:calc(34px * var(--slet-fit))}',
    '.menu-item.primary .bar{height:calc(44px * var(--slet-fit))}',
    '.menu-item.locked .bar, .menu-item.locked:hover .bar{height:calc(34px * var(--slet-fit))}',
    '.slet-measure *{transition:none!important}',
    '.slet-scroll .menu{overflow-y:auto;min-height:0;flex-shrink:1}',
    '.slet-gm .menu-text{white-space:nowrap}',
    '.menu-item.slet-accent .bar{background:var(--slet-accent)}'
  ].join('');

  // The two menu pages. panel is the box that has to fit the screen, its last child is the bottom of what it draws,
  // and list holds the buttons.
  var PAGES = {
    pause: { page: /OutSetting\.html/, panel: '.left-panel', list: '.left-panel > .menu-list', css: PAUSE_CSS },
    title: { page: /MainUI\.html/, panel: '.container', list: '.container > .menu', css: TITLE_CSS }
  };

  // The rows the plugin last sent, sorted by order then id: { id, label, spot, order, accent, wanted }.
  var rows = [];
  var reported = {};
  var frames = document.getElementsByTagName('iframe');

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  function report(text) {
    if (reported[text]) return;

    reported[text] = true;
    send('diag|' + text);
  }

  function wantedIn(kind) {
    return rows.filter(function (row) {
      return row.wanted === true && (kind === 'title' ? row.spot === 'title' : row.spot === 'top' || row.spot === 'bottom');
    });
  }

  function hasWanted() {
    for (var i = 0; i < rows.length; i++) {
      if (rows[i].wanted === true) return true;
    }

    return false;
  }

  // ===== The buttons =====

  // One node per button per page, kept on the page's document, so a redraw that drops it puts the same one back.
  function nodeFor(doc, kind, row) {
    var nodes = doc.__sletGmNodes || (doc.__sletGmNodes = {});
    var node = nodes[row.id];

    if (!node) {
      node = kind === 'title' ? titleNode(doc, row.id) : pauseNode(doc, row.id);
      nodes[row.id] = node;
    }

    var text = node.__sletText;

    // Written only when it differs: a write is a change the page watcher sees, and it would wake itself forever.
    if (text.textContent !== row.label) text.textContent = row.label;

    var accent = row.accent || '';

    if (node.__sletAccent !== accent) {
      node.__sletAccent = accent;

      if (accent) {
        node.style.setProperty('--slet-accent', accent);
        node.classList.add('slet-accent');
      } else {
        node.style.removeProperty('--slet-accent');
        node.classList.remove('slet-accent');
      }
    }

    return node;
  }

  // Like the game's own pause button. The text sits in a span so a long label ends in "..." instead of wrapping.
  function pauseNode(doc, id) {
    var node = doc.createElement('div');
    var text = doc.createElement('span');

    node.id = 'slet-gm-' + id;
    node.className = 'main-btn slet-gm';
    node.setAttribute('data-slet-gm', id);
    text.className = 'slet-gm-text';
    node.appendChild(text);
    node.__sletText = text;
    node.addEventListener('click', function () { send('run|' + id); });

    return node;
  }

  // Like the game's own title item: a bar and a text. It has no data-i18n, as the game's click handler reads that and
  // would run one of its own actions.
  function titleNode(doc, id) {
    var node = doc.createElement('div');
    var bar = doc.createElement('div');
    var text = doc.createElement('div');

    node.id = 'slet-gm-' + id;
    node.className = 'menu-item slet-gm';
    node.setAttribute('data-slet-gm', id);
    bar.className = 'bar';
    text.className = 'menu-text';
    node.appendChild(bar);
    node.appendChild(text);
    node.__sletText = text;
    node.addEventListener('click', function () {
      node.classList.remove('clicked');
      void node.offsetWidth;
      node.classList.add('clicked');
      setTimeout(function () { send('run|' + id); }, TITLE_CLICK_MS);
      setTimeout(function () { node.classList.remove('clicked'); }, TITLE_CLICKED_MS);
    });

    return node;
  }

  function childWith(list, attribute, value) {
    var children = list.children;

    for (var i = 0; i < children.length; i++) {
      if (children[i].getAttribute(attribute) === value) return children[i];
    }

    return null;
  }

  function childWithClass(list, name) {
    var children = list.children;

    for (var i = 0; i < children.length; i++) {
      if (children[i].classList.contains(name)) return children[i];
    }

    return null;
  }

  // Puts nodes in order right after `after`, moving only what is out of place.
  function placeAfter(list, after, nodes) {
    var prev = after;

    for (var i = 0; i < nodes.length; i++) {
      if (prev.nextElementSibling !== nodes[i]) list.insertBefore(nodes[i], prev.nextElementSibling);

      prev = nodes[i];
    }
  }

  // Puts nodes in order right before `before`, moving only what is out of place.
  function placeBefore(list, before, nodes) {
    var next = before;

    for (var i = nodes.length - 1; i >= 0; i--) {
      if (nodes[i].parentNode !== list || nodes[i].nextElementSibling !== next) list.insertBefore(nodes[i], next);

      next = nodes[i];
    }
  }

  function setClass(node, name, isOn) {
    if (node.classList.contains(name) !== isOn) {
      if (isOn) node.classList.add(name);
      else node.classList.remove(name);
    }
  }

  // Top buttons right after Settings; bottom ones just above Restart (when the game shows it) or Save & Exit, the
  // first of them taking the game's spacing so the group sits at the bottom.
  function placePause(doc, list, wanted) {
    var top = [];
    var bottom = [];

    wanted.forEach(function (row) { (row.spot === 'top' ? top : bottom).push(nodeFor(doc, 'pause', row)); });

    if (top.length) {
      var settings = childWith(list, 'data-i18n', 'settings') || childWith(list, 'data-i18n', 'back');

      if (settings) placeAfter(list, settings, top);
      else report('the pause menu has no Settings button to put buttons after');
    }

    if (bottom.length) {
      var anchor = childWithClass(list, 'btn-restart') || childWithClass(list, 'btn-exit');

      if (anchor) placeBefore(list, anchor, bottom);
      else report('the pause menu has no Save & Exit button to put buttons above');
    }

    bottom.forEach(function (node, i) { setClass(node, 'slet-sink', i === 0); });
    top.forEach(function (node) { setClass(node, 'slet-sink', false); });
  }

  function placeTitle(doc, list, wanted) {
    var exit = null;
    var children = list.children;

    for (var i = 0; i < children.length && !exit; i++) {
      var text = children[i].querySelector('.menu-text');

      if (text && text.getAttribute('data-i18n') === 'exit') exit = children[i];
    }

    if (!exit) {
      if (wanted.length) report('the title screen has no Exit item to put buttons before');

      return;
    }

    placeBefore(list, exit, wanted.map(function (row) { return nodeFor(doc, 'title', row); }));
  }

  // Takes away every button of this page that is no longer wanted, and forgets the ones the plugin dropped.
  function removeUnwanted(doc, wanted) {
    var nodes = doc.__sletGmNodes;

    if (!nodes) return;

    var keep = {};

    wanted.forEach(function (row) { keep[row.id] = true; });

    Object.keys(nodes).forEach(function (id) {
      if (keep[id]) return;

      if (nodes[id].parentNode) nodes[id].remove();

      if (!rows.some(function (row) { return row.id === id; })) delete nodes[id];
    });
  }

  function addStyle(doc, kind) {
    if (doc.getElementById('slet-gm-style')) return;

    var style = doc.createElement('style');

    style.id = 'slet-gm-style';
    style.textContent = PAGES[kind].css;
    (doc.head || doc.body).appendChild(style);
  }

  // The list, kept on the document while it is still the one on the page: the game's menus change often, and a
  // search each time would cost on every change.
  function listOf(doc, kind) {
    var list = doc.__sletGmList;

    if (list && list.isConnected) return list;

    list = doc.querySelector(PAGES[kind].list);
    doc.__sletGmList = list;

    return list;
  }

  function place(doc, kind) {
    var list = listOf(doc, kind);

    if (!list) return;

    var wanted = wantedIn(kind);

    addStyle(doc, kind);

    if (kind === 'title') placeTitle(doc, list, wanted);
    else placePause(doc, list, wanted);

    removeUnwanted(doc, wanted);
    keepFitted(doc, kind, list);
  }

  // ===== The fit =====

  function setFit(doc, value) {
    if (doc.__sletFit === value) return;

    doc.__sletFit = value;
    doc.documentElement.style.setProperty('--slet-fit', String(value));
  }

  // How tall the panel wants to be: down to the bottom of its last child, plus its own bottom padding.
  function natural(panel, win) {
    var last = panel.lastElementChild;

    if (!last) return 0;

    var padding = parseFloat(win.getComputedStyle(panel).paddingBottom) || 0;

    return last.offsetTop + last.offsetHeight + padding;
  }

  // Finds the largest size, up to the game's own, at which the whole menu fits the panel. Every size in the menu is
  // a multiple of --slet-fit, so how tall the menu is grows in a straight line with it: measuring at full size and
  // at the smallest gives the line, and the size that fits is read off it. A few steps down cover text rounding.
  // This reads layout after writing styles on purpose; it runs only when the menu's buttons or the window change.
  function fit(doc, panel) {
    var win = doc.defaultView;
    var available = panel.clientHeight;
    var size = 1;
    var isTooTall = false;

    // A list left scrolling from the last fit is only as tall as the screen, so it would measure as fitting.
    panel.classList.remove('slet-scroll');
    panel.classList.add('slet-measure');
    setFit(doc, 1);

    var full = natural(panel, win);

    if (full > available) {
      setFit(doc, MIN_FIT);

      var smallest = natural(panel, win);

      if (smallest >= available) {
        size = MIN_FIT;
        isTooTall = smallest > available;
      } else {
        size = MIN_FIT + (1 - MIN_FIT) * (available - smallest) / (full - smallest);
        size = Math.max(MIN_FIT, Math.min(1, Math.floor(size * 100) / 100));
        setFit(doc, size);

        for (var step = 0; step < MAX_FIT_STEPS && size > MIN_FIT && natural(panel, win) > available; step++) {
          size = Math.max(MIN_FIT, Math.round((size - FIT_STEP) * 100) / 100);
          setFit(doc, size);
        }
      }
    }

    // The size is laid out before the animations come back, so they have no change left to animate.
    setFit(doc, size);
    natural(panel, win);
    panel.classList.remove('slet-measure');
    setClass(panel, 'slet-scroll', isTooTall);
  }

  // Fits again only when something that changes the menu's height changed: how many buttons the list holds, or the
  // window's size. A page the game is hiding has no size to fit, so it waits until it shows.
  function keepFitted(doc, kind, list) {
    var win = doc.defaultView;

    if (!win) return;

    var signature = list.children.length + '|' + win.innerWidth + 'x' + win.innerHeight;

    if (doc.__sletFitFor === signature) return;

    var panel = doc.querySelector(PAGES[kind].panel);

    if (!panel || !panel.clientHeight) return;

    doc.__sletFitFor = signature;
    fit(doc, panel);
  }

  // ===== The pages =====

  // Which of the two menu pages a frame holds now: 'pause', 'title', or '' for every other page. Kept on the page's
  // document, which is a new one each time the frame loads a page.
  function kindOf(frame) {
    var doc = null;

    try {
      doc = frame.contentDocument;
    } catch (e) {
      return { doc: null, kind: '' };
    }

    if (!doc) return { doc: null, kind: '' };

    if (typeof doc.__sletGmKind !== 'string') {
      var src = String(frame.src || '');

      try {
        if (doc.location && doc.location.href && doc.location.href !== 'about:blank') src = String(doc.location.href);
      } catch (e) {}

      doc.__sletGmKind = PAGES.pause.page.test(src) ? 'pause' : PAGES.title.page.test(src) ? 'title' : '';
    }

    return { doc: doc, kind: doc.__sletGmKind };
  }

  function placeIn(frame) {
    try {
      var found = kindOf(frame);

      if (!found.kind || !found.doc.body) return;

      watchPage(frame, found.doc, found.kind);
      place(found.doc, found.kind);
    } catch (e) {
      report('could not place the menu buttons: ' + (e && e.message ? e.message : e));
    }
  }

  function placeAll() {
    for (var i = 0; i < frames.length; i++) placeIn(frames[i]);
  }

  // Did these records come only from the toolkit's own buttons coming and going?
  function isOwnChange(records) {
    for (var i = 0; i < records.length; i++) {
      var record = records[i];
      var nodes = [record.addedNodes, record.removedNodes];

      for (var n = 0; n < nodes.length; n++) {
        var list = nodes[n] || [];

        for (var j = 0; j < list.length; j++) {
          if (!list[j].classList || !list[j].classList.contains('slet-gm')) return false;
        }
      }
    }

    return true;
  }

  // The buttons go back the moment the game redraws its menu, not on the next poll, so they show with the game's
  // own. This is the one place that writes inside the observer: a write put off to the next frame would show the
  // menu without the buttons for a frame. The page's window is watched for a resize, which can change what fits.
  function watchPage(frame, doc, kind) {
    if (!frame.__sletGmWatched) {
      frame.__sletGmWatched = true;
      frame.addEventListener('load', function () { placeIn(frame); });
    }

    if (doc.__sletGmWatched) return;

    var win = doc.defaultView;

    doc.__sletGmWatched = true;

    if (win && typeof win.MutationObserver !== 'undefined') {
      var observer = new win.MutationObserver(function (records) {
        if (isOwnChange(records) || !hasWanted()) return;

        try {
          place(doc, kind);
        } catch (e) {}

        observer.takeRecords();
      });

      observer.observe(doc.body, { childList: true, subtree: true });
    }

    if (win) {
      win.addEventListener('resize', function () {
        doc.__sletFitFor = '';

        if (hasWanted()) place(doc, kind);
      });
    }
  }

  // The whole list every time, so a button the plugin dropped leaves the menus too.
  function set(list) {
    rows = (list || []).filter(function (row) { return row && typeof row.id === 'string' && row.id; });
    rows.sort(function (a, b) { return (Number(a.order) || 0) - (Number(b.order) || 0) || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0); });
    placeAll();
  }

  // Polled because the game creates and drops its pages whenever it likes. With none of the buttons wanted there is
  // nothing to place, so the poll waits.
  setInterval(function () {
    try {
      if (hasWanted()) placeAll();
    } catch (e) {}
  }, POLL_MS);

  window.__exiledtoolkitgamemenu = { set: set };
  send('ready');
})();
