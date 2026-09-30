(function () {
    "use strict";

    function normalizeBase64(value) {
        const text = String(value || "").trim();
        const commaIndex = text.indexOf(",");

        return text.startsWith("data:") && commaIndex >= 0
            ? text.substring(commaIndex + 1)
            : text;
    }

    function base64ToBytes(value) {
        const binary = atob(normalizeBase64(value));
        const bytes = new Uint8Array(binary.length);

        for (let index = 0; index < binary.length; index += 1) {
            bytes[index] = binary.charCodeAt(index);
        }

        return bytes;
    }

    function bytesToBase64(bytes) {
        const chunkSize = 0x8000;
        let binary = "";

        for (let index = 0; index < bytes.length; index += chunkSize) {
            binary += String.fromCharCode.apply(
                null,
                bytes.subarray(index, Math.min(index + chunkSize, bytes.length)));
        }

        return btoa(binary);
    }

    async function loadLogoBytes(logoUrl) {
        const response = await fetch(logoUrl, { cache: "force-cache" });
        if (!response.ok) {
            throw new Error(`Unable to load the EBI receipt logo (${response.status}).`);
        }

        return new Uint8Array(await response.arrayBuffer());
    }

    window.receiptPdfBranding = {
        replaceLogo: async function (base64Pdf, logoUrl) {
            if (!window.PDFLib) {
                throw new Error("The PDF branding library is unavailable.");
            }

            const pdfBytes = base64ToBytes(base64Pdf);
            const pdfDocument = await window.PDFLib.PDFDocument.load(pdfBytes);
            const pages = pdfDocument.getPages();

            if (pages.length === 0) {
                throw new Error("The receipt PDF has no pages.");
            }

            const firstPage = pages[0];
            const pageSize = firstPage.getSize();
            const scale = pageSize.width / 204;

            // The receipt API places its logo in this isolated top-centre band.
            // Cover only that band so the company name and address remain untouched.
            firstPage.drawRectangle({
                x: 0,
                y: pageSize.height - (52 * scale),
                width: pageSize.width,
                height: 44 * scale,
                color: window.PDFLib.rgb(1, 1, 1)
            });

            const logo = await pdfDocument.embedPng(await loadLogoBytes(logoUrl));
            const logoBox = 54 * scale;
            firstPage.drawImage(logo, {
                x: (pageSize.width - logoBox) / 2,
                y: pageSize.height - (60 * scale),
                width: logoBox,
                height: logoBox
            });

            return bytesToBase64(await pdfDocument.save());
        }
    };
})();
