window.$themeSelector = {
  themes: [
    {
      src: 'assets/style/themes/default.css',
      name: 'Simple',
      id: 'simple'
    },
    {
      src: 'assets/style/themes/dark.css',
      name: 'Simple Dark',
      id: 'simple-dark'
    }
  ],
  selector: 'theme-id',
  switchTheme() {
    currentTheme = this.getStoredTheme() || this.themes[0];
    currentIndex = this.themes.findIndex(x => x.id === currentTheme.id);
    if (++currentIndex > this.themes.length - 1) {
      currentIndex = 0;
    }
    this.setTheme(this.themes[currentIndex]);
  },
  setTheme(theme) {
    let element = document.getElementById(this.selector);
    element.href = theme.src;
    this.setStoredTheme(theme);
  },
  loadStoredTheme() {
    currentTheme = this.getStoredTheme();
    if (!currentTheme) {
      return;
    }
    this.setTheme(currentTheme);
  },
  getStoredTheme() {
    return JSON.parse(localStorage.getItem('theme') || 'null');
  },
  setStoredTheme(theme) {
    localStorage.setItem('theme', JSON.stringify(theme));
  }
}