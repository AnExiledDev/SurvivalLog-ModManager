// A fake root page for page.js: elements whose querySelector gives one element per selector, no game iframes, and a
// fake Exiled's Toolkit that records the panels handed to it.
var window = this;

function setTimeout() { return 1; }
function clearTimeout() {}
function setInterval() { return 1; }

class FakeEl {
  constructor(tag, selector) {
    this.tagName = String(tag).toUpperCase();
    this.selector = selector || '';
    this.children = [];
    this.parentNode = null;
    this.style = {};
    this.dataset = {};
    this.id = '';
    this.className = '';
    this.innerHTML = '';
    this.textContent = '';
    this.found = {};
  }

  appendChild(child) {
    child.parentNode = this;
    this.children.push(child);

    return child;
  }

  querySelector(selector) {
    if (!this.found[selector]) {
      this.found[selector] = new FakeEl('div', selector);
      this.found[selector].parentNode = this;
    }

    return this.found[selector];
  }

  querySelectorAll() { return []; }

  addEventListener() {}
}

var document = {
  documentElement: new FakeEl('html'),
  head: new FakeEl('head'),
  body: new FakeEl('body'),
  createElement: function (tag) { return new FakeEl(tag); },
  getElementsByTagName: function () { return []; }
};

window.addEventListener = function () {};

var handed = [];
window.ExiledUi = { size: function (el, options) { handed.push({ el: el, options: options }); } };

function screenRoot() {
  return document.body.children.filter(function (el) { return el.id === 'slmm'; })[0];
}
