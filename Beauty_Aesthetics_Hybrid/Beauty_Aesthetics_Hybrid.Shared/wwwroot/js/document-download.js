/**
 * Document Download Module
 * Handles downloading documents from the DocumentPage
 */

// Download a document file
window.downloadDocumentFile = function (fileName, filePath) {
    try {
        // If filePath is a data URL (base64), create blob from it
        if (filePath.startsWith('data:')) {
            const blob = dataURLtoBlob(filePath);
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
            return true;
        } else {
            // For regular file paths, create a link and trigger download
            const link = document.createElement('a');
            link.href = filePath;
            link.download = fileName;
            link.target = '_blank';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            return true;
        }
    } catch (error) {
        console.error('Error downloading document file:', error);
        return false;
    }
};

// Download HTML content as a file
window.downloadDocumentContent = function (fileName, htmlContent) {
    try {
        // Create a blob from HTML content
        const blob = new Blob([htmlContent], { type: 'text/html' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
        return true;
    } catch (error) {
        console.error('Error downloading document content:', error);
        return false;
    }
};

// Convert data URL to Blob
function dataURLtoBlob(dataurl) {
    const arr = dataurl.split(',');
    const mime = arr[0].match(/:(.*?);/)[1];
    const bstr = atob(arr[1]);
    let n = bstr.length;
    const u8arr = new Uint8Array(n);
    while (n--) {
        u8arr[n] = bstr.charCodeAt(n);
    }
    return new Blob([u8arr], { type: mime });
}

console.log('✓ Document Download Module Loaded');

