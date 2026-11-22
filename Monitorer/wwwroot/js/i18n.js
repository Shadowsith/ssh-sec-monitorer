// Texts for the scripts. The layout embeds the "js.*" keys of the current language as JSON (<script id="i18n-texts">),
// the language itself is <html lang>. Placeholders are {0}, {1}, ... like on the server.
const I18n = (() => {
    const el = document.getElementById('i18n-texts');
    const texts = el ? JSON.parse(el.textContent) : {};
    const lang = document.documentElement.lang || 'en';
    return {
        lang,
        /** Translated text for a full key ("js.copied"); unknown keys are returned as they are. */
        t(key, ...args) {
            const text = texts[key] ?? key;
            return text.replace(/\{(\d+)\}/g, (match, i) => (args[i] ?? match));
        },
        /** Number in the UI language (decimal separator etc.). */
        number(value, options) {
            return Number(value).toLocaleString(lang, options);
        }
    };
})();
