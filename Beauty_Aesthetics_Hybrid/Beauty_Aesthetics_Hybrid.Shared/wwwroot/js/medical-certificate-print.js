// Medical Certificate Print Functionality

// Populate the print template with data
window.populateMCTemplate = function (certNumber, patientName, dateIssued, bodyText, period, duration, reason, doctorName, doctorRegNumber, signatureDataUrl) {
    console.log('Populating MC template with data...');
    
    // Update all template fields
    document.getElementById('print-cert-number').textContent = certNumber;
    document.getElementById('print-patient-name').textContent = patientName;
    document.getElementById('print-date-issued').textContent = dateIssued;
    document.getElementById('print-body-text').textContent = bodyText;
    document.getElementById('print-period').textContent = period;
    document.getElementById('print-duration').textContent = duration;
    document.getElementById('print-reason').textContent = reason;
    
    // Update doctor information
    document.getElementById('print-doctor-name').textContent = doctorName;
    document.getElementById('print-doctor-reg').textContent = 'Registration No: ' + doctorRegNumber;
    
    // Handle signature display
    const signatureImage = document.getElementById('print-signature-image');
    const signatureLine = document.getElementById('print-signature-line');
    
    if (signatureDataUrl && signatureDataUrl.trim() !== '') {
        // Show signature image, hide line
        signatureImage.src = signatureDataUrl;
        signatureImage.style.display = 'block';
        signatureLine.style.display = 'none';
    } else {
        // Show line, hide signature
        signatureImage.style.display = 'none';
        signatureLine.style.display = 'block';
    }
    
    console.log('✓ Template populated successfully');
    console.log('   - Certificate:', certNumber);
    console.log('   - Patient:', patientName);
    console.log('   - Doctor:', doctorName);
    console.log('   - Registration:', doctorRegNumber);
    console.log('   - Signature:', signatureDataUrl ? 'Yes' : 'No');
};

// Print the medical certificate (allows save as PDF)
window.printMedicalCertificate = function () {
    console.log('Opening print dialog...');
    
    // Small delay to ensure template is populated
    setTimeout(function () {
        // Open print dialog
        // Users can choose "Save as PDF" as the printer destination
        window.print();
        
        console.log('✓ Print dialog opened');
        console.log('📄 Tip: Select "Save as PDF" or "Microsoft Print to PDF" to save the certificate');
    }, 100);
};

// Optional: Direct PDF generation (requires additional library)
// For now, we use browser's built-in print-to-PDF functionality
window.downloadMCasPDF = function (certNumber) {
    console.log('Note: Use browser Print dialog and select "Save as PDF"');
    console.log('Suggested filename:', certNumber + '_Medical_Certificate.pdf');
    window.print();
};

console.log('✓ Medical Certificate Print Module Loaded');
