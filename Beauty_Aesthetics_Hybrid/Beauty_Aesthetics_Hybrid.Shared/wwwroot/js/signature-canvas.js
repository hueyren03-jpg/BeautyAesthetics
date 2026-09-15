// Signature Canvas Module
(function() {
    'use strict';

    let canvas = null;
    let ctx = null;
    let isDrawing = false;

    // Initialize signature canvas
    window.initializeSignatureCanvas = function() {
        canvas = document.getElementById('signatureCanvas');
        
        if (!canvas) {
            console.warn('Signature canvas not found');
            return;
        }

        ctx = canvas.getContext('2d');
        
        // Set canvas styling
        ctx.strokeStyle = '#1e40af'; // Blue ink color
        ctx.lineWidth = 2;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        
        // Clear canvas with white background
        ctx.fillStyle = 'white';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        
        console.log('✓ Signature canvas initialized');
    };

    // Draw signature line
    window.drawSignature = function(x, y, lastX, lastY) {
        if (!canvas || !ctx) {
            console.warn('Canvas not initialized');
            return;
        }

        ctx.beginPath();
        ctx.moveTo(lastX, lastY);
        ctx.lineTo(x, y);
        ctx.stroke();
    };

    // Clear signature canvas
    window.clearSignatureCanvas = function() {
        if (!canvas || !ctx) {
            console.warn('Canvas not initialized');
            return;
        }

        ctx.fillStyle = 'white';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        console.log('✓ Signature canvas cleared');
    };

    // Get signature as data URL
    window.getSignatureDataUrl = function() {
        if (!canvas) {
            console.warn('Canvas not initialized');
            return '';
        }

        // Check if canvas is blank
        const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const pixels = imageData.data;
        let isBlank = true;

        // Check if any pixel is not white
        for (let i = 0; i < pixels.length; i += 4) {
            if (pixels[i] !== 255 || pixels[i + 1] !== 255 || pixels[i + 2] !== 255) {
                isBlank = false;
                break;
            }
        }

        if (isBlank) {
            return '';
        }

        return canvas.toDataURL('image/png');
    };

    // Get canvas bounding client rect for touch events
    window.getCanvasRect = function() {
        if (!canvas) {
            return { Left: 0, Top: 0, Right: 0, Bottom: 0, Width: 0, Height: 0 };
        }

        const rect = canvas.getBoundingClientRect();
        return {
            Left: rect.left,
            Top: rect.top,
            Right: rect.right,
            Bottom: rect.bottom,
            Width: rect.width,
            Height: rect.height
        };
    };

    // Convert screen coordinates to canvas coordinates
    window.getCanvasCoordinates = function(clientX, clientY) {
        if (!canvas) {
            return { x: 0, y: 0 };
        }

        const rect = canvas.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        
        const x = (clientX - rect.left) * scaleX;
        const y = (clientY - rect.top) * scaleY;
        
        return { x: x, y: y };
    };

    console.log('✓ Signature Canvas Module Loaded');
})();

