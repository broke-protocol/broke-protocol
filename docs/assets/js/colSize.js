window.$colSize = {
  full: true,
  default: {
    '--docsify-example-panels-left-panel-width': '55%',
    '--docsify-example-panels-right-panel-width': '45%',
    '--docsify-example-panels-document-width': '100%',
    '--docsify-example-panels-padding-inner': '8px 16px'
  },
  toggle() {
    if (this.full) {
      this.setDefault();
      return;
    }
    this.setOneCol();
  },
  setDefault() {
    this.setProps(this.default, true);
    this.full = false;
  },
  setOneCol() {
    this.setProps({
      '--docsify-example-panels-left-panel-width': '100%',
      '--docsify-example-panels-right-panel-width': '100%',
      '--docsify-example-panels-document-width': '100%',
      '--docsify-example-panels-padding-inner': '0'
    }, true);
    this.full = true;
  },
  setProps(items, save) {
    for (var key in items) {
      this.setProp(key, items[key]);
    }
    if (!save) {
      return;
    }
    this.setStoredMode(items);
  },
  setProp(key, val) {
    document.body.style.setProperty(key, val);
  },
  loadStoredMode() {
    current = this.getStoredMode() || this.default;
    if (!current) {
      return;
    }
    this.setProps(current, false);
    if (current === this.default) {
      this.full = false;
    }
  },
  getStoredMode() {
    return JSON.parse(localStorage.getItem('col-mode') || 'null');
  },
  setStoredMode(item) {
    localStorage.setItem('col-mode', JSON.stringify(item));
  }
}