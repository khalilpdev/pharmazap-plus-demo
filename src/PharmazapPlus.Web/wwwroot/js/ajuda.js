window.ajudaInterop = {
    initAnchorLinks: function () {
        var container = document.querySelector('.markdown-body');
        if (!container) return;

        // Use event delegation to avoid duplicate listeners on re-renders
        if (container._ajudaListenerAttached) return;
        container._ajudaListenerAttached = true;

        container.addEventListener('click', function (e) {
            var link = e.target.closest('a[href^="#"]');
            if (!link) return;

            var href = link.getAttribute('href');
            if (!href || !href.startsWith('#')) return;

            var id = href.substring(1);
            var target = document.getElementById(id);
            if (target) {
                e.preventDefault();
                e.stopPropagation();
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                try {
                    history.pushState(null, '', href);
                } catch (err) {
                    console.warn('ajudaInterop: Failed to update URL hash.', err);
                }
            }
        });

        // If the page was loaded with a hash, scroll to it
        if (window.location.hash) {
            var id = window.location.hash.substring(1);
            var target = document.getElementById(id);
            if (target) {
                setTimeout(function () {
                    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }, 100);
            }
        }
    }
};
