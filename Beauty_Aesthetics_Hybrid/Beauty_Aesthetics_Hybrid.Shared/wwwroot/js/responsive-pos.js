(function () {
    "use strict";

    function getViewportWidth() {
        if (window.visualViewport && window.visualViewport.width) {
            return Math.round(window.visualViewport.width);
        }

        return Math.round(window.innerWidth || document.documentElement.clientWidth || 1280);
    }

    function getViewportKind() {
        var width = getViewportWidth();
        if (width <= 768) return "mobile";
        if (width <= 1180) return "tablet";
        return "desktop";
    }

    function applyViewportClass() {
        var root = document.documentElement;
        if (!root) return;

        root.classList.remove("pos-mobile", "pos-tablet", "pos-desktop");
        root.classList.add("pos-" + getViewportKind());
        root.dataset.posViewport = getViewportKind();
    }

    window.posResponsive = {
        getViewportWidth: getViewportWidth,
        getViewportKind: getViewportKind,
        refresh: applyViewportClass
    };

    applyViewportClass();

    var resizeTimer;
    function scheduleRefresh() {
        window.clearTimeout(resizeTimer);
        resizeTimer = window.setTimeout(applyViewportClass, 80);
    }

    window.addEventListener("resize", scheduleRefresh, { passive: true });
    window.addEventListener("orientationchange", scheduleRefresh, { passive: true });

    if (window.visualViewport) {
        window.visualViewport.addEventListener("resize", scheduleRefresh, { passive: true });
    }
})();
