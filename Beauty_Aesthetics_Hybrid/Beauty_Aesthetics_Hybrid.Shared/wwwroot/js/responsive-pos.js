(function () {
    "use strict";

    function ensureInventoryMobileFixes() {
        if (document.getElementById("inventory-mobile-fixes-css")) {
            return;
        }

        var link = document.createElement("link");
        link.id = "inventory-mobile-fixes-css";
        link.rel = "stylesheet";
        link.href = "_content/Beauty_Aesthetics_Hybrid.Shared/inventory-mobile-fixes.css";
        document.head.appendChild(link);
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

    ensureInventoryMobileFixes();
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
