window._storedRemarkValue = null;
window._remarkTouched = false;

window.resetRemarkCapture = function () {
    window._storedRemarkValue = null;
    window._remarkTouched = false;
};

window.attachRemarkListener = function (id, initialValue) {
    var el = document.getElementById(id);
    if (!el) return;
    if (!window._remarkTouched) {
        // Not yet typed: if a C# value was passed, set it directly (Blazor's value binding
        // on <textarea> is unreliable in MAUI WebView — we set it explicitly here).
        if (initialValue !== undefined && initialValue !== null && initialValue !== '') {
            el.value = initialValue;
            window._storedRemarkValue = initialValue;
        } else {
            window._storedRemarkValue = el.value;
        }
    } else {
        // User has already typed: Blazor re-rendered and may have reset el.value —
        // restore our captured value so the user's input is not lost.
        el.value = window._storedRemarkValue || '';
    }
    el.removeEventListener('input', el._remarkHandler);
    el.removeEventListener('change', el._remarkHandler);
    el._remarkHandler = function () {
        window._remarkTouched = true;
        window._storedRemarkValue = el.value;
    };
    el.addEventListener('input', el._remarkHandler);
    el.addEventListener('change', el._remarkHandler);
};

window.getStoredRemark = function () {
    return window._storedRemarkValue;
};

window.getFieldValue = function (id) {
    var el = document.getElementById(id);
    return el ? el.value : null;
};

// Persist item remarks in localStorage so they survive app restarts
// (the EBI API does not reliably persist the Remarks field).
window.storeItemRemark = function (id, remark) {
    if (!id) return;
    var key = 'item_remark_' + id;
    if (remark) {
        localStorage.setItem(key, remark);
    } else {
        localStorage.removeItem(key);
    }
};

window.getItemRemark = function (id) {
    if (!id) return null;
    return localStorage.getItem('item_remark_' + id) || null;
};


window.modalHistoryHelper = {
    dotNetHelper: null,
    pushModalState: function (dotNetHelper) {
        this.dotNetHelper = dotNetHelper;
        history.pushState({ modalOpen: true }, "");
        window.onpopstate = () => {
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('CloseFromHardwareBack');
            }
        };
    },
    popModalState: function () {
        if (history.state && history.state.modalOpen) {
            window.onpopstate = null;
            history.back();
        }
    }
};
