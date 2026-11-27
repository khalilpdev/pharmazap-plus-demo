window.metaOAuthInterop = (() => {
    let dotNetRef = null;
    let allowedOrigin = null;
    let listener = null;
    let pendingPopup = null;
    let activePopup = null;
    let expectedPopupOrigin = null;
    const popupFeatures = "width=700,height=820,resizable=yes,scrollbars=yes";

    function openWindow(url) {
        const popup = window.open(url, "meta-oauth", popupFeatures);
        if (popup) {
            popup.focus();
        }

        return popup;
    }

    function onMessage(event) {
        if (!dotNetRef) {
            return;
        }

        const data = event.data;
        if (!data || data.type !== "meta-oauth-result") {
            return;
        }

        const fromAllowedOrigin = !!allowedOrigin && event.origin === allowedOrigin;
        const hasMatchingWindowReference = !!activePopup && event.source === activePopup;
        const hasMatchingPopupOrigin = !!expectedPopupOrigin && event.origin === expectedPopupOrigin;
        if (!fromAllowedOrigin && !(hasMatchingWindowReference && hasMatchingPopupOrigin)) {
            return;
        }

        dotNetRef.invokeMethodAsync("HandleMetaOAuthMessage", JSON.stringify(data.payload));
    }

    return {
        register: function (reference, origin) {
            dotNetRef = reference;
            allowedOrigin = origin;

            if (listener) {
                window.removeEventListener("message", listener);
            }

            listener = onMessage;
            window.addEventListener("message", listener);
        },
        unregister: function () {
            if (listener) {
                window.removeEventListener("message", listener);
            }

            listener = null;
            dotNetRef = null;
            allowedOrigin = null;
        },
        preparePopup: function () {
            if (pendingPopup && !pendingPopup.closed) {
                pendingPopup.focus();
                return true;
            }

            pendingPopup = openWindow("about:blank");
            return pendingPopup !== null;
        },
        closePendingPopup: function () {
            if (pendingPopup && !pendingPopup.closed) {
                pendingPopup.close();
            }

            if (activePopup && !activePopup.closed) {
                activePopup.close();
            }

            pendingPopup = null;
            activePopup = null;
            expectedPopupOrigin = null;
        },
        showPendingPopupError: function (message) {
            if (!pendingPopup || pendingPopup.closed) {
                return false;
            }

            const safeMessage = escapeHtml(message || "Nao foi possivel iniciar a conexao com a Meta.");
            pendingPopup.document.open();
            pendingPopup.document.write(`<!doctype html>
<html>
<head>
    <meta charset="utf-8" />
    <title>Meta OAuth</title>
    <style>
        body{font-family:Arial,sans-serif;padding:2rem;line-height:1.5;background:#f7f7f8;color:#222;}
        .card{max-width:720px;margin:0 auto;background:#fff;border:1px solid #d9d9dd;border-radius:12px;padding:1.5rem;box-shadow:0 8px 24px rgba(0,0,0,.08);}
        .title{margin:0 0 .75rem 0;}
        .message{white-space:pre-wrap;margin:0;}
    </style>
</head>
<body>
    <div class="card">
        <h3 class="title">Falha na conexao</h3>
        <p class="message">${safeMessage}</p>
        <p>Feche esta janela e tente novamente.</p>
    </div>
</body>
</html>`);
            pendingPopup.document.close();
            pendingPopup.focus();
            return true;
        },
        openPopup: function (url) {
            const popupOrigin = tryGetOrigin(url);
            if (pendingPopup && !pendingPopup.closed) {
                activePopup = pendingPopup;
                expectedPopupOrigin = popupOrigin;
                pendingPopup.location.replace(url);
                pendingPopup.focus();
                pendingPopup = null;
                return true;
            }

            pendingPopup = openWindow(url);
            activePopup = pendingPopup;
            expectedPopupOrigin = popupOrigin;
            return pendingPopup !== null;
        }
    };
})();

function escapeHtml(value) {
    return value
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function tryGetOrigin(url) {
    try {
        return new URL(url).origin;
    } catch {
        // Invalid/relative URL should simply skip origin pinning for this attempt.
        return null;
    }
}
