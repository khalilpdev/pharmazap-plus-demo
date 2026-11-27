window.layoutInterop = {
    setRootFontScale: function (percent) {
        document.documentElement.style.fontSize = percent + '%';
    },
    setImageSrc: function (elementId, src) {
        var el = document.getElementById(elementId);
        if (el) el.src = src;
    },
    setImageSrcWhenReady: async function (elementId, src, attempts, delayMs) {
        attempts = attempts || 1;
        delayMs = delayMs || 0;

        for (var i = 0; i < attempts; i++) {
            var el = document.getElementById(elementId);
            if (el) {
                el.src = src;
                return true;
            }

            if (delayMs > 0) {
                await new Promise(function (resolve) { setTimeout(resolve, delayMs); });
            } else {
                await new Promise(function (resolve) { requestAnimationFrame(resolve); });
            }
        }

        console.warn("[layoutInterop] element '" + elementId + "' not found to set image src");
        return false;
    },
    copyToClipboard: async function (text) {
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(text);
            return;
        }

        var textarea = document.createElement("textarea");
        textarea.value = text;
        textarea.style.position = "fixed";
        textarea.style.opacity = "0";
        document.body.appendChild(textarea);
        textarea.select();

        var copied = document.execCommand("copy");
        document.body.removeChild(textarea);

        if (!copied) {
            throw new Error("Clipboard copy failed.");
        }
    }
};
