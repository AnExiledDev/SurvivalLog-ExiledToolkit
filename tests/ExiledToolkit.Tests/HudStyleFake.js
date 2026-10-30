// What dock.js's button styles need on top of PhoneFake.js: svg elements, a search by class, attributes that can be
// taken off, and HUD buttons shaped like the game's and the mods' (a ui-btn holding an svg with one path). Test code
// only; nothing here ships.
Element.prototype.getElementsByClassName = function (name) {
  return this.all().filter(function (el) { return el.classList.contains(name); });
};

Element.prototype.removeAttribute = function (name) {
  delete this.attributes[name];
  this.changed(name);
};

function addHudHelpers(doc, column) {
  doc.createElementNS = function (ns, tag) {
    var el = new Element(doc, tag);

    el.namespaceURI = ns;

    return el;
  };

  // The game's own svg in a button, as Vue draws it: svg.icon-svg holding one path.
  function gameIcon(path) {
    var svg = doc.createElementNS('http://www.w3.org/2000/svg', 'svg');
    var line = doc.createElementNS('http://www.w3.org/2000/svg', 'path');

    svg.setAttribute('class', 'icon-svg');
    svg.classList.add('icon-svg');
    line.setAttribute('d', path);
    svg.appendChild(line);

    return svg;
  }

  // A HUD button with the given extra classes and icon path, added to parent (the column unless given).
  function hudButton(classes, path, parent) {
    var el = doc.createElement('div');

    el.classList.add('ui-btn');
    el.classList.add('icon-sq');
    classes.forEach(function (name) { el.classList.add(name); });
    el.appendChild(gameIcon(path || 'M1 1h2v2H1z'));

    return (parent || column).appendChild(el);
  }

  // The dock's own icon in a button, or null.
  function iconOf(button) {
    return button.children.filter(function (el) { return el.getAttribute('class') === 'etk-ic'; })[0] || null;
  }

  // Vue drawing a button's content again: everything in it goes, and a new svg of the game's comes in.
  function redraw(button, path) {
    button.children.slice().forEach(function (child) { button.removeChild(child); });
    button.appendChild(gameIcon(path || 'M1 1h2v2H1z'));
  }

  return { hudButton: hudButton, iconOf: iconOf, redraw: redraw };
}
