// Document Print and PDF Generation Module
console.log('📄 Loading Document Print Module...');

// Print document
window.printDocument = function (htmlContent, documentTitle) {
    console.log('🖨️ Printing document:', documentTitle);
    
    // Remove any existing print iframe first
    const existingFrame = document.getElementById('document-print-frame');
    if (existingFrame && existingFrame.parentNode) {
        existingFrame.parentNode.removeChild(existingFrame);
    }
    
    // Create a hidden iframe for printing
    const printFrame = document.createElement('iframe');
    printFrame.id = 'document-print-frame';
    printFrame.style.position = 'fixed';
    printFrame.style.right = '0';
    printFrame.style.bottom = '0';
    printFrame.style.width = '0';
    printFrame.style.height = '0';
    printFrame.style.border = '0';
    printFrame.style.visibility = 'hidden';
    printFrame.style.display = 'none';
    document.body.appendChild(printFrame);
    
    // Write the HTML content to the iframe
    const printDoc = printFrame.contentWindow.document;
    printDoc.open();
    printDoc.write(htmlContent);
    printDoc.close();
    
    // Flag to prevent multiple print calls
    let printTriggered = false;
    let fallbackTimeout = null;
    
    // Function to trigger print
    function triggerPrint() {
        if (printTriggered) return;
        printTriggered = true;
        
        // Clear fallback timeout if it exists
        if (fallbackTimeout) {
            clearTimeout(fallbackTimeout);
            fallbackTimeout = null;
        }
        
        setTimeout(function () {
            try {
                printFrame.contentWindow.focus();
                printFrame.contentWindow.print();
                console.log('✓ Print dialog opened');
            } catch (e) {
                console.error('Print error:', e);
            }
            
            // Remove iframe after a delay (allow print dialog to open)
            setTimeout(function () {
                if (printFrame && printFrame.parentNode) {
                    try {
                        document.body.removeChild(printFrame);
                    } catch (e) {
                        console.error('Error removing iframe:', e);
                    }
                }
            }, 1000);
        }, 250);
    }
    
    // Wait for content to load, then trigger print
    printFrame.onload = function () {
        triggerPrint();
    };
    
    // Fallback if onload doesn't fire (with timeout to prevent double calls)
    fallbackTimeout = setTimeout(function () {
        if (!printTriggered && printFrame.contentWindow) {
            triggerPrint();
        }
    }, 500);
};

// Generate PDF from document HTML (opens print dialog where user can save as PDF)
window.generateDocumentPDF = function (htmlContent, fileName) {
    console.log('📄 Generating PDF:', fileName);
    
    // Use the same print function (user can select "Save as PDF" in print dialog)
    window.printDocument(htmlContent, fileName);
};

// Download document as a real PDF using the local Beauty receipt HTML.
// This intentionally avoids the backend thermal PDF so server-side Senang
// branding cannot leak into Beauty Aesthetics receipts.
window.downloadDocumentAsPDF = async function (htmlContent, fileName) {
    console.log('📥 Downloading branded PDF:', fileName);

    const normalizedFileName = (fileName || 'Receipt.pdf').toLowerCase().endsWith('.pdf')
        ? fileName
        : (fileName || 'Receipt') + '.pdf';

    if (typeof window.html2pdf !== 'function') {
        console.warn('html2pdf is unavailable; opening the print dialog as PDF fallback.');
        window.printDocument(htmlContent, normalizedFileName);
        return true;
    }

    const existingFrame = document.getElementById('document-pdf-frame');
    if (existingFrame && existingFrame.parentNode) {
        existingFrame.parentNode.removeChild(existingFrame);
    }

    const frame = document.createElement('iframe');
    frame.id = 'document-pdf-frame';
    frame.style.position = 'fixed';
    frame.style.left = '-10000px';
    frame.style.top = '0';
    frame.style.width = '900px';
    frame.style.height = '1300px';
    frame.style.border = '0';
    frame.style.background = '#ffffff';
    document.body.appendChild(frame);

    try {
        const doc = frame.contentWindow.document;
        doc.open();
        doc.write(htmlContent);
        doc.close();

        await new Promise((resolve) => {
            if (doc.readyState === 'complete') {
                resolve();
                return;
            }

            frame.onload = () => resolve();
            setTimeout(resolve, 600);
        });

        if (doc.fonts && doc.fonts.ready) {
            try {
                await doc.fonts.ready;
            } catch (_) {
                // Font readiness is best-effort only.
            }
        }

        const images = Array.from(doc.images || []);
        await Promise.all(images.map((img) => {
            if (img.complete) return Promise.resolve();
            return new Promise((resolve) => {
                img.onload = resolve;
                img.onerror = resolve;
                setTimeout(resolve, 1500);
            });
        }));

        const target = doc.querySelector('.receipt') || doc.body;

        await window.html2pdf()
            .set({
                margin: [8, 8, 8, 8],
                filename: normalizedFileName,
                image: { type: 'jpeg', quality: 0.98 },
                html2canvas: {
                    scale: 2,
                    useCORS: true,
                    backgroundColor: '#ffffff',
                    logging: false
                },
                jsPDF: {
                    unit: 'mm',
                    format: 'a4',
                    orientation: 'portrait'
                },
                pagebreak: { mode: ['css', 'legacy'] }
            })
            .from(target)
            .save();

        console.log('✓ Branded PDF downloaded:', normalizedFileName);
        return true;
    } catch (error) {
        console.error('PDF generation failed:', error);
        return false;
    } finally {
        if (frame && frame.parentNode) {
            frame.parentNode.removeChild(frame);
        }
    }
};

console.log('✓ Document Print Module Loaded');







