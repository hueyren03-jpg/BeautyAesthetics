(() => {
    const wrapperClass = "pos-touch-table-scroll";
    const tableClass = "pos-touch-table";
    let scheduled = false;

    function hasHorizontalScrolling(element) {
        if (!element || element === document.body) {
            return false;
        }

        const style = window.getComputedStyle(element);
        return style.overflowX === "auto" || style.overflowX === "scroll";
    }

    function enhanceTable(table) {
        if (!(table instanceof HTMLTableElement)) {
            return;
        }

        table.classList.add(tableClass);

        const existingWrapper = table.closest(`.${wrapperClass}`);
        if (existingWrapper) {
            return;
        }

        const parent = table.parentElement;
        if (!parent) {
            return;
        }

        // Reuse an existing responsive/scrolling container where the page already has one.
        if (hasHorizontalScrolling(parent) ||
            parent.classList.contains("table-responsive") ||
            /table-(wrap|wrapper|container)/i.test(parent.className) ||
            /table_(wrap|wrapper|container)/i.test(parent.className)) {
            parent.classList.add(wrapperClass);
            return;
        }

        const wrapper = document.createElement("div");
        wrapper.className = wrapperClass;
        wrapper.setAttribute("role", "region");
        wrapper.setAttribute("aria-label", "Scrollable table");
        wrapper.tabIndex = 0;

        parent.insertBefore(wrapper, table);
        wrapper.appendChild(table);
    }

    function enhanceAllTables(root = document) {
        if (root instanceof HTMLTableElement) {
            enhanceTable(root);
        }

        if (root.querySelectorAll) {
            root.querySelectorAll("table").forEach(enhanceTable);
        }
    }

    function scheduleEnhancement() {
        if (scheduled) {
            return;
        }

        scheduled = true;
        window.requestAnimationFrame(() => {
            scheduled = false;
            enhanceAllTables(document);
        });
    }

    function start() {
        enhanceAllTables(document);

        const observer = new MutationObserver(scheduleEnhancement);
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})();
