// Exiled's Toolkit: the toast deck, run in the game's root web document above every page.
// The C# side decides what is on screen (merges, key replacement, the waiting queue) and sends ops through
// window.__exiledtoolkit.apply; this script only draws, times and reports. Text goes in with textContent only.
(function () {
  if (window.__exiledtoolkit) return;

  var PREFIX = 'slmod|toolkit|';
  var LEAVE_MS = 450;
  var MIN_RESUME_MS = 1000;
  // The game may stop sending pointer moves once the pointer is off every clickable area, so a missed mouseleave
  // must not hold a box forever.
  var MAX_HOLD_MS = 15000;
  // As long as the etk-bump animation; a merge during it does not start it again.
  var BUMP_MS = 260;
  var RECT_SETTLE_MS = 350;
  var RECT_POLL_MS = 1000;
  // While boxes are up the HUD is measured this often, so the deck follows it and lets clicks through to panels.
  var WATCH_MS = 250;
  // The deck stays aside this long after the last pointer event on the HUD's buttons: the game may send nothing more
  // once the pointer leaves for the world, so a mouseout that never comes must not keep the deck hidden.
  var TOOLS_IDLE_MS = 4000;
  // The HUD parts whose tips open where the deck sits: the tool column (phone, defense, bag) and the speed buttons.
  var TIP_OWNERS = '.vertical-tools, .speed-controls';
  var POSITIONS = ['hud', 'top-right', 'top-left', 'bottom-right', 'bottom-left'];
  var KINDS = ['info', 'success', 'warning', 'error'];

  // The deck: every card is CARD_WIDTH wide when in front; each older one is INSET narrower on both sides and shows
  // PEEK px past the one before it, down to MAX_PEEKS cards, so the deck keeps about one card's footprint. The C# side
  // shows at most MAX_PEEKS + 1 toasts at once (ToastSettings.MaxMaxVisible); any more are not drawn.
  var CARD_WIDTH = 280;
  var INSET = 6;
  var PEEK = 8;
  var MAX_PEEKS = 3;
  var DIM = [1, 0.78, 0.62, 0.5];
  var Z_FRONT = 2000;
  var Z_DECK = 1000;
  // A corner deck sits this far from the screen edges.
  var EDGE = 16;
  // Beside the HUD: this far right of the tool column (phone, bag) and this far above the action queue.
  var HUD_GAP_X = 10;
  var HUD_GAP_Y = 8;
  // Where the HUD's tool column and queue sit at 1920 x 1080, for a deck shown before the HUD was ever measured.
  var HUD_FALLBACK = { left: 88, bottom: 100 };

  var boxes = {};
  // Ids of boxes the player closed. Ids are never reused, so an entry only ever stops a late merge.
  var closedIds = {};
  var lastRects = null;
  var position = 'hud';
  var hudSpot = null;
  // The deck at rest, relative to the stack's anchor; what is checked against open panels.
  var footprint = null;
  var isHidden = false;
  // Over an open panel the deck stays in sight but lets every click through to the panel.
  var isThrough = false;
  var isToolsHovered = false;
  var toolsSeenAt = 0;
  // The clickable areas each game page last reported, by page id, copied into plain objects of this document.
  var pageRects = {};
  var isRefreshDue = false;
  var settleTimer = 0;

  function send(text) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + text);
    } catch (e) {
      // The C# side keeps its own time and drops a toast it never hears about.
    }
  }

  // Without the ready message the stack is never placed and nothing is reshown, so when the bridge is not there yet
  // it is sent once the bridge arrives.
  function sendReady() {
    if (window.vuplex) send('ready');
    else window.addEventListener('vuplexready', function () { send('ready'); });
  }

  var style = document.createElement('style');
  style.id = 'etk-style';
  style.textContent = [
    '#etk-stack { position: fixed; z-index: 2147483000; width: 0; height: 0; pointer-events: none;',
    '  font-family: "Segoe UI", "Microsoft YaHei", sans-serif; transition: opacity .15s ease; }',
    '#etk-stack.etk-through { opacity: .5; }',
    '#etk-stack.etk-hidden { opacity: 0; }',
    '#etk-stack.etk-hidden .etk-toast, #etk-stack.etk-through .etk-toast { pointer-events: none; }',
    '.etk-toast { --etk-accent: #d9bf8a; --etk-tint: rgba(217,191,138,.2); --etk-edge: rgba(217,191,138,.45);',
    '  position: absolute; box-sizing: border-box; display: flex; align-items: center; gap: 10px;',
    '  padding: 8px 26px 8px 10px; pointer-events: auto; cursor: default;',
    '  background: linear-gradient(180deg, rgba(66,54,42,.96), rgba(42,34,26,.96));',
    '  border: 1px solid rgba(220,188,120,.24); border-left: 3px solid var(--etk-accent); border-radius: 5px;',
    '  box-shadow: 0 6px 18px rgba(0,0,0,.5), inset 0 1px 0 rgba(255,230,180,.07); color: #f0e8d8; text-shadow: 0 1px 2px rgba(0,0,0,.5);',
    '  user-select: none; -webkit-user-select: none; transition: filter .15s ease;',
    '  animation: etk-in .24s cubic-bezier(.22,1,.36,1) both; }',
    '.etk-toast.etk-success { --etk-accent: #8cc56a; --etk-tint: rgba(140,197,106,.2); --etk-edge: rgba(140,197,106,.45); }',
    '.etk-toast.etk-warning { --etk-accent: #FFC83A; --etk-tint: rgba(255,200,58,.2); --etk-edge: rgba(255,200,58,.45); }',
    '.etk-toast.etk-error { --etk-accent: #e0614f; --etk-tint: rgba(224,97,79,.2); --etk-edge: rgba(224,97,79,.45); }',
    '.etk-toast.etk-clickable { cursor: pointer; }',
    '.etk-toast.etk-front { border-color: rgba(255,200,58,.45); border-left-color: var(--etk-accent);',
    '  box-shadow: 0 8px 22px rgba(0,0,0,.6), 0 0 10px rgba(255,200,58,.18), inset 0 1px 0 rgba(255,230,180,.09); }',
    '.etk-toast.etk-out { animation: etk-out .4s ease both; pointer-events: none; }',
    '.etk-toast.etk-bump { animation: etk-bump .26s ease-out both; }',
    '#etk-stack.etk-hud .etk-toast, #etk-stack.etk-bottom-right .etk-toast, #etk-stack.etk-bottom-left .etk-toast { animation-name: etk-in-up; }',
    '#etk-stack.etk-hud .etk-toast.etk-out, #etk-stack.etk-bottom-right .etk-toast.etk-out,',
    '  #etk-stack.etk-bottom-left .etk-toast.etk-out { animation-name: etk-out-down; }',
    '#etk-stack .etk-toast.etk-bump { animation-name: etk-bump; }',
    '@keyframes etk-in { from { opacity: 0; transform: translateY(-10px); } to { opacity: 1; transform: translateY(0); } }',
    '@keyframes etk-out { from { opacity: 1; transform: translateY(0); } to { opacity: 0; transform: translateY(-6px); } }',
    '@keyframes etk-in-up { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }',
    '@keyframes etk-out-down { from { opacity: 1; transform: translateY(0); } to { opacity: 0; transform: translateY(6px); } }',
    '@keyframes etk-bump { 0% { transform: scale(1); } 40% { transform: scale(1.03); } 100% { transform: scale(1); } }',
    '.etk-icon { width: 30px; height: 30px; flex: none; box-sizing: border-box; border-radius: 6px; overflow: hidden;',
    '  display: flex; align-items: center; justify-content: center;',
    '  border: 1px solid var(--etk-edge); background: linear-gradient(135deg, var(--etk-tint), rgba(0,0,0,.12));',
    '  box-shadow: inset 0 1px 2px rgba(0,0,0,.4); }',
    '.etk-icon img { width: 26px; height: 26px; object-fit: contain; filter: drop-shadow(0 1px 2px rgba(0,0,0,.5)); }',
    '.etk-lines { display: flex; flex-direction: column; gap: 1px; min-width: 0; flex: 1; }',
    '.etk-cap { font-size: 9.5px; line-height: 1.2; letter-spacing: 1.4px; text-transform: uppercase; font-weight: 700;',
    '  color: var(--etk-accent); opacity: .9; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }',
    '.etk-title { font-size: 13px; font-weight: 600; line-height: 1.3; color: #fff;',
    '  white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }',
    '.etk-body { font-size: 11.5px; line-height: 1.35; color: #a89a82; white-space: pre-line; overflow-wrap: anywhere;',
    '  display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: 2; overflow: hidden; }',
    '.etk-toast.etk-front .etk-title { white-space: normal; overflow-wrap: anywhere;',
    '  display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: 3; }',
    '.etk-toast.etk-front .etk-body { -webkit-line-clamp: 6; color: #cbbfa8; }',
    '.etk-items { display: none; flex-wrap: wrap; gap: 4px; margin-top: 4px; }',
    '.etk-toast.etk-front .etk-items { display: flex; }',
    '.etk-itemline { font-size: 11.5px; line-height: 1.35; color: #a89a82; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }',
    '.etk-toast.etk-front .etk-itemline { display: none; }',
    '.etk-chip { display: inline-flex; align-items: center; gap: 3px; padding: 1px 6px 1px 2px; border-radius: 4px;',
    '  background: rgba(20,14,8,.45); border: 1px solid rgba(220,188,120,.11); font-size: 10.5px; line-height: 17px; color: #cbbfa8; }',
    '.etk-chip.etk-noimg { padding-left: 6px; }',
    '.etk-chip img { width: 15px; height: 15px; object-fit: contain; }',
    '.etk-chip b { color: #f0e8d8; font-weight: 600; }',
    '.etk-count { position: absolute; top: -7px; left: -7px; min-width: 18px; height: 15px; padding: 0 5px; box-sizing: border-box;',
    '  border-radius: 8px; background: #FFC83A; color: #2a1f10; font-size: 10px; font-weight: 700; text-shadow: none;',
    '  line-height: 15px; text-align: center; box-shadow: 0 1px 4px rgba(0,0,0,.5); pointer-events: none; }',
    '.etk-close { position: absolute; top: 5px; right: 5px; width: 14px; height: 14px; border-radius: 3px; cursor: pointer; opacity: .45; }',
    '.etk-close:hover { opacity: .95; background: rgba(255,200,58,.15); }',
    '.etk-close::before, .etk-close::after { content: ""; position: absolute; left: 3px; top: 6.25px; width: 8px; height: 1.5px;',
    '  background: #cbbfa8; border-radius: 1px; }',
    '.etk-close::before { transform: rotate(45deg); }',
    '.etk-close::after { transform: rotate(-45deg); }',
    '.etk-bar { position: absolute; left: 0; right: 0; bottom: 0; height: 2px; overflow: hidden; border-radius: 0 0 4px 0;',
    '  background: rgba(255,240,210,.06); pointer-events: none; }',
    '.etk-bar i { display: block; height: 100%; width: 100%; background: var(--etk-accent); opacity: .7; }',
    '.etk-toast.etk-behind .etk-count, .etk-toast.etk-behind .etk-close, .etk-toast.etk-behind .etk-bar { visibility: hidden; }'
  ].join('\n');
  (document.head || document.documentElement).appendChild(style);

  var stack = document.createElement('div');
  stack.id = 'etk-stack';
  stack.className = 'etk-hud';
  (document.body || document.documentElement).appendChild(stack);

  function child(parent, className) {
    var el = document.createElement('div');
    el.className = className;
    parent.appendChild(el);
    return el;
  }

  function kindName(kind) {
    return KINDS.indexOf(kind) >= 0 ? kind : 'info';
  }

  function px(value) {
    return Math.round(value) + 'px';
  }

  // The stack's zoom from Interface > UiScale. Its own place and everything laid out inside it are in its zoomed
  // pixels, the screen and the game's pages in plain ones: a length inside is this many screen pixels.
  function zoom() {
    var z = Number(stack.currentCSSZoom);
    return z > 0 ? z : 1;
  }

  // Builds the inside of a box from one toast; used for new boxes and for boxes updated in place.
  function fill(box, toast) {
    box.el.className = 'etk-toast etk-' + kindName(toast.kind) + (toast.click ? ' etk-clickable' : '');
    box.el.textContent = '';
    box.click = !!toast.click;

    if (toast.icon) {
      var icon = child(box.el, 'etk-icon');
      var img = document.createElement('img');
      img.alt = '';
      img.onerror = function () { icon.style.display = 'none'; };
      img.src = toast.icon;
      icon.appendChild(img);
    }

    var lines = child(box.el, 'etk-lines');
    child(lines, 'etk-cap').textContent = toast.source || '';
    child(lines, 'etk-title').textContent = toast.title || '';
    if (toast.body) child(lines, 'etk-body').textContent = toast.body;
    fillItems(lines, toast.items);

    box.countEl = child(box.el, 'etk-count');
    setCount(box, toast.count);

    box.barEl = document.createElement('i');
    child(box.el, 'etk-bar').appendChild(box.barEl);

    var close = child(box.el, 'etk-close');
    close.addEventListener('click', function (event) {
      event.stopPropagation();
      leave(box, true);
    });
  }

  // Item chips: a picture when it loads, the name, and the amount. Text goes in with textContent only. A card that is
  // not in front has no room for chips, so it lists the items on one line instead.
  function fillItems(parent, items) {
    if (!items || !items.length) return;
    child(parent, 'etk-itemline').textContent = items.map(function (item) {
      return (item.label || '') + (item.count > 0 ? ' x' + item.count : '');
    }).join(', ');
    var row = child(parent, 'etk-items');

    items.forEach(function (item) {
      var chip = child(row, 'etk-chip');
      if (item.icon) {
        var img = document.createElement('img');
        img.alt = '';
        img.onerror = function () { img.style.display = 'none'; chip.classList.add('etk-noimg'); };
        img.src = item.icon;
        chip.appendChild(img);
      } else {
        chip.classList.add('etk-noimg');
      }
      var name = document.createElement('span');
      name.textContent = item.label || '';
      chip.appendChild(name);
      if (item.count > 0) {
        var amount = document.createElement('b');
        amount.textContent = ' x' + item.count;
        chip.appendChild(amount);
      }
    });
  }

  // The thin bar along the bottom shows the time left: it runs down over the box's time and stands still while held.
  function runBar(box, ms) {
    var bar = box.barEl;
    if (!bar) return;
    var part = box.total > 0 ? Math.min(1, ms / box.total) : 1;
    bar.style.transition = 'none';
    bar.style.width = (part * 100) + '%';
    void bar.offsetWidth;
    bar.style.transition = 'width ' + ms + 'ms linear';
    bar.style.width = '0%';
  }

  function stopBar(box) {
    var bar = box.barEl;
    if (!bar) return;
    var part = box.total > 0 ? Math.max(0, Math.min(1, box.remaining / box.total)) : 1;
    bar.style.transition = 'none';
    bar.style.width = (part * 100) + '%';
  }

  function setCount(box, count) {
    var many = count >= 2;
    box.count = count >= 1 ? count : 1;
    box.countEl.textContent = many ? 'x' + count : '';
    box.countEl.style.display = many ? '' : 'none';
  }

  // Starts the box's time again. While the pointer rests on it the clock is held, and resumes when it leaves.
  // A resume keeps the box's full time, so the bar picks up where it stood.
  function arm(box, ms, isResume) {
    clearTimeout(box.timer);
    if (!isResume) box.total = ms;
    box.remaining = ms;
    box.deadline = Date.now() + ms;
    if (box.hovered) {
      stopBar(box);
      return;
    }
    clearTimeout(box.holdTimer);
    box.timer = setTimeout(function () { leave(box); }, ms);
    runBar(box, ms);
  }

  function hold(box) {
    if (box.hovered || box.leaving) return;
    deck().forEach(function (other) { if (other !== box) letGo(other); });
    box.hovered = true;
    clearTimeout(box.timer);
    box.remaining = Math.max(0, box.deadline - Date.now());
    stopBar(box);
    box.holdTimer = setTimeout(function () { release(box); }, MAX_HOLD_MS);
    redraw();
  }

  // Lets go of a hovered box without redrawing, for callers that redraw once after letting go of several.
  function letGo(box) {
    if (!box.hovered) return false;
    box.hovered = false;
    clearTimeout(box.holdTimer);
    if (!box.leaving) arm(box, Math.max(box.remaining, MIN_RESUME_MS), true);
    return true;
  }

  function release(box) {
    if (letGo(box)) redraw();
  }

  function bump(box) {
    var now = Date.now();
    if (now < box.bumpUntil) return;
    box.bumpUntil = now + BUMP_MS;
    box.el.classList.remove('etk-bump');
    void box.el.offsetWidth;
    box.el.classList.add('etk-bump');
  }

  function create(toast) {
    var box = { id: toast.id, el: document.createElement('div'), timer: 0, holdTimer: 0, hovered: false, leaving: false, count: 1, bumpUntil: 0, total: 0, barEl: null };
    fill(box, toast);

    box.el.addEventListener('mousedown', function (event) { event.preventDefault(); });
    box.el.addEventListener('mouseenter', function () { hold(box); });
    box.el.addEventListener('mouseleave', function () { release(box); });
    box.el.addEventListener('click', function () {
      if (box.leaving) return;
      if (box.click) send('click|' + box.id);
      leave(box, true);
    });

    box.el.etkBox = box;
    boxes[box.id] = box;
    stack.insertBefore(box.el, stack.firstChild);
    arm(box, toast.ms);
  }

  // A box the player closed (x or a click) stays gone; one whose time ran out comes back for a merge it never drew,
  // so the message carries the count it showed and how it left.
  function leave(box, isClosedByPlayer) {
    if (box.leaving) return;
    box.leaving = true;
    clearTimeout(box.timer);
    clearTimeout(box.holdTimer);
    box.el.classList.remove('etk-bump');
    box.el.classList.add('etk-out');
    if (isClosedByPlayer) closedIds[box.id] = true;
    send('gone|' + box.id + '|' + box.count + '|' + (isClosedByPlayer ? 'closed' : 'timeout'));
    redraw();
    setTimeout(function () {
      if (box.el.parentNode) box.el.parentNode.removeChild(box.el);
      if (boxes[box.id] === box) delete boxes[box.id];
      refresh();
    }, LEAVE_MS);
  }

  function live(id) {
    var box = boxes[id];
    return box && !box.leaving ? box : null;
  }

  function show(toast) {
    var box = live(toast.id);
    if (!box) return create(toast);
    fill(box, toast);
    arm(box, toast.ms);
  }

  // A merge for a box already on its way out, or one this page does not have, draws the toast afresh under the same
  // id: the newest trigger is never lost, and the C# side keeps the toast when the gone count is behind its own. A
  // merge sent before the C# side heard the player closed the box is dropped.
  function merge(op) {
    if (closedIds[op.id]) return;
    var box = live(op.id);
    if (!box) return create(op);
    box.click = !!op.click;
    box.el.classList.toggle('etk-clickable', box.click);
    setCount(box, op.count);
    bump(box);
    arm(box, op.ms);
  }

  function replace(op) {
    var box = live(op.old);
    if (!box) return show(op);
    delete boxes[box.id];
    box.id = op.id;
    boxes[box.id] = box;
    fill(box, op);
    bump(box);
    arm(box, op.ms);
  }

  function place(pos) {
    position = POSITIONS.indexOf(pos) >= 0 ? pos : 'hud';
    stack.className = 'etk-' + position + (isHidden ? ' etk-hidden' : '') + (isThrough ? ' etk-through' : '');
    redraw();
  }

  // ===== Where the deck goes =====

  // The game keeps a page invisible until it says it is ready, and only then sets its visibility inline.
  function isShownFrame(frame) {
    return frame.style.display !== 'none' && frame.style.visibility === 'visible';
  }

  // The page with the tool column: the main HUD, or the prologue's when the main one is not up.
  function hudFrame() {
    var frames = document.getElementsByTagName('iframe');
    var prologue = null;

    for (var i = 0; i < frames.length; i++) {
      var frame = frames[i];
      var src = String(frame.src || '');
      if (!isShownFrame(frame)) continue;
      if (/CoreUI1\.html/.test(src)) return frame;
      if (!prologue && /CoreUI0\.html/.test(src)) prologue = frame;
    }

    return prologue;
  }

  // A part of the HUD that is laid out; one the game hid (plan mode) has an empty box and counts as missing.
  function hudPart(doc, selector) {
    var el = doc.querySelector(selector);
    if (!el) return null;
    var rect = el.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0 ? rect : null;
  }

  // Right of the tool column and above the action queue, in this page's pixels; null while the HUD cannot be read. The
  // prologue's checklist bubble points out of the column to its right, so while it shows the deck goes past it.
  function measureHud() {
    try {
      var frame = hudFrame();
      if (!frame) return null;
      var doc = frame.contentDocument;
      if (!doc || doc.readyState === 'loading') return null;
      watchTools(doc);
      var tools = hudPart(doc, '.vertical-tools');
      if (!tools) return null;
      var bubble = hudPart(doc, '.checklist-bubble');
      var queue = hudPart(doc, '.action-panel');
      var origin = frame.getBoundingClientRect();
      var columnRight = bubble ? Math.max(tools.right, bubble.right) : tools.right;
      var floor = queue ? queue.top - HUD_GAP_Y : tools.bottom;
      return { left: origin.left + columnRight + HUD_GAP_X, bottom: (window.innerHeight || 0) - (origin.top + floor) };
    } catch (e) {
      return null;
    }
  }

  // The deck stays where the HUD last was while the HUD is hidden, so it does not jump about in plan mode.
  function placeStack() {
    var s = stack.style;
    var z = zoom();
    var left = '';
    var right = '';
    var top = '';
    var bottom = '';

    if (position === 'hud') {
      hudSpot = measureHud() || hudSpot;
      var spot = hudSpot || HUD_FALLBACK;
      var width = window.innerWidth || 0;
      var height = window.innerHeight || 0;
      left = px(Math.max(EDGE, Math.min(spot.left, width - CARD_WIDTH * z - EDGE)) / z);
      bottom = px(Math.max(EDGE, Math.min(spot.bottom, height - EDGE)) / z);
    } else {
      var edge = px(EDGE / z);
      if (position.indexOf('left') >= 0) left = edge;
      else right = edge;
      if (position.indexOf('top') === 0) top = edge;
      else bottom = edge;
    }

    if (s.left !== left) s.left = left;
    if (s.right !== right) s.right = right;
    if (s.top !== top) s.top = top;
    if (s.bottom !== bottom) s.bottom = bottom;
  }

  // Hands the deck to the UI size script (ui.js), or leaves it for that script to take when it is not in yet.
  function uiSize(el, options) {
    var ui = window.ExiledUi;
    if (ui) {
      ui.size(el, options);
      return;
    }
    var waiting = window.__exiledUiWait = window.__exiledUiWait || [];
    waiting.push([el, options]);
  }

  // Live boxes, newest first.
  function deck() {
    var list = [];

    for (var i = 0; i < stack.children.length; i++) {
      var box = stack.children[i].etkBox;
      if (box && !box.leaving) list.push(box);
    }

    return list;
  }

  // The cards the deck can show, newest first; one past them would sit unseen behind the last.
  function inDeck() {
    return deck().slice(0, MAX_PEEKS + 1);
  }

  // Lays the cards out as a deck: the newest in front at the anchor, each older one narrower, dimmer and peeking out
  // past the one before it. A hovered card comes to the front at full width with its text in full, in its own place,
  // so the others do not move under the pointer. Only layout boxes move; animations use transform and opacity, so
  // the reported rects stay exact.
  function layout() {
    var isLeftHung = position.indexOf('right') < 0;
    var isBottomHung = position.indexOf('top') !== 0;
    var edge = 0;
    var bounds = null;

    deck().forEach(function (box, depth) {
      box.el.style.visibility = depth <= MAX_PEEKS ? '' : 'hidden';
    });

    inDeck().forEach(function (box, depth) {
      var el = box.el;
      var s = el.style;
      var restInset = depth * INSET;
      var restHeight = measureAtRest(box, depth, restInset);
      box.restHeight = restHeight;
      var inset = box.hovered ? 0 : restInset;

      s.width = px(CARD_WIDTH - 2 * inset);
      s.left = isLeftHung ? px(inset) : '';
      s.right = isLeftHung ? '' : px(inset);
      s.zIndex = String(box.hovered ? Z_FRONT : Z_DECK - depth);
      s.filter = box.hovered || depth === 0 ? '' : 'brightness(' + DIM[depth] + ')';
      el.classList.toggle('etk-front', box.hovered);
      el.classList.toggle('etk-behind', depth > 0 && !box.hovered);

      var near = depth === 0 ? 0 : Math.max(0, edge + PEEK - restHeight);
      edge = near + restHeight;
      s.top = isBottomHung ? '' : px(near);
      s.bottom = isBottomHung ? px(near) : '';

      var x0 = isLeftHung ? restInset : restInset - CARD_WIDTH;
      var y0 = isBottomHung ? -edge : near;
      bounds = grow(bounds, x0, y0, CARD_WIDTH - 2 * restInset, restHeight);
    });

    footprint = bounds;
  }

  // A card's height in its place in the deck, measured every layout, so new text from a merge or a replace counts
  // even while the card is hovered and drawn in full.
  function measureAtRest(box, depth, restInset) {
    var el = box.el;
    el.style.width = px(CARD_WIDTH - 2 * restInset);
    el.classList.toggle('etk-front', false);
    el.classList.toggle('etk-behind', depth > 0);

    return el.offsetHeight;
  }

  function grow(bounds, x, y, w, h) {
    if (!bounds) return { x0: x, y0: y, x1: x + w, y1: y + h };
    return { x0: Math.min(bounds.x0, x), y0: Math.min(bounds.y0, y), x1: Math.max(bounds.x1, x + w), y1: Math.max(bounds.y1, y + h) };
  }

  // ===== Staying out of the way =====

  // The HUD's tips open to the right of its buttons, where the deck sits, so the deck steps aside while the pointer is
  // on them. Watched from the start, so a pointer already there when the first toast comes is known.
  function watchHud() {
    try {
      var frame = hudFrame();
      var doc = frame && frame.contentDocument;
      if (doc) watchTools(doc);
    } catch (e) {
      // The HUD can be between pages; the next heartbeat looks again.
    }
  }

  function watchTools(doc) {
    if (doc.etkToolsWatched) return;
    doc.etkToolsWatched = true;
    doc.addEventListener('mouseover', notePointer, true);
    doc.addEventListener('mousemove', notePointer, true);
    doc.addEventListener('mouseout', function (event) {
      if (!event.relatedTarget) setToolsHovered(false);
    }, true);
  }

  function notePointer(event) {
    var target = event.target;
    setToolsHovered(!!(target && target.closest && target.closest(TIP_OWNERS)));
  }

  function setToolsHovered(isOn) {
    var wasOn = isOnTools();
    isToolsHovered = isOn;
    if (isOn) toolsSeenAt = Date.now();
    if (isOnTools() !== wasOn) refresh();
  }

  function isOnTools() {
    return isToolsHovered && Date.now() - toolsSeenAt < TOOLS_IDLE_MS;
  }

  // Every page tells the root where its clickable parts are; a copy of that tells the deck where open panels are. The
  // copy holds plain numbers only, so it keeps nothing of a closed page alive.
  function watchPages() {
    var original = window.syncInteractiveRects;
    if (typeof original !== 'function' || original.etkWatched) return;

    var watched = function (pageId, rects) {
      try {
        pageRects[String(pageId)] = copyRects(rects);
        if (deck().length > 0) refreshSoon();
      } catch (e) {
        // The page's own rects must reach the game whatever happens here.
      }
      return original.apply(this, arguments);
    };

    watched.etkWatched = true;
    window.syncInteractiveRects = watched;
  }

  function copyRects(rects) {
    var list = [];
    var count = rects && rects.length > 0 ? rects.length : 0;

    for (var i = 0; i < count; i++) {
      var rect = rects[i];
      if (rect) list.push({ x: Number(rect.x) || 0, y: Number(rect.y) || 0, w: Number(rect.w) || 0, h: Number(rect.h) || 0 });
    }

    return list;
  }

  // A page the game closed takes what it reported with it.
  function forgetClosedPages() {
    Object.keys(pageRects).forEach(function (pageId) {
      if (!document.getElementById(pageId)) delete pageRects[pageId];
    });
  }

  // A page that was up before this script reported its buttons unheard. Pages that keep their buttons reported send
  // them again on a resize, so each panel not heard from is sent one, once per page load. A panel that never answers
  // reports no buttons: the game hands it every click on the screen.
  function askUnheardPanels() {
    panelFrames().forEach(function (frame) {
      if (pageRects[frame.id]) return;

      try {
        var view = frame.contentWindow;
        if (!view || view.etkAsked) return;
        view.etkAsked = true;
        view.dispatchEvent(new view.Event('resize'));
      } catch (e) {
        // A page between loads is asked on a later heartbeat.
      }
    });
  }

  function frameZ(frame) {
    return Number(frame.style.zIndex) || 0;
  }

  function isOpenFrame(frame) {
    var opacity = parseFloat(frame.style.opacity);
    return isShownFrame(frame) && frame.style.pointerEvents !== 'none' && !(opacity <= 0.01);
  }

  // Shown pages that take the pointer and are drawn above the HUD; every such page when there is no HUD. The game
  // turns the pointer off for pages that let clicks through to the world, so those are not panels.
  function panelFrames() {
    var frames = document.getElementsByTagName('iframe');
    var hud = hudFrame();
    var hudZ = hud ? frameZ(hud) : -Infinity;
    var list = [];

    for (var i = 0; i < frames.length; i++) {
      var frame = frames[i];
      if (frame !== hud && isOpenFrame(frame) && frameZ(frame) > hudZ) list.push(frame);
    }

    return list;
  }

  // A panel covers the deck when it has a clickable part where the deck would be, or when it reports no clickable
  // parts at all: the game then sends it every click on the screen, as for a full window with a dimmed backdrop.
  function isPanelOver() {
    if (!footprint) return false;
    var width = window.innerWidth || 1;
    var height = window.innerHeight || 1;
    var origin = stack.getBoundingClientRect();
    var z = zoom();
    var deckBox = {
      x: (origin.left + footprint.x0 * z) / width,
      y: (origin.top + footprint.y0 * z) / height,
      w: (footprint.x1 - footprint.x0) * z / width,
      h: (footprint.y1 - footprint.y0) * z / height
    };

    return panelFrames().some(function (frame) {
      var rects = pageRects[frame.id];
      return !rects || rects.some(function (rect) { return overlaps(rect, deckBox); });
    });
  }

  function overlaps(a, b) {
    return a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
  }

  // A hidden deck lets go of the pointer and reports no clickable area, so what is under it takes the clicks.
  function setHidden(isOn) {
    if (isOn === isHidden) return;
    isHidden = isOn;
    stack.classList.toggle('etk-hidden', isOn);
    if (isOn) letGoAll();
  }

  // A deck over a panel is still drawn on top, so its news is seen, but like a hidden one it takes no clicks.
  function setThrough(isOn) {
    if (isOn === isThrough) return;
    isThrough = isOn;
    stack.classList.toggle('etk-through', isOn);
    if (isOn) letGoAll();
  }

  function letGoAll() {
    var wasHeld = false;
    deck().forEach(function (box) { if (letGo(box)) wasHeld = true; });
    if (wasHeld) layout();
  }

  function refresh() {
    setHidden(position === 'hud' && isOnTools());
    setThrough(isPanelOver());
    syncRects();
  }

  // Pages report their clickable parts often, some many times a frame; the deck looks again once, before the next
  // frame is drawn.
  function refreshSoon() {
    if (isRefreshDue) return;

    if (typeof requestAnimationFrame === 'undefined') {
      refresh();
      return;
    }

    isRefreshDue = true;
    requestAnimationFrame(function () {
      isRefreshDue = false;
      refresh();
    });
  }

  function redraw() {
    placeStack();
    layout();
    refresh();
  }

  // Tells the C# side where the cards it can see are, as fractions of the page, so clicks on them reach this page.
  // Measured from layout boxes, which the in, out and bump animations do not move, so an animation sends nothing.
  function syncRects() {
    var width = window.innerWidth || 1;
    var height = window.innerHeight || 1;
    var cards = isHidden || isThrough ? [] : inDeck();
    // With no card to report the page is not measured at all.
    var origin = cards.length > 0 ? stack.getBoundingClientRect() : null;
    var z = cards.length > 0 ? zoom() : 1;
    var parts = [];

    cards.forEach(function (box) {
      var el = box.el;
      if (el.offsetWidth <= 0 || el.offsetHeight <= 0) return;
      parts.push([(origin.left + el.offsetLeft * z) / width, (origin.top + el.offsetTop * z) / height, el.offsetWidth * z / width, el.offsetHeight * z / height]
        .map(function (n) { return n.toFixed(5); }).join(','));
    });

    var text = parts.join(';');
    if (text === lastRects) return;
    lastRects = text;
    send('rects|' + text);
  }

  // While boxes are up they are sent every second even unchanged, so the C# side can tell a live page from a lost one.
  function heartbeat() {
    watchPages();
    watchHud();
    forgetClosedPages();
    askUnheardPanels();
    if (lastRects) lastRects = null;
    syncRects();
  }

  // Follows the HUD and the panels while cards are up. The cards are laid out again only when one changed height on
  // its own (a picture or a font that came in late); everything else that moves them lays them out as it happens.
  function watch() {
    if (deck().length === 0) return;
    placeStack();
    if (hasCardResized()) layout();
    refresh();
  }

  // Read in one go, after the stack is placed, so the page is laid out once for all of them.
  function hasCardResized() {
    return inDeck().some(function (box) { return !box.hovered && box.el.offsetHeight !== box.restHeight; });
  }

  // A burst of ops lays the deck out again once, when the last of them has settled.
  function apply(ops) {
    (ops || []).forEach(function (op) {
      try {
        if (op.op === 'place') place(op.pos);
        else if (op.op === 'show') show(op);
        else if (op.op === 'merge') merge(op);
        else if (op.op === 'replace') replace(op);
        else if (op.op === 'remove' && boxes[op.id]) leave(boxes[op.id]);
      } catch (e) {
        // One bad op must not stop the rest of the frame's changes.
      }
    });
    redraw();
    clearTimeout(settleTimer);
    settleTimer = setTimeout(redraw, RECT_SETTLE_MS);
  }

  watchPages();
  watchHud();
  askUnheardPanels();
  placeStack();
  uiSize(stack, { fit: false, onZoom: redraw });
  window.addEventListener('resize', redraw);
  setInterval(heartbeat, RECT_POLL_MS);
  setInterval(watch, WATCH_MS);

  window.__exiledtoolkit = { apply: apply };
  sendReady();
})();
