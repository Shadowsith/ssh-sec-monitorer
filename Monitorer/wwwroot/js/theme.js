// Applies the stored light / dark theme before the page renders (prevents flashing).
(function () {
    try {
        var theme = localStorage.getItem('ssm-theme');
        if (!theme) {
            theme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        }
        document.documentElement.setAttribute('data-bs-theme', theme);
    } catch (e) {
        /* storage not available - keep default */
    }
})();
