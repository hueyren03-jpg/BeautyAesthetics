(function () {
    "use strict";

    let activeSelect = null;
    let popup = null;
    let searchInput = null;
    let optionList = null;

    function closePopup() {
        popup?.remove();
        popup = null;
        searchInput = null;
        optionList = null;
        activeSelect?.classList.remove("app-searchable-select--open");
        activeSelect = null;
    }

    function positionPopup() {
        if (!popup || !activeSelect || !document.body.contains(activeSelect)) {
            closePopup();
            return;
        }

        const rect = activeSelect.getBoundingClientRect();
        const margin = 8;
        const width = Math.max(240, Math.min(rect.width, window.innerWidth - margin * 2));
        popup.style.width = `${width}px`;
        popup.style.left = `${Math.max(margin, Math.min(rect.left, window.innerWidth - width - margin))}px`;
        const availableBelow = window.innerHeight - rect.bottom - margin;
        const openAbove = availableBelow < 260 && rect.top > availableBelow;
        if (openAbove) {
            popup.style.top = "auto";
            popup.style.bottom = `${window.innerHeight - rect.top + 4}px`;
        } else {
            popup.style.bottom = "auto";
            popup.style.top = `${rect.bottom + 4}px`;
        }
    }

    function optionText(option) {
        return (option.textContent || option.label || option.value || "").trim();
    }

    function renderOptions(query) {
        if (!activeSelect || !optionList) return;
        const normalized = (query || "").trim().toLocaleLowerCase();
        const options = Array.from(activeSelect.options).filter(option => {
            if (option.hidden) return false;
            return !normalized || `${optionText(option)} ${option.value}`.toLocaleLowerCase().includes(normalized);
        });

        optionList.replaceChildren();
        if (!options.length) {
            const empty = document.createElement("div");
            empty.className = "app-searchable-select__empty";
            empty.textContent = "No matching options";
            optionList.appendChild(empty);
            return;
        }

        options.forEach(option => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "app-searchable-select__option";
            button.setAttribute("role", "option");
            button.setAttribute("aria-selected", option.selected ? "true" : "false");
            button.disabled = option.disabled;
            const label = document.createElement("span");
            label.textContent = optionText(option);
            button.appendChild(label);
            if (option.selected) {
                const check = document.createElement("i");
                check.className = "bi bi-check2";
                check.setAttribute("aria-hidden", "true");
                button.appendChild(check);
            }

            button.addEventListener("click", () => {
                if (!activeSelect) return;
                if (activeSelect.multiple) option.selected = !option.selected;
                else activeSelect.value = option.value;
                activeSelect.dispatchEvent(new Event("input", { bubbles: true }));
                activeSelect.dispatchEvent(new Event("change", { bubbles: true }));
                if (activeSelect.multiple) renderOptions(searchInput?.value || "");
                else closePopup();
            });
            optionList.appendChild(button);
        });
    }

    function openPopup(select, initialQuery) {
        if (select.disabled || select.dataset.nativeSelect === "true" || Number(select.size) > 1) return;
        closePopup();
        activeSelect = select;
        select.classList.add("app-searchable-select--open");
        popup = document.createElement("div");
        popup.className = "app-searchable-select__popup";
        popup.setAttribute("role", "dialog");
        popup.setAttribute("aria-label", "Search selection options");

        const search = document.createElement("div");
        search.className = "app-searchable-select__search";
        const icon = document.createElement("i");
        icon.className = "bi bi-search";
        icon.setAttribute("aria-hidden", "true");
        searchInput = document.createElement("input");
        searchInput.type = "search";
        searchInput.placeholder = select.getAttribute("aria-label") || select.title || "Type to filter options";
        searchInput.autocomplete = "off";
        searchInput.value = initialQuery || "";
        searchInput.addEventListener("input", () => renderOptions(searchInput.value));
        searchInput.addEventListener("keydown", event => {
            if (event.key === "Escape") {
                event.preventDefault();
                closePopup();
                select.focus();
            } else if (event.key === "ArrowDown") {
                event.preventDefault();
                optionList?.querySelector("button:not(:disabled)")?.focus();
            }
        });
        search.append(icon, searchInput);
        optionList = document.createElement("div");
        optionList.className = "app-searchable-select__options";
        optionList.setAttribute("role", "listbox");
        popup.append(search, optionList);
        document.body.appendChild(popup);
        positionPopup();
        renderOptions(searchInput.value);
        requestAnimationFrame(() => searchInput?.focus());
    }

    document.addEventListener("pointerdown", event => {
        const select = event.target.closest?.("select");
        if (select && !select.disabled && select.dataset.nativeSelect !== "true" && Number(select.size) <= 1) {
            event.preventDefault();
            openPopup(select, "");
            return;
        }
        if (popup && !popup.contains(event.target)) closePopup();
    }, true);

    document.addEventListener("keydown", event => {
        const select = event.target instanceof HTMLSelectElement ? event.target : null;
        if (select && !select.disabled && select.dataset.nativeSelect !== "true") {
            if (event.key === "Enter" || event.key === " " || event.key === "ArrowDown" || (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey)) {
                event.preventDefault();
                openPopup(select, event.key.length === 1 ? event.key : "");
            }
        } else if (event.key === "Escape" && popup) closePopup();
    });

    window.addEventListener("resize", positionPopup);
    window.addEventListener("scroll", positionPopup, true);
})();
