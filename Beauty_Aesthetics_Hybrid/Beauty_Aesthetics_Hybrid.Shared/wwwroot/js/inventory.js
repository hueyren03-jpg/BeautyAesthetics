'use strict';

let tooltipInstances = [];
let barcodeScannerStream = null;
let barcodeScannerFrame = null;
let barcodeScannerActive = false;
let barcodeScannerVideo = null;
let barcodeScannerDetector = null;
let barcodeScannerDotNetReference = null;

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

export async function startBarcodeScanner(videoElement, dotNetReference) {
    stopBarcodeScanner();

    if (!videoElement) {
        return 'Camera preview could not be opened. Please try again.';
    }

    if (!navigator.mediaDevices?.getUserMedia) {
        return 'Camera access is unavailable on this device. Use a handheld scanner or type the barcode.';
    }

    if (!('BarcodeDetector' in globalThis)) {
        return 'Camera barcode scanning is not supported on this device. Use a handheld scanner or type the barcode.';
    }

    try {
        const preferredFormats = [
            'ean_13',
            'ean_8',
            'upc_a',
            'upc_e',
            'code_128',
            'code_39',
            'code_93',
            'itf',
            'codabar',
            'data_matrix',
            'qr_code'
        ];
        const supportedFormats = typeof BarcodeDetector.getSupportedFormats === 'function'
            ? await BarcodeDetector.getSupportedFormats()
            : [];
        const formats = preferredFormats.filter(format => supportedFormats.includes(format));

        barcodeScannerDetector = formats.length > 0
            ? new BarcodeDetector({ formats })
            : new BarcodeDetector();
        barcodeScannerDotNetReference = dotNetReference;
        barcodeScannerVideo = videoElement;
        barcodeScannerStream = await navigator.mediaDevices.getUserMedia({
            video: {
                facingMode: { ideal: 'environment' },
                width: { ideal: 1280 },
                height: { ideal: 720 }
            },
            audio: false
        });

        barcodeScannerVideo.srcObject = barcodeScannerStream;
        barcodeScannerVideo.setAttribute('playsinline', '');
        barcodeScannerVideo.muted = true;
        await barcodeScannerVideo.play();

        barcodeScannerActive = true;
        barcodeScannerFrame = requestAnimationFrame(scanBarcodeFrame);
        return null;
    } catch (error) {
        console.error('[InventoryInterop] Unable to start barcode scanner:', error);
        stopBarcodeScanner();

        if (error?.name === 'NotAllowedError' || error?.name === 'SecurityError') {
            return 'Camera permission was not granted. Allow camera access, or use a handheld scanner.';
        }
        if (error?.name === 'NotFoundError' || error?.name === 'OverconstrainedError') {
            return 'No suitable camera was found. Use a handheld scanner or type the barcode.';
        }
        if (error?.name === 'NotReadableError') {
            return 'The camera is being used by another app. Close it there and try again.';
        }

        return 'Camera scanning could not start. Use a handheld scanner or type the barcode.';
    }
}

async function scanBarcodeFrame() {
    if (!barcodeScannerActive || !barcodeScannerVideo || !barcodeScannerDetector) {
        return;
    }

    try {
        if (barcodeScannerVideo.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA) {
            const results = await barcodeScannerDetector.detect(barcodeScannerVideo);
            const barcode = results.find(result => result.rawValue?.trim());

            if (barcode) {
                const detectedValue = barcode.rawValue.trim();
                const callbackReference = barcodeScannerDotNetReference;
                stopBarcodeScanner();

                if (callbackReference) {
                    await callbackReference.invokeMethodAsync('OnGrnBarcodeDetected', detectedValue);
                }
                return;
            }
        }
    } catch (error) {
        console.warn('[InventoryInterop] Barcode frame could not be read:', error);
    }

    if (barcodeScannerActive) {
        barcodeScannerFrame = requestAnimationFrame(scanBarcodeFrame);
    }
}

export function stopBarcodeScanner() {
    barcodeScannerActive = false;

    if (barcodeScannerFrame !== null) {
        cancelAnimationFrame(barcodeScannerFrame);
        barcodeScannerFrame = null;
    }

    if (barcodeScannerStream) {
        barcodeScannerStream.getTracks().forEach(track => track.stop());
        barcodeScannerStream = null;
    }

    if (barcodeScannerVideo) {
        barcodeScannerVideo.pause();
        barcodeScannerVideo.srcObject = null;
    }

    barcodeScannerVideo = null;
    barcodeScannerDetector = null;
    barcodeScannerDotNetReference = null;
}

window.addEventListener('pagehide', stopBarcodeScanner);
