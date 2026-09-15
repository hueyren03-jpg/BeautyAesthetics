// Prescription Print and PDF Generation Module
console.log('📄 Loading Prescription Print Module...');

// Print prescription
window.printPrescription = function (htmlContent, prescriptionNumber) {
    console.log('🖨️ Printing prescription:', prescriptionNumber);
    
    // Remove any existing print iframe first
    const existingFrame = document.getElementById('prescription-print-frame');
    if (existingFrame && existingFrame.parentNode) {
        existingFrame.parentNode.removeChild(existingFrame);
    }
    
    // Create a hidden iframe for printing
    const printFrame = document.createElement('iframe');
    printFrame.id = 'prescription-print-frame';
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

// Generate PDF from prescription HTML
window.generatePrescriptionPDF = function (htmlContent, fileName) {
    console.log('📄 Generating PDF:', fileName);
    
    // Use the same print function (user can select "Save as PDF" in print dialog)
    window.printPrescription(htmlContent, fileName);
};

console.log('✓ Prescription Print Module Loaded');

