// Exiled's Toolkit's Mods menu. Runs in Root.html, the document every game page iframe sits in. Carries the shared
// slmod-menu block, so the inventory toolbar has one "Mods" dropdown whichever mods add to it, and puts in it the
// entries mods register through ExiledToolkit.ModsMenu. The plugin sends the whole list with
// window.__exiledtoolkitmenu.set(rows) whenever it changes; the page says which entry was picked through
// vuplex.postMessage("slmod|toolkitmenu|<verb>").
(function () {
  if (window.__exiledtoolkitmenu) return;

  // ===== BEGIN slmod-menu v2. Shared block: keep it byte-identical in every mod that carries it. =====
  // Contract "slmod-menu" (window.__slmodmenu): the inventory toolbar's one "Mods" button and its dropdown, shared by
  // every mod that adds an inventory action, so the mods take one button's room between the game's discard and sort
  // buttons instead of one each. Since v2 the button also sits in the bag panel of the station screens (workbench,
  // stove, ration press, drone hub, greenhouse, brewing barrel, dye vat, rat cage, material rack, shop), listing only
  // the entries marked for stations. Each mod's page.js carries this block and reaches the menu only through
  // slmodMenu, which looks window.__slmodmenu up on every call. The first mod to load creates the menu. A block with a
  // higher VERSION takes it over and keeps every entry and watcher; a lower or equal one only registers. A new version
  // may add methods and entry fields, never drop or change one, and its retire() keeps returning { entries, watchers }
  // in this shape. The game sends the page no keys, so each plugin calls window.__slmodmenu.close() itself on Esc.
  // Carried by sl-exiled-toolkit, sl-storage-hub and sl-unpack-all.
  //
  //   slmodMenu.add(entry)                  adds or replaces the entry with entry.id; false without an id or run
  //   slmodMenu.update(id, changes)         changes some of an entry's fields; false for an unknown id
  //   slmodMenu.remove(id)
  //   slmodMenu.setWanted(id, isWanted)     only wanted entries are listed; with none, the button is taken away
  //   slmodMenu.place(doc)                  makes an inventory page's toolbar match the entries; call it on every
  //                                         poll, since Vue drops foreign nodes when it re-renders the toolbar
  //   slmodMenu.placeAll()                  (v2) place() on every inventory and station page in this document; the
  //                                         menu itself checks the inventory's item tab, so a v2 caller's wanted is
  //                                         its own rule only
  //   slmodMenu.close()                     closes the menu if it is open
  //   slmodMenu.watch(owner, onOpenChange, onProblem)
  //                                         onOpenChange(isOpen) runs each time the menu opens or closes;
  //                                         onProblem(text) (v2, optional) once per page the menu cannot read
  //
  // entry: { id, label, title, icon, hotkey, order, wanted, stations, run }. icon is the d of an svg path on a 24-unit
  // grid, hotkey is shown as given ('' shows none), rows sort by order and then id, an entry is not wanted until it
  // says so, stations (v2) also lists it on the station screens, and run() is called with no arguments once the menu
  // has closed.
  var slmodMenu = (function () {
    var VERSION = 2;
    var SVG_NS = 'http://www.w3.org/2000/svg';
    var GONE_CHECK_MS = 400;
    // Four tiles: room for whatever the mods add.
    var MENU_ICON = 'M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z';
    var INVENTORY_PAGE = /BackpackUI\.html/;
    // The station screens draw their own bag panel. at is where the button goes: in front of the game's sort button,
    // whose look it then copies, or (append) at the end of the bag panel's title row in a look of its own. down opens
    // the menu downwards, for a title row at the top of the panel. The shredder and the drone trade are left out on
    // purpose.
    var STATIONS = [
      { page: /ToolTable\.html/, at: '#bagToolbar > #btn-sort-bag' },
      { page: /Cooking\.html/, at: '.bag-toolbar > #sortBagBtn' },
      { page: /Compressor\.html/, at: '.cart-foot > #btnSortBag' },
      { page: /GreenhouseBuild\.html/, at: '.cart-ft > #btnSort' },
      { page: /DroneHub\.html/, at: '#bagTitleBar > #sortBagBtn1', down: true },
      { page: /BrewPanel\.html/, at: '.col-t > #bagInfo', own: true, down: true },
      { page: /DyePanel\.html/, at: '.col-t > #bagInfo', own: true, down: true },
      { page: /RatCage\.html/, at: '.generator-system > .panel > .panel-title', append: true, down: true },
      { page: /MaterialRack\.html/, at: '.rack-system > .bag-panel > .header', append: true, down: true },
      { page: /ShopUI\.html/, at: '.left-panel > .panel-title, .left-panel > .bag-tabs', append: true, down: true }
    ];
    // The look comes from the game's own category dropdown on the inventory page (category-dropdown-wrapper, -btn,
    // category-dropdown, category-option). These rules only open it upwards there, as the toolbar is the panel's last
    // row, point the caret the way it opens, lay out a row's icon, label and hotkey, and give the button the spacing
    // the game gives its toolbar buttons beside the robot switches. The station pages have no such dropdown, so the
    // slmm-st rules draw one in the same colours, caret included (pointing the way the menu opens, slmm-st-down for
    // one that opens downwards), and slmm-own is the button for a title row with no game button to copy.
    var CSS = [
      '.slmm-wrap{display:flex}',
      '.toolbar.has-rsw .slmm-wrap{margin-left:auto}',
      '.slmm-wrap .slmm-menu{top:auto;bottom:calc(100% + 4px)}',
      '.slmm-btn::after{transform:rotate(180deg)}',
      '.slmm-btn.open::after{transform:none}',
      '.slmm-row{display:flex;align-items:center;gap:8px}',
      '.slmm-row svg{flex:none;width:14px;height:14px;fill:currentColor;opacity:.8}',
      '.slmm-key{margin-left:auto;padding-left:18px;font-size:11px;opacity:.6}',
      '.slmm-st{position:relative;align-items:center;margin-left:auto}',
      '.slmm-st .slmm-btn{display:inline-flex;align-items:center;justify-content:center;gap:6px;white-space:nowrap}',
      '.slmm-st .slmm-btn svg{flex:none;width:14px;height:14px;fill:currentColor;opacity:.8}',
      '.slmm-st .slmm-btn::after{content:"";display:inline-block;width:0;height:0;border-left:5px solid transparent;border-right:5px solid transparent;border-top:6px solid currentColor;margin-left:3px;transition:transform .2s;opacity:.9}',
      '.slmm-st-down .slmm-btn::after{transform:none}',
      '.slmm-st-down .slmm-btn.open::after{transform:rotate(180deg)}',
      '.slmm-st .slmm-menu{position:absolute;right:0;left:auto;z-index:1000;min-width:100%;padding:4px 0;max-height:240px;overflow-y:auto;background:rgba(20,24,36,.96);border:1px solid rgba(100,120,160,.35);border-radius:6px;box-shadow:0 8px 24px rgba(0,0,0,.5)}',
      '.slmm-st .slmm-menu.slmm-down{bottom:auto;top:calc(100% + 4px)}',
      '.slmm-st .slmm-row{padding:7px 14px;font-size:13px;font-weight:normal;letter-spacing:0;text-shadow:none;color:#c8cde0;cursor:pointer;white-space:nowrap}',
      '.slmm-st .slmm-row:hover{background:rgba(80,110,180,.35);color:#fff}',
      '.slmm-own{padding:3px 10px;font-family:inherit;font-size:12px;font-weight:600;letter-spacing:0;text-shadow:none;color:#e8d9b8;background:rgba(20,15,12,.55);border:1px solid rgba(232,176,75,.45);border-radius:6px;cursor:pointer}',
      '.slmm-own:hover{color:#ffc83a;border-color:#ffc83a}',
      '.slmm-st + .r{margin-left:10px}',
      '.slmm-st + .sortb{margin-left:8px}',
      '.bag-toolbar > .slmm-st{margin-right:8px}',
      '.cart-ft > .slmm-st{flex:1 1 0;margin-left:0}',
      '.cart-ft > .slmm-st > .slmm-btn{width:100%}'
    ].join('\n');

    function createHub(snapshot) {
      var entries = {};
      var watchers = {};
      var openMenu = null;
      var isRetired = false;
      var problems = {};

      function add(entry) {
        if (!entry || typeof entry.id !== 'string' || !entry.id || typeof entry.run !== 'function') return false;

        entries[entry.id] = {
          id: entry.id,
          label: String(entry.label || entry.id),
          title: String(entry.title || ''),
          icon: String(entry.icon || ''),
          hotkey: String(entry.hotkey || ''),
          order: typeof entry.order === 'number' ? entry.order : 0,
          wanted: !!entry.wanted,
          stations: !!entry.stations,
          run: entry.run
        };

        return true;
      }

      function update(id, changes) {
        var entry = entries[id];
        var merged = {};

        if (!entry || !changes) return false;

        Object.keys(entry).forEach(function (key) { merged[key] = entry[key]; });
        Object.keys(changes).forEach(function (key) { merged[key] = changes[key]; });
        merged.id = id;

        return add(merged);
      }

      function remove(id) {
        delete entries[id];
      }

      function setWanted(id, isWanted) {
        return update(id, { wanted: !!isWanted });
      }

      function watch(owner, onOpenChange, onProblem) {
        if (typeof owner !== 'string' || typeof onOpenChange !== 'function') return false;

        watchers[owner] = { onOpenChange: onOpenChange, onProblem: typeof onProblem === 'function' ? onProblem : null };

        return true;
      }

      function wantedEntries(station) {
        return Object.keys(entries)
          .map(function (id) { return entries[id]; })
          .filter(function (entry) { return entry.wanted && (!station || entry.stations); })
          .sort(function (a, b) { return a.order - b.order || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0); });
      }

      // The inventory page: its toolbar, on the bag's own item tab only, as the game's own put-all buttons do.
      function place(doc) {
        if (isRetired || !doc) return;

        var bar = doc.querySelector('.bag-slot .toolbar');
        var isShowable = !!bar && isItemTab(doc);

        show(doc, null, bar, isShowable ? firstGameButton(bar) : null, isShowable);
      }

      function placeStation(doc, station) {
        var at = doc.querySelector(station.at);

        if (!at) {
          show(doc, station, null, null, false);

          return;
        }

        if (station.append) show(doc, station, at, null, true);
        else show(doc, station, at.parentNode, at, true);
      }

      // The button goes in bar in front of before (or at the end); a button already there is kept, so a poll never
      // closes an open menu.
      function show(doc, station, bar, before, isShowable) {
        var rows = wantedEntries(station);
        var existing = doc.querySelector('.slmm-wrap');
        var isWanted = isShowable && !!bar && rows.length > 0;
        var isInPlace = !!existing && existing.parentNode === bar && existing.getAttribute('data-slmm-version') === String(VERSION) &&
          (!station || (before ? existing.nextElementSibling === before : true));

        if (isWanted && isInPlace) {
          if (openMenu && openMenu.wrap === existing) showRows(rows);

          return;
        }

        if (openMenu && openMenu.doc === doc) close();
        if (existing) existing.remove();
        if (!isWanted) return;

        addStyle(doc);
        bar.insertBefore(menuButton(doc, station, before), before);
      }

      function placeAll() {
        if (isRetired) return;

        var frames = document.querySelectorAll('iframe');

        for (var i = 0; i < frames.length; i++) {
          var src = frames[i].src || '';
          var station = stationFor(src);

          if (!station && !INVENTORY_PAGE.test(src)) continue;

          var doc = null;

          try {
            doc = frames[i].contentDocument;
          } catch (e) {}

          if (!doc) {
            problem('page ' + src, 'cannot reach the page document of ' + src);
            continue;
          }

          if (station) placeStation(doc, station);
          else place(doc);
        }
      }

      function stationFor(src) {
        for (var i = 0; i < STATIONS.length; i++) {
          if (STATIONS[i].page.test(src)) return STATIONS[i];
        }

        return null;
      }

      // Reads the inventory page's Vue state, which a game update can rename; failing open shows the entries on every
      // tab, so the first failure is reported. The game hides the whole toolbar in its prologue.
      function isItemTab(doc) {
        try {
          var setup = doc.getElementById('app')._vnode.component.setupState;

          return setup.curATab === 0 && !setup.isPrologue;
        } catch (e) {
          problem('tab', 'cannot read the inventory tab state, showing the Mods menu on every tab: ' + e);

          return true;
        }
      }

      function problem(key, text) {
        if (problems[key]) return;

        problems[key] = true;
        Object.keys(watchers).forEach(function (owner) {
          try {
            if (watchers[owner].onProblem) watchers[owner].onProblem(text);
          } catch (e) {}
        });
      }

      // The game's sort button, so the menu sits between it and the discard button.
      function firstGameButton(bar) {
        for (var node = bar.firstElementChild; node; node = node.nextElementSibling) {
          if (node.classList.contains('action-btn') && !node.classList.contains('discard-btn')) return node;
        }

        return null;
      }

      function menuButton(doc, station, before) {
        var wrap = doc.createElement('div');
        var trigger = doc.createElement('button');

        wrap.className = station ? 'slmm-wrap slmm-st' + (station.down ? ' slmm-st-down' : '') : 'category-dropdown-wrapper slmm-wrap';
        wrap.setAttribute('data-slmm-version', String(VERSION));
        trigger.className = (station ? (before && !station.own ? before.className : 'slmm-own') : 'action-btn category-dropdown-btn') + ' unity-interactive slmm-btn';
        trigger.title = 'Mod actions';
        trigger.appendChild(icon(doc, MENU_ICON));
        trigger.appendChild(text(doc, 'btn-txt', 'Mods'));
        trigger.addEventListener('click', function (e) {
          e.stopPropagation();
          toggle(doc, wrap, station);
        });
        wrap.appendChild(trigger);

        return wrap;
      }

      function toggle(doc, wrap, station) {
        if (isRetired) return;

        var wasOpenHere = !!openMenu && openMenu.wrap === wrap;

        close();

        if (!wasOpenHere) open(doc, wrap, station);
      }

      function open(doc, wrap, station) {
        var win = doc.defaultView;
        var rows = wantedEntries(station);

        if (!win || rows.length === 0) return;

        var menu = doc.createElement('div');

        menu.className = station ? 'slmm-menu' + (station.down ? ' slmm-down' : '') : 'category-dropdown slmm-menu';
        menu.addEventListener('click', onMenuClick);
        wrap.appendChild(menu);
        wrap.querySelector('.slmm-btn').classList.add('open');
        openMenu = { doc: doc, win: win, wrap: wrap, menu: menu, station: station, shown: '', timer: setInterval(closeIfGone, GONE_CHECK_MS) };
        showRows(rows);
        win.addEventListener('mousedown', onOutsideDown, true);
        win.addEventListener('keydown', onKeyDown, true);
        notify(true);
      }

      function close() {
        if (!openMenu) return;

        var closing = openMenu;
        var trigger = closing.wrap.querySelector('.slmm-btn');
        openMenu = null;
        clearInterval(closing.timer);

        try {
          closing.win.removeEventListener('mousedown', onOutsideDown, true);
          closing.win.removeEventListener('keydown', onKeyDown, true);
        } catch (e) {}

        closing.menu.remove();

        if (trigger) trigger.classList.remove('open');

        notify(false);
      }

      // Rebuilt only when what it lists changed, so a poll never redraws the row under the pointer.
      function showRows(rows) {
        var shown = JSON.stringify(rows.map(function (entry) { return [entry.id, entry.label, entry.title, entry.icon, entry.hotkey]; }));

        if (shown === openMenu.shown) return;

        openMenu.shown = shown;
        openMenu.menu.textContent = '';
        rows.forEach(function (entry) { openMenu.menu.appendChild(row(openMenu.doc, entry)); });
      }

      function row(doc, entry) {
        var node = doc.createElement('div');
        node.className = 'category-option slmm-row unity-interactive';
        node.setAttribute('data-slmm-id', entry.id);

        if (entry.title) node.title = entry.title;

        node.appendChild(icon(doc, entry.icon));
        node.appendChild(text(doc, 'slmm-label', entry.label));

        if (entry.hotkey) node.appendChild(text(doc, 'slmm-key', entry.hotkey));

        return node;
      }

      // A row without an icon still gets the empty svg, so every label starts at the same place.
      function icon(doc, d) {
        var svg = doc.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');

        if (!d) return svg;

        var path = doc.createElementNS(SVG_NS, 'path');
        path.setAttribute('d', d);
        path.setAttribute('fill-rule', 'evenodd');
        svg.appendChild(path);

        return svg;
      }

      function text(doc, className, value) {
        var span = doc.createElement('span');
        span.className = className;
        span.textContent = value;

        return span;
      }

      // A click on the menu's own padding keeps it open; a row closes it first, so a throwing run() cannot leave it up.
      function onMenuClick(e) {
        var target = e.target && e.target.closest ? e.target.closest('[data-slmm-id]') : null;
        var entry = target ? entries[target.getAttribute('data-slmm-id')] : null;

        e.stopPropagation();

        if (!target) return;

        close();

        if (!entry) return;

        try {
          entry.run();
        } catch (err) {}
      }

      // A press anywhere else closes the menu and still does what it would have, as the game's own dropdown does.
      function onOutsideDown(e) {
        if (openMenu && !openMenu.wrap.contains(e.target)) close();
      }

      // Only reached on a page the game does send keys to; elsewhere the plugins close the menu on Esc.
      function onKeyDown(e) {
        if (!openMenu || e.key !== 'Escape') return;

        e.stopPropagation();
        e.preventDefault();
        close();
      }

      // The game hides or drops a page without telling anyone. A menu left open past it would keep the plugins
      // holding back the game's hotkeys, so it closes with the page.
      function closeIfGone() {
        if (!openMenu) return;

        var frame = null;

        try {
          frame = openMenu.win.frameElement;
        } catch (e) {}

        var isShown = openMenu.wrap.isConnected && !!frame && frame.isConnected && frame.style.display !== 'none';

        if (!isShown) close();
      }

      function notify(isOpen) {
        Object.keys(watchers).forEach(function (owner) {
          try {
            watchers[owner].onOpenChange(isOpen);
          } catch (e) {}
        });
      }

      function retire() {
        close();
        isRetired = true;

        return {
          entries: Object.keys(entries).map(function (id) { return entries[id]; }),
          watchers: Object.keys(watchers).map(function (owner) {
            return { owner: owner, onOpenChange: watchers[owner].onOpenChange, onProblem: watchers[owner].onProblem };
          })
        };
      }

      if (snapshot) {
        (snapshot.entries || []).forEach(function (entry) { add(entry); });
        (snapshot.watchers || []).forEach(function (watcher) { watch(watcher.owner, watcher.onOpenChange, watcher.onProblem); });
      }

      return { version: VERSION, add: add, update: update, remove: remove, setWanted: setWanted, place: place, placeAll: placeAll, close: close, watch: watch, retire: retire };
    }

    // The style outlives a dropped menu button, so a rebuild must not stack another copy of it.
    function addStyle(doc) {
      var id = 'slmm-style-v' + VERSION;

      if (doc.getElementById(id)) return;

      var style = doc.createElement('style');
      style.id = id;
      style.textContent = CSS;
      doc.head.appendChild(style);
    }

    function install() {
      var current = window.__slmodmenu;
      var isHub = !!current && typeof current.version === 'number';
      var snapshot = null;

      if (isHub && current.version >= VERSION) return;

      if (isHub && typeof current.retire === 'function') {
        try {
          snapshot = current.retire();
        } catch (e) {}
      }

      window.__slmodmenu = createHub(snapshot);
    }

    function forward(name) {
      return function () {
        var hub = window.__slmodmenu;

        return hub && typeof hub[name] === 'function' ? hub[name].apply(hub, arguments) : undefined;
      };
    }

    install();

    return { add: forward('add'), update: forward('update'), remove: forward('remove'), setWanted: forward('setWanted'), place: forward('place'), placeAll: forward('placeAll'), close: forward('close'), watch: forward('watch') };
  })();
  // ===== END slmod-menu v2 =====

  var PREFIX = 'slmod|toolkitmenu|';
  var POLL_MS = 400;

  // id -> the row the plugin last sent; wanted is the plugin's rule (say, before the disaster only).
  var rows = {};

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  function runner(id) {
    return function () { send('run|' + id); };
  }

  // The whole list every time, so an entry the plugin dropped leaves the menu here too. The plugin's rule decides
  // each entry, sent as wanted; the plugin sends the list again whenever that changes. These entries stay on the
  // inventory screen, as they do not say stations.
  function set(list) {
    var next = {};

    (list || []).forEach(function (row) {
      if (!row || typeof row.id !== 'string' || !row.id) return;

      next[row.id] = row;
      slmodMenu.add({ id: row.id, label: String(row.label || ''), title: String(row.title || ''), icon: String(row.icon || ''), hotkey: '', order: Number(row.order) || 0, wanted: row.wanted === true, run: runner(row.id) });
    });

    Object.keys(rows).forEach(function (id) {
      if (!next[id]) slmodMenu.remove(id);
    });

    rows = next;
    slmodMenu.placeAll();
  }

  function hasWanted() {
    for (var id in rows) {
      if (rows[id].wanted === true) return true;
    }

    return false;
  }

  // The game sends the page no keys, so the plugin holds the game's hotkeys back while the menu is open and closes
  // it on Esc itself.
  slmodMenu.watch('exiledtoolkit', function (isOpen) { send('open|' + (isOpen ? '1' : '0')); }, function (text) { send('diag|' + text); });

  // Polled because the game creates and drops its pages whenever it likes; the menu itself decides where the button
  // shows. With none of these entries wanted there is nothing of this mod's to show, so the poll waits.
  setInterval(function () {
    try {
      if (hasWanted()) slmodMenu.placeAll();
    } catch (e) {}
  }, POLL_MS);

  window.__exiledtoolkitmenu = { set: set };
  send('ready');
})();
