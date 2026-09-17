(() => {
    const wrapperClass = "pos-touch-table-scroll";
    const tableClass = "pos-touch-table";
    const clickableRowClass = "pos-touch-row-clickable";
    const generatedTabIndexAttribute = "data-pos-touch-tabindex";
    const generatedLabelAttribute = "data-pos-touch-label";

    const interactiveSelector = [
        "a",
        "button",
        "input",
        "select",
        "textarea",
        "label",
        "summary",
        "[role='button']",
        "[role='link']",
        "[contenteditable='true']",
        ".resize-handle"
    ].join(",");

    const destructiveOrAlternateActionPattern = /\b(edit|delete|remove|cancel|void|approve|receive|accept|reject|print|download|export|select|check|expand|collapse|sort|save|submit|create|add|return|back)\b/i;
    const explicitDetailsPattern = /(?:\b(?:view|show|open)\b.{0,40}\bdetails?\b)|(?:\bdetails?\b.{0,40}\b(?:view|show|open)\b)/i;
    const detailWordPattern = /\bdetails?\b/i;
    const viewWordPattern = /\b(?:view|show|open)\b/i;

    let scheduled = false;
    let pointerGesture = null;
    let suppressedRow = null;
    let suppressRowClickUntil = 0;

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

    function isDisabled(element) {
        return element instanceof HTMLButtonElement && element.disabled
            || element instanceof HTMLInputElement && element.disabled
            || element instanceof HTMLSelectElement && element.disabled
            || element.getAttribute("aria-disabled") === "true";
    }

    function describeAction(element) {
        return [
            element.getAttribute("aria-label"),
            element.getAttribute("title"),
            element.getAttribute("data-action"),
            element.getAttribute("data-role"),
            element.getAttribute("data-testid"),
            element.id,
            typeof element.className === "string" ? element.className : "",
            element.textContent,
            element instanceof HTMLAnchorElement ? element.getAttribute("href") : ""
        ]
            .filter(Boolean)
            .join(" ")
            .replace(/\s+/g, " ")
            .trim();
    }

    function scoreDetailTrigger(element) {
        if (!(element instanceof HTMLElement) || isDisabled(element)) {
            return 0;
        }

        const description = describeAction(element);
        const lowerDescription = description.toLowerCase();
        const accessibleDescription = [
            element.getAttribute("aria-label"),
            element.getAttribute("title"),
            element.textContent
        ]
            .filter(Boolean)
            .join(" ")
            .replace(/\s+/g, " ")
            .trim();

        // Never hijack a clearly different row action such as Edit, Delete, Receive or Print.
        if (destructiveOrAlternateActionPattern.test(accessibleDescription) &&
            !explicitDetailsPattern.test(accessibleDescription)) {
            return 0;
        }

        let score = 0;

        if (explicitDetailsPattern.test(accessibleDescription)) {
            score = 140;
        } else if (detailWordPattern.test(accessibleDescription)) {
            score = 120;
        } else if (viewWordPattern.test(accessibleDescription)) {
            score = 90;
        }

        if (/\/(?:view|details?)(?:\/|\?|#|$)/i.test(lowerDescription) ||
            /(?:view|details?)[-_]/i.test(lowerDescription)) {
            score = Math.max(score, 115);
        }

        if (element.querySelector(".bi-eye, .bi-eye-fill, .fa-eye, .fas.fa-eye, .far.fa-eye") ||
            /\b(?:bi|fa)[-_]?eye\b/i.test(lowerDescription)) {
            score = Math.max(score, 80);
        }

        return score;
    }

    function findDetailTrigger(row) {
        const candidates = Array.from(row.querySelectorAll("button, a, [role='button'], [role='link']"))
            .map(element => ({ element, score: scoreDetailTrigger(element) }))
            .filter(candidate => candidate.score > 0)
            .sort((left, right) => right.score - left.score);

        if (candidates.length === 0) {
            return null;
        }

        // If several unrelated actions are only weak 'View' matches, do not guess.
        if (candidates.length > 1 &&
            candidates[0].score < 110 &&
            candidates[0].score === candidates[1].score) {
            return null;
        }

        return candidates[0].element;
    }

    function isDataRow(row) {
        if (!(row instanceof HTMLTableRowElement) || !row.closest("tbody")) {
            return false;
        }

        if (row.classList.contains("services-mobile-detail-row") ||
            row.classList.contains("inventory-mobile-detail-row") ||
            row.classList.contains("pos-touch-row-no-details")) {
            return false;
        }

        if (row.cells.length === 1 && row.cells[0].colSpan > 1) {
            return false;
        }

        return true;
    }

    function getTriggerLabel(trigger) {
        return trigger?.getAttribute("aria-label")
            || trigger?.getAttribute("title")
            || trigger?.textContent?.replace(/\s+/g, " ").trim()
            || "View details";
    }

    function clearGeneratedRowAccessibility(row) {
        row.classList.remove(clickableRowClass);

        if (row.hasAttribute(generatedTabIndexAttribute)) {
            row.removeAttribute("tabindex");
            row.removeAttribute(generatedTabIndexAttribute);
        }

        if (row.hasAttribute(generatedLabelAttribute)) {
            row.removeAttribute("aria-label");
            row.removeAttribute(generatedLabelAttribute);
        }
    }

    function enhanceTableRows(table) {
        Array.from(table.tBodies).forEach(body => {
            Array.from(body.rows).forEach(row => {
                if (!isDataRow(row)) {
                    clearGeneratedRowAccessibility(row);
                    return;
                }

                const detailTrigger = findDetailTrigger(row);
                if (!detailTrigger) {
                    clearGeneratedRowAccessibility(row);
                    return;
                }

                row.classList.add(clickableRowClass);

                if (!row.hasAttribute("tabindex")) {
                    row.tabIndex = 0;
                    row.setAttribute(generatedTabIndexAttribute, "true");
                }

                if (!row.hasAttribute("aria-label")) {
                    row.setAttribute("aria-label", getTriggerLabel(detailTrigger));
                    row.setAttribute(generatedLabelAttribute, "true");
                }
            });
        });
    }

    function enhanceTable(table) {
        if (!(table instanceof HTMLTableElement)) {
            return;
        }

        table.classList.add(tableClass);
        setReadableTouchWidth(table);
        enhanceTableRows(table);

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

    function findClickableRow(target) {
        if (!(target instanceof Element)) {
            return null;
        }

        const row = target.closest(`table.${tableClass} tbody tr.${clickableRowClass}`);
        return row instanceof HTMLTableRowElement ? row : null;
    }

    function isInsideIndependentControl(target, row) {
        if (!(target instanceof Element)) {
            return false;
        }

        const control = target.closest(interactiveSelector);
        return Boolean(control && control !== row && row.contains(control));
    }

    function activateRowDetails(row) {
        const trigger = findDetailTrigger(row);
        if (!trigger || isDisabled(trigger)) {
            scheduleEnhancement();
            return;
        }

        trigger.click();
    }

    function onDocumentClickCapture(event) {
        const row = findClickableRow(event.target);
        if (!row) {
            return;
        }

        if (isInsideIndependentControl(event.target, row)) {
            return;
        }

        if (suppressedRow === row && performance.now() < suppressRowClickUntil) {
            event.preventDefault();
            event.stopPropagation();
            suppressedRow = null;
            return;
        }

        // Replace a blank-cell/row tap with the row's existing View/Details action.
        // Capture phase prevents a page-specific row @onclick from firing twice.
        event.preventDefault();
        event.stopPropagation();
        activateRowDetails(row);
    }

    function onDocumentKeyDownCapture(event) {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }

        const row = findClickableRow(event.target);
        if (!row || event.target !== row) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        activateRowDetails(row);
    }

    function onPointerDownCapture(event) {
        const row = findClickableRow(event.target);
        if (!row || isInsideIndependentControl(event.target, row)) {
            pointerGesture = null;
            return;
        }

        pointerGesture = {
            pointerId: event.pointerId,
            row,
            startX: event.clientX,
            startY: event.clientY,
            moved: false
        };
    }

    function onPointerMoveCapture(event) {
        if (!pointerGesture || pointerGesture.pointerId !== event.pointerId || pointerGesture.moved) {
            return;
        }

        const deltaX = Math.abs(event.clientX - pointerGesture.startX);
        const deltaY = Math.abs(event.clientY - pointerGesture.startY);
        if (deltaX > 10 || deltaY > 10) {
            pointerGesture.moved = true;
        }
    }

    function finishPointerGesture(event) {
        if (!pointerGesture || pointerGesture.pointerId !== event.pointerId) {
            return;
        }

        if (pointerGesture.moved) {
            suppressedRow = pointerGesture.row;
            suppressRowClickUntil = performance.now() + 500;
        }

        pointerGesture = null;
    }

    function cancelPointerGesture() {
        pointerGesture = null;
    }

    function start() {
        enhanceAllTables(document);

        document.addEventListener("click", onDocumentClickCapture, true);
        document.addEventListener("keydown", onDocumentKeyDownCapture, true);
        document.addEventListener("pointerdown", onPointerDownCapture, true);
        document.addEventListener("pointermove", onPointerMoveCapture, true);
        document.addEventListener("pointerup", finishPointerGesture, true);
        document.addEventListener("pointercancel", cancelPointerGesture, true);

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
