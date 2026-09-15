'use strict';

let tooltipInstances = [];

export function initializeTooltips() {
    disposeTooltips();

    if (!window.bootstrap || !window.bootstrap.Tooltip) {
        console.warn('[InventoryInterop] Bootstrap tooltip library is not available.');
        return;
    }

    const triggers = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipInstances = Array.from(triggers).map(trigger => new bootstrap.Tooltip(trigger));
}

export function disposeTooltips() {
    if (tooltipInstances.length === 0) {
        return;
    }

    tooltipInstances.forEach(instance => instance.dispose());
    tooltipInstances = [];
}

export function downloadFile(fileName, base64Data, mimeType) {
    if (!base64Data || !fileName) {
        console.warn('[InventoryInterop] downloadFile called without required parameters.');
        return;
    }

    try {
        const byteCharacters = atob(base64Data);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: mimeType || 'application/octet-stream' });
        
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.style.display = 'none';
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
    } catch (error) {
        console.error('[InventoryInterop] Error downloading file:', error);
    }
}

export function printHtml(htmlContent) {
    if (!htmlContent) {
        console.warn('[InventoryInterop] printHtml called without content.');
        return;
    }

    const frame = document.createElement('iframe');
    frame.style.position = 'fixed';
    frame.style.right = '0';
    frame.style.bottom = '0';
    frame.style.width = '0';
    frame.style.height = '0';
    frame.style.border = '0';

    document.body.appendChild(frame);

    const frameDoc = frame.contentWindow?.document;
    if (!frameDoc) {
        document.body.removeChild(frame);
        throw new Error('[InventoryInterop] Unable to access print frame document.');
    }

    frameDoc.open();
    frameDoc.write(htmlContent);
    frameDoc.close();

    setTimeout(() => {
        const frameWindow = frame.contentWindow;
        frameWindow?.focus();
        frameWindow?.print();

        setTimeout(() => {
            document.body.removeChild(frame);
        }, 1000);
    }, 50);
}

export function openFilePicker(inputElementId) {
    if (!inputElementId) {
        console.warn('[InventoryInterop] openFilePicker called without inputElementId.');
        return;
    }

    const input = document.getElementById(inputElementId);
    if (!input) {
        console.warn(`[InventoryInterop] No file input found with id: ${inputElementId}`);
        return;
    }

    input.click();
}
