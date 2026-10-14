// Exiled's Toolkit: the toast stack, run in the game's root web document above every page.
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
  var POSITIONS = ['top-right', 'top-left', 'bottom-right', 'bottom-left'];
  var KINDS = ['info', 'success', 'warning', 'error'];

  var boxes = {};
  var lastRects = null;

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
    '#etk-stack { position: fixed; z-index: 2147483000; display: flex; flex-direction: column; gap: 10px;',
    '  pointer-events: none; font-family: "Microsoft YaHei", sans-serif; }',
    '#etk-stack.etk-top-right { top: 20px; right: 20px; align-items: flex-end; }',
    '#etk-stack.etk-top-left { top: 20px; left: 20px; align-items: flex-start; }',
    '#etk-stack.etk-bottom-right { bottom: 20px; right: 20px; align-items: flex-end; }',
    '#etk-stack.etk-bottom-left { bottom: 20px; left: 20px; align-items: flex-start; }',
    '.etk-toast { --etk-accent: #7fb3d5; --etk-glow: rgba(127,179,213,.14);',
    '  position: relative; box-sizing: border-box; display: flex; align-items: center; gap: 14px;',
    '  min-width: 280px; max-width: 440px; padding: 12px 36px 12px 14px; pointer-events: auto; cursor: default;',
    '  background: linear-gradient(168deg, rgba(40,40,43,.97), rgba(20,20,22,.985));',
    '  border: 1px solid rgba(255,255,255,.18); border-left: 4px solid var(--etk-accent); border-radius: 12px;',
    '  box-shadow: 0 14px 44px rgba(0,0,0,.6), 0 0 22px var(--etk-glow), 0 0 1px rgba(255,255,255,.06) inset;',
    '  color: #F2F2F2; user-select: none; -webkit-user-select: none;',
    '  animation: etk-in .32s cubic-bezier(.22,1,.36,1) both; }',
    '.etk-toast.etk-success { --etk-accent: #8fcf9a; --etk-glow: rgba(143,207,154,.14); }',
    '.etk-toast.etk-warning { --etk-accent: #EBA308; --etk-glow: rgba(235,163,8,.16); }',
    '.etk-toast.etk-error { --etk-accent: #e0614f; --etk-glow: rgba(224,97,79,.18); }',
    '.etk-toast.etk-clickable { cursor: pointer; }',
    '.etk-toast.etk-clickable:hover { border-top-color: rgba(255,255,255,.3); border-right-color: rgba(255,255,255,.3);',
    '  border-bottom-color: rgba(255,255,255,.3); }',
    '.etk-toast.etk-out { animation: etk-out .4s ease both; pointer-events: none; }',
    '.etk-toast.etk-bump { animation: etk-bump .26s ease-out both; }',
    '#etk-stack.etk-bottom-right .etk-toast, #etk-stack.etk-bottom-left .etk-toast { animation-name: etk-in-up; }',
    '#etk-stack.etk-bottom-right .etk-toast.etk-out, #etk-stack.etk-bottom-left .etk-toast.etk-out { animation-name: etk-out-down; }',
    '#etk-stack .etk-toast.etk-bump { animation-name: etk-bump; }',
    '@keyframes etk-in { from { opacity: 0; transform: translateY(-16px); } to { opacity: 1; transform: translateY(0); } }',
    '@keyframes etk-out { from { opacity: 1; transform: translateY(0); } to { opacity: 0; transform: translateY(-8px); } }',
    '@keyframes etk-in-up { from { opacity: 0; transform: translateY(16px); } to { opacity: 1; transform: translateY(0); } }',
    '@keyframes etk-out-down { from { opacity: 1; transform: translateY(0); } to { opacity: 0; transform: translateY(8px); } }',
    '@keyframes etk-bump { 0% { transform: scale(1); } 40% { transform: scale(1.035); } 100% { transform: scale(1); } }',
    '.etk-icon { width: 48px; height: 48px; flex: none; border-radius: 8px; overflow: hidden;',
    '  border: 1px solid var(--etk-accent); background: linear-gradient(180deg, #131316, #1d1d20); }',
    '.etk-icon img { width: 100%; height: 100%; object-fit: cover; object-position: top center; }',
    '.etk-lines { display: flex; flex-direction: column; gap: 3px; min-width: 0; }',
    '.etk-cap { display: flex; align-items: center; gap: 8px; font-size: 12px; letter-spacing: 2px;',
    '  color: var(--etk-accent); white-space: nowrap; }',
    '.etk-cap::after { content: ""; height: 1px; flex: 1 0 18px; background: linear-gradient(90deg, var(--etk-accent), transparent); opacity: .6; }',
    '.etk-title { font-size: 17px; font-weight: 700; line-height: 1.3; color: #F2F2F2; overflow-wrap: anywhere; }',
    '.etk-body { font-size: 14px; line-height: 1.35; color: rgba(255,255,255,.74); white-space: pre-line; overflow-wrap: anywhere; }',
    '.etk-count { position: absolute; top: -9px; left: -9px; min-width: 24px; height: 22px; padding: 0 6px; box-sizing: border-box;',
    '  border-radius: 11px; background: var(--etk-accent); color: #141416; font-size: 13px; font-weight: 700;',
    '  line-height: 22px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,.5); }',
    '.etk-close { position: absolute; top: 8px; right: 8px; width: 20px; height: 20px; border-radius: 4px; cursor: pointer; opacity: .55; }',
    '.etk-close:hover { opacity: 1; background: rgba(255,255,255,.1); }',
    '.etk-close::before, .etk-close::after { content: ""; position: absolute; left: 4px; top: 9px; width: 12px; height: 2px;',
    '  background: #F2F2F2; border-radius: 1px; }',
    '.etk-close::before { transform: rotate(45deg); }',
    '.etk-close::after { transform: rotate(-45deg); }'
  ].join('\n');
  (document.head || document.documentElement).appendChild(style);

  var stack = document.createElement('div');
  stack.id = 'etk-stack';
  stack.className = 'etk-top-right';
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

    box.countEl = child(box.el, 'etk-count');
    setCount(box, toast.count);

    var close = child(box.el, 'etk-close');
    close.addEventListener('click', function (event) {
      event.stopPropagation();
      leave(box);
    });
  }

  function setCount(box, count) {
    var many = count >= 2;
    box.count = count >= 1 ? count : 1;
    box.countEl.textContent = many ? 'x' + count : '';
    box.countEl.style.display = many ? '' : 'none';
  }

  // Starts the box's time again. While the pointer rests on it the clock is held, and resumes when it leaves.
  function arm(box, ms) {
    clearTimeout(box.timer);
    box.remaining = ms;
    box.deadline = Date.now() + ms;
    if (box.hovered) return;
    clearTimeout(box.holdTimer);
    box.timer = setTimeout(function () { leave(box); }, ms);
  }

  function release(box) {
    if (!box.hovered) return;
    box.hovered = false;
    if (!box.leaving) arm(box, Math.max(box.remaining, MIN_RESUME_MS));
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
    var box = { id: toast.id, el: document.createElement('div'), timer: 0, holdTimer: 0, hovered: false, leaving: false, count: 1, bumpUntil: 0 };
    fill(box, toast);

    box.el.addEventListener('mousedown', function (event) { event.preventDefault(); });
    box.el.addEventListener('mouseenter', function () {
      if (box.hovered || box.leaving) return;
      box.hovered = true;
      clearTimeout(box.timer);
      box.remaining = Math.max(0, box.deadline - Date.now());
      box.holdTimer = setTimeout(function () { release(box); }, MAX_HOLD_MS);
    });
    box.el.addEventListener('mouseleave', function () { release(box); });
    box.el.addEventListener('click', function () {
      if (box.leaving) return;
      if (box.click) send('click|' + box.id);
      leave(box);
    });

    boxes[box.id] = box;
    stack.insertBefore(box.el, stack.firstChild);
    arm(box, toast.ms);
  }

  function leave(box) {
    if (box.leaving) return;
    box.leaving = true;
    clearTimeout(box.timer);
    clearTimeout(box.holdTimer);
    box.el.classList.remove('etk-bump');
    box.el.classList.add('etk-out');
    // With the count it showed: a merge the C# side sent after that brings the toast back.
    send('gone|' + box.id + '|' + box.count);
    syncRects();
    setTimeout(function () {
      if (box.el.parentNode) box.el.parentNode.removeChild(box.el);
      if (boxes[box.id] === box) delete boxes[box.id];
      syncRects();
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
  // id: the newest trigger is never lost, and the C# side keeps the toast when the gone count is behind its own.
  function merge(op) {
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
    stack.className = 'etk-' + (POSITIONS.indexOf(pos) >= 0 ? pos : 'top-right');
  }

  // Tells the C# side where the boxes are, as fractions of the page, so clicks on them reach this page. Measured from
  // layout boxes, which the in, out and bump animations do not move, so an animation sends nothing.
  function syncRects() {
    var width = window.innerWidth || 1;
    var height = window.innerHeight || 1;
    var origin = stack.getBoundingClientRect();
    var parts = [];
    Object.keys(boxes).forEach(function (id) {
      var box = boxes[id];
      if (box.leaving) return;
      var el = box.el;
      if (el.offsetWidth <= 0 || el.offsetHeight <= 0) return;
      parts.push([(origin.left + el.offsetLeft) / width, (origin.top + el.offsetTop) / height, el.offsetWidth / width, el.offsetHeight / height]
        .map(function (n) { return n.toFixed(5); }).join(','));
    });
    var text = parts.join(';');
    if (text === lastRects) return;
    lastRects = text;
    send('rects|' + text);
  }

  // While boxes are up they are sent every second even unchanged, so the C# side can tell a live page from a lost one.
  function heartbeat() {
    if (lastRects) lastRects = null;
    syncRects();
  }

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
    syncRects();
    setTimeout(function () { syncRects(); }, RECT_SETTLE_MS);
  }

  window.addEventListener('resize', function () { syncRects(); });
  setInterval(heartbeat, RECT_POLL_MS);

  window.__exiledtoolkit = { apply: apply };
  sendReady();
})();
