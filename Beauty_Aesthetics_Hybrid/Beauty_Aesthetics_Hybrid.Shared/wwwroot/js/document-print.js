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

// Download document as PDF (opens print dialog)
window.downloadDocumentAsPDF = function (htmlContent, fileName) {
    console.log('📥 Downloading document as PDF:', fileName);
    
    // Use print function which allows user to save as PDF
    window.printDocument(htmlContent, fileName);
};

console.log('✓ Document Print Module Loaded');







