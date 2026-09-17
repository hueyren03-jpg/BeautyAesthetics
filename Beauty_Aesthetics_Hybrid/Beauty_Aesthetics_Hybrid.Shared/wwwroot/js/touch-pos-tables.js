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

    function setReadableTouchWidth(table) {
        const firstHeaderRow = table.tHead?.rows?.[0];
        const firstBodyRow = table.tBodies?.[0]?.rows?.[0];
        const columnCount = firstHeaderRow?.cells?.length || firstBodyRow?.cells?.length || 0;

        if (columnCount < 4) {
            table.style.removeProperty("--pos-touch-table-min-width");
            return;
        }

        const widthPerColumn = columnCount >= 8 ? 128 : 140;
        const minimumWidth = Math.min(1800, Math.max(680, columnCount * widthPerColumn));
        table.style.setProperty("--pos-touch-table-min-width", `${minimumWidth}px`);
    }

    function enhanceTable(table) {
        if (!(table instanceof HTMLTableElement)) {
            return;
        }

        table.classList.add(tableClass);
        setReadableTouchWidth(table);

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
