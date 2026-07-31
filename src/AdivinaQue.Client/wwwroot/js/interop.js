window.blazorInterop = {
    getItem: function (key) {
        return window.localStorage.getItem(key);
    },
    setItem: function (key, value) {
        window.localStorage.setItem(key, value);
    },
    copyText: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    },
    canShare: function () {
        return typeof navigator.share === "function";
    },
    share: async function (title, text, url) {
        try {
            await navigator.share({ title, text, url });
            return true;
        } catch {
            // User cancelled the share sheet, or the platform rejected it — either
            // way the caller should just fall back to the copy buttons, not show an error.
            return false;
        }
    },
};
