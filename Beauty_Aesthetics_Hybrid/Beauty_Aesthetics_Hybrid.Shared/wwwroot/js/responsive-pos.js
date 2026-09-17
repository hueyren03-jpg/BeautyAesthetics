(function () {
    "use strict";

    function ensureStylesheet(id, href) {
        if (document.getElementById(id)) {
            return;
        }

        var link = document.createElement("link");
        link.id = id;
        link.rel = "stylesheet";
        link.href = href;
        document.head.appendChild(link);
    }

    function ensureResponsiveStyles() {
        ensureStylesheet(
            "inventory-mobile-fixes-css",
            "_content/Beauty_Aesthetics_Hybrid.Shared/inventory-mobile-fixes.css");

        ensureStylesheet(
            "viewport-containment-fixes-css",
            "_content/Beauty_Aesthetics_Hybrid.Shared/viewport-containment-fixes.css");
    }

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

    ensureResponsiveStyles();
    applyViewportClass();

    var resizeTimer;
    function scheduleRefresh() {
        window.clearTimeout(resizeTimer);
        resizeTimer = window.setTimeout(function () {
            ensureResponsiveStyles();
            applyViewportClass();
        }, 80);
    }

    window.addEventListener("resize", scheduleRefresh, { passive: true });
    window.addEventListener("orientationchange", scheduleRefresh, { passive: true });

    if (window.visualViewport) {
        window.visualViewport.addEventListener("resize", scheduleRefresh, { passive: true });
    }
})();
