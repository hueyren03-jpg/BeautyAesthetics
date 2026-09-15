// Suppress browser extension errors that don't affect the application
(function() {
    // Store original console.error
    const originalError = console.error;
    
    // Override console.error to filter out extension-related errors
    console.error = function(...args) {
        const errorMessage = args.join(' ');
        
        // Filter out known extension errors
        const ignoredErrors = [
            'immersivetranslate.com',
            'content_script.js',
            'fetchError',
            'Connection timed out'
        ];
        
        // Check if error should be ignored
        const shouldIgnore = ignoredErrors.some(pattern => 
            errorMessage.includes(pattern)
        );
        
        // Only log if not an ignored error
        if (!shouldIgnore) {
            originalError.apply(console, args);
        }
    };
    
    // Handle unhandled promise rejections from extensions
    window.addEventListener('unhandledrejection', function(event) {
        const errorMessage = event.reason?.toString() || '';
        
        if (errorMessage.includes('immersivetranslate') || 
            errorMessage.includes('content_script')) {
            event.preventDefault(); // Suppress the error
        }
    });
})();
