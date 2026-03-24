export class ThemeManager {
    constructor() {
        this.themeKey = 'learnbox-theme';
        this.currentTheme = null;
        this.init();
    }

    init() {
        const savedTheme = localStorage.getItem(this.themeKey);
        
        if (savedTheme && (savedTheme === 'light' || savedTheme === 'dark')) {
            this.setTheme(savedTheme);
        } else {
            this.setThemeBasedOnSystem();
        }
        
        this.listenToSystemTheme();
    }

    setTheme(theme) {
        if (theme !== 'light' && theme !== 'dark') {
            console.error('Invalid theme:', theme);
            return;
        }
        
        this.currentTheme = theme;
        
        if (theme === 'dark') {
            document.documentElement.setAttribute('data-theme', 'dark');
        } else {
            document.documentElement.setAttribute('data-theme', 'light');
        }
        
        localStorage.setItem(this.themeKey, theme);
        
        window.dispatchEvent(new CustomEvent('themeChanged', { detail: { theme } }));
    }

    setThemeBasedOnSystem() {
        const isDarkMode = window.matchMedia('(prefers-color-scheme: dark)').matches;
        this.setTheme(isDarkMode ? 'dark' : 'light');
    }

    toggleTheme() {
        const newTheme = this.currentTheme === 'dark' ? 'light' : 'dark';
        this.setTheme(newTheme);
    }

    listenToSystemTheme() {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
            if (!localStorage.getItem(this.themeKey)) {
                this.setTheme(e.matches ? 'dark' : 'light');
            }
        });
    }

    getCurrentTheme() {
        return this.currentTheme;
    }

    isDarkMode() {
        return this.currentTheme === 'dark';
    }
}

export default ThemeManager;