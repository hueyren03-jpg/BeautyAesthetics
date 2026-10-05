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

    let textReaderPromise;
    function loadTextReader() {
        // Loaded only when a receipt is downloaded; no cost to sales-page loading.
        if (!textReaderPromise) {
            const libraryRoot = "https://cdn.jsdelivr.net/npm/pdfjs-dist@5.4.624/legacy/build/";
            textReaderPromise = import(libraryRoot + "pdf.min.mjs").then(function (library) {
                library.GlobalWorkerOptions.workerSrc = libraryRoot + "pdf.worker.min.mjs";
                return library;
            }).catch(function (error) {
                textReaderPromise = null;
                throw error;
            });
        }
        return textReaderPromise;
    }

    function compact(text) {
        return String(text || "").replace(/\s+/g, "").toLowerCase();
    }

    function textRows(content) {
        const entries = content.items.filter(item => typeof item.str === "string" && item.str.trim())
            .map(item => ({
                text: item.str.trim(), x: item.transform[4], y: item.transform[5],
                width: item.width, size: Math.hypot(item.transform[2], item.transform[3])
            })).sort((a, b) => b.y - a.y || a.x - b.x);
        const rows = [];
        for (const entry of entries) {
            let row = rows.find(candidate => Math.abs(candidate.y - entry.y) < 1.5);
            if (!row) {
                row = { y: entry.y, entries: [] };
                rows.push(row);
            }
            row.entries.push(entry);
        }
        for (const row of rows) row.entries.sort((a, b) => a.x - b.x);
        return rows;
    }

    function missingCell(entry) {
        return entry && /^[\-\u2013\u2014]+$/.test(entry.text);
    }

    function numberCell(entry) {
        return entry && (missingCell(entry) || /^\(?-?[\d,]+(?:\.\d+)?\)?$/.test(entry.text));
    }

    function rowText(row) {
        return row.entries.map(entry => entry.text).join(" ");
    }

    function itemColumns(row, rows, pageWidth) {
        const item = row.entries.find(entry => compact(entry.text) === "item");
        const quantity = row.entries.find(entry => compact(entry.text) === "qty");
        const price = row.entries.find(entry => compact(entry.text) === "price");
        if (!item || !quantity || !price) return null;
        const amount = rows.flatMap(candidate => Math.abs(candidate.y - row.y) < 12 ? candidate.entries : [])
            .find(entry => compact(entry.text) === "amount");
        if (!amount) return null;
        const center = entry => entry.x + entry.width / 2;
        return {
            quantityStart: (center(item) + center(quantity)) / 2,
            priceStart: (center(quantity) + center(price)) / 2,
            amountStart: (center(price) + center(amount)) / 2,
            right: pageWidth - item.x
        };
    }

    async function fillMissingItemAmounts(base64Pdf, itemValues) {
        if (!window.PDFLib) throw new Error("The PDF library is unavailable.");
        const library = await loadTextReader();
        const bytes = base64ToBytes(base64Pdf);
        const loading = library.getDocument({ data: bytes.slice(), isEvalSupported: false });
        const source = await loading.promise;
        const patches = [];
        const values = Array.isArray(itemValues) ? itemValues : [];
        let itemIndex = 0;
        let columns = null;
        let insideItems = false;
        let identity = [];
        let foundTable = false;

        try {
            for (let pageNumber = 1; pageNumber <= source.numPages; pageNumber += 1) {
                const page = await source.getPage(pageNumber);
                const rows = textRows(await page.getTextContent());
                const pageWidth = page.view[2] - page.view[0];
                for (const row of rows) {
                    const headerColumns = itemColumns(row, rows, pageWidth);
                    if (headerColumns) {
                        columns = headerColumns;
                        insideItems = true;
                        foundTable = true;
                        continue;
                    }
                    if (!insideItems) continue;
                    if (compact(rowText(row)).startsWith("itemcount:")) {
                        insideItems = false;
                        identity = [];
                        continue;
                    }
                    // Ignore the currency-label row below the column headings.
                    if (row.entries.every(entry => /^\([A-Z]{2,4}\)$/.test(entry.text))) continue;

                    const quantity = row.entries.find(entry =>
                        entry.x >= columns.quantityStart && entry.x < columns.priceStart && numberCell(entry));
                    const price = row.entries.find(entry =>
                        entry.x >= columns.priceStart && entry.x < columns.amountStart && numberCell(entry));
                    const amount = row.entries.find(entry => entry.x >= columns.amountStart && numberCell(entry));
                    identity.push(...row.entries.filter(entry => entry.x < columns.quantityStart)
                        .map(entry => entry.text));
                    if (!quantity || !price || !amount) continue;

                    const missing = [quantity, price, amount].some(missingCell);
                    const value = values[itemIndex];
                    if (missing) {
                        const printedIdentity = compact(identity.join(" "));
                        const matches = value && (
                            (compact(value.sku) && printedIdentity.includes(compact(value.sku))) ||
                            (compact(value.description) && printedIdentity.includes(compact(value.description))));
                        if (!matches) {
                            throw new Error(`The saved sale could not be matched to receipt item ${itemIndex + 1}.`);
                        }
                        if (!missingCell(quantity) &&
                            Math.abs(Number(quantity.text.replace(/,/g, "")) - Number(value.quantity)) > 0.0001) {
                            throw new Error(`The saved quantity does not match receipt item ${itemIndex + 1}.`);
                        }

                        const cells = [
                            { entry: quantity, value: value.quantity, left: columns.quantityStart,
                                right: columns.priceStart, align: "center", money: false },
                            { entry: price, value: value.unitPrice, left: columns.priceStart,
                                right: columns.amountStart, align: "center", money: true },
                            { entry: amount, value: value.amount, left: columns.amountStart,
                                right: columns.right, align: "right", money: true }
                        ];
                        for (const cell of cells.filter(cell => missingCell(cell.entry))) {
                            if (cell.value === null || cell.value === undefined || !Number.isFinite(Number(cell.value))) {
                                throw new Error(`The original ${cell.money ? "price/amount" : "quantity"} is missing for ${value.sku || value.description}.`);
                            }
                            patches.push({ pageNumber, ...cell });
                        }
                    }
                    itemIndex += 1;
                    identity = [];
                }
                page.cleanup();
                // The remainder is the original tax/balance/redemption report,
                // not an item table. Do not parse or rewrite those pages.
                if (foundTable && !insideItems) break;
            }
        } finally {
            await loading.destroy();
        }

        if (!foundTable) throw new Error("The receipt API returned an unrecognised item-table layout.");
        if (!patches.length) return base64Pdf;
        if (itemIndex !== values.length) {
            throw new Error("The saved sale item count does not match the receipt item count.");
        }

        const pdf = await window.PDFLib.PDFDocument.load(bytes);
        const font = await pdf.embedFont(window.PDFLib.StandardFonts.Helvetica);
        for (const patch of patches) {
            const page = pdf.getPages()[patch.pageNumber - 1];
            const entry = patch.entry;
            const text = Number(patch.value).toLocaleString("en-US", {
                minimumFractionDigits: patch.money ? 2 : 0,
                maximumFractionDigits: patch.money ? 2 : 3
            });
            const left = Math.max(0, patch.left);
            const right = Math.min(page.getSize().width, patch.right);
            if (!Number.isFinite(left) || !Number.isFinite(right) || right <= left) {
                throw new Error("The receipt item column has invalid bounds.");
            }

            // A dash may sit at the edge of the inferred column. Its position
            // is an alignment preference, not the width available for a number.
            // Fit against the whole column, then keep the text inside its bounds.
            const padding = Math.min(1, (right - left) / 4);
            const availableWidth = right - left - 2 * padding;
            const originalSize = Number.isFinite(entry.size) && entry.size > 0 ? entry.size : 8;
            // Leave a small fitting tolerance for floating-point text metrics.
            const size = Math.min(originalSize,
                availableWidth * 0.99 / font.widthOfTextAtSize(text, 1));
            const width = font.widthOfTextAtSize(text, size);
            const anchor = patch.align === "right" ? entry.x + entry.width : entry.x + entry.width / 2;
            const preferredX = patch.align === "right" ? anchor - width : anchor - width / 2;
            const x = Math.max(left + padding, Math.min(preferredX, right - padding - width));
            // Replace only the dash, at its original baseline. All other report
            // content (including tax, balances and redemption history) is intact.
            page.drawRectangle({ x: entry.x - 0.5, y: entry.y - originalSize * 0.25,
                width: entry.width + 1, height: originalSize * 1.25,
                color: window.PDFLib.rgb(1, 1, 1) });
            page.drawText(text, { x, y: entry.y, size, font, color: window.PDFLib.rgb(0, 0, 0) });
        }
        console.info(`[Receipt PDF] Filled ${patches.length} missing item cells from the original sale.`);
        return bytesToBase64(await pdf.save());
    }

    window.receiptPdfBranding = {
        fillMissingItemAmounts,
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
