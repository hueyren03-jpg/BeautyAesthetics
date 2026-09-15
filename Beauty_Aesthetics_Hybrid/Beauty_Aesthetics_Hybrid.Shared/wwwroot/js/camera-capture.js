/**
 * ==========================================
 * CAMERA CAPTURE FUNCTIONALITY
 * ==========================================
 * Handles device camera access and image capture
 */

let cameraStream = null;
let cameraVideo = null;
let cameraCanvas = null;
let flipH = false;
let flipV = false;

/**
 * Start the camera and display video feed
 */
window.startCamera = async function() {
    try {
        console.log('Starting camera...');
        
        // Wait for DOM elements to be available
        let attempts = 0;
        while (attempts < 10) {
            cameraVideo = document.getElementById('cameraVideo');
            cameraCanvas = document.getElementById('cameraCanvas');
            
            if (cameraVideo && cameraCanvas) {
                break;
            }
            
            await new Promise(resolve => setTimeout(resolve, 100));
            attempts++;
        }
        
        if (!cameraVideo || !cameraCanvas) {
            console.error('Camera elements not found after waiting');
            throw new Error('Camera video or canvas elements not found in DOM');
        }
        
        // Request camera access with ideal constraints
        const constraints = {
            video: {
                facingMode: 'user',
                width: { ideal: 1280 },
                height: { ideal: 720 }
            },
            audio: false
        };
        
        cameraStream = await navigator.mediaDevices.getUserMedia(constraints);
        cameraVideo.srcObject = cameraStream;
        
        // Ensure video is visible and will play
        cameraVideo.style.display = 'block';
        
        // Wait for video metadata to load
        await new Promise((resolve) => {
            if (cameraVideo.readyState >= 2) {
                resolve();
            } else {
                cameraVideo.addEventListener('loadedmetadata', resolve, { once: true });
            }
        });
        
        // Play the video
        try {
            await cameraVideo.play();
        } catch (playError) {
            console.warn('Auto-play prevented, trying again:', playError);
            // Some browsers prevent autoplay, try again after user interaction
            setTimeout(async () => {
                try {
                    await cameraVideo.play();
                } catch (e) {
                    console.error('Failed to play video:', e);
                }
            }, 100);
        }
        
        // Reset flip states
        flipH = false;
        flipV = false;
        updateVideoTransform();
        
        console.log('✓ Camera started successfully, video playing');
    } catch (error) {
        console.error('Error starting camera:', error);
        alert('Failed to access camera. Please ensure you have granted camera permissions.');
        throw error;
    }
};

/**
 * Stop the camera and release resources
 */
window.stopCamera = function() {
    try {
        if (cameraStream) {
            const tracks = cameraStream.getTracks();
            tracks.forEach(track => track.stop());
            cameraStream = null;
        }
        
        if (cameraVideo) {
            cameraVideo.srcObject = null;
        }
        
        // Reset flip states
        flipH = false;
        flipV = false;
        
        console.log('✓ Camera stopped');
    } catch (error) {
        console.error('Error stopping camera:', error);
    }
};

/**
 * Flip camera view (horizontal or vertical)
 */
window.flipCamera = function(direction) {
    if (direction === 'horizontal') {
        flipH = !flipH;
    } else if (direction === 'vertical') {
        flipV = !flipV;
    }
    
    updateVideoTransform();
    console.log(`✓ Camera flipped ${direction}, H: ${flipH}, V: ${flipV}`);
};

/**
 * Update video element transform based on flip states
 */
function updateVideoTransform() {
    if (!cameraVideo) return;
    
    let transform = '';
    
    if (flipH) {
        transform += 'scaleX(-1) ';
    }
    
    if (flipV) {
        transform += 'scaleY(-1) ';
    }
    
    cameraVideo.style.transform = transform.trim() || 'none';
}

/**
 * Capture image from video stream
 */
window.captureImage = function() {
    return new Promise((resolve, reject) => {
        try {
            if (!cameraVideo || !cameraCanvas) {
                reject(new Error('Camera elements not initialized'));
                return;
            }
            
            // Ensure video is still playing (don't pause it during capture)
            if (cameraVideo.paused) {
                cameraVideo.play().catch(err => {
                    console.warn('Warning: Video was paused during capture:', err);
                });
            }
            
            // Set canvas size to match video
            const videoWidth = cameraVideo.videoWidth;
            const videoHeight = cameraVideo.videoHeight;
            
            cameraCanvas.width = videoWidth;
            cameraCanvas.height = videoHeight;
            
            const ctx = cameraCanvas.getContext('2d');
            
            // Apply transformations to context
            ctx.save();
            
            // Set up transformations based on flip states
            if (flipH || flipV) {
                // Move to center for flipping
                ctx.translate(videoWidth / 2, videoHeight / 2);
                
                if (flipH) {
                    ctx.scale(-1, 1);
                }
                
                if (flipV) {
                    ctx.scale(1, -1);
                }
                
                // Move back
                ctx.translate(-videoWidth / 2, -videoHeight / 2);
            }
            
            // Draw video frame to canvas
            ctx.drawImage(cameraVideo, 0, 0, videoWidth, videoHeight);
            ctx.restore();
            
            // Convert canvas to base64 data URL
            const dataUrl = cameraCanvas.toDataURL('image/png', 0.9);
            
            const sizeKB = Math.round(dataUrl.length / 1024);
            console.log(`✓ Image captured: ${videoWidth}x${videoHeight}, ${sizeKB} KB`);
            
            resolve(dataUrl);
        } catch (error) {
            console.error('Error capturing image:', error);
            reject(error);
        }
    });
};

/**
 * Reset camera (restart video feed)
 */
window.resetCamera = async function() {
    try {
        // Reset flip states
        flipH = false;
        flipV = false;
        updateVideoTransform();
        
        // Ensure video element is visible and playing
        if (cameraVideo) {
            // Make sure video element is visible (in case it was hidden)
            cameraVideo.style.display = 'block';
            
            // If video is paused, resume it
            if (cameraVideo.paused && cameraVideo.readyState >= 2) {
                await cameraVideo.play().catch(err => {
                    console.warn('Error playing video after reset:', err);
                });
            }
            
            // Ensure video stream is still active
            if (!cameraVideo.srcObject || !cameraStream) {
                console.warn('Video stream not found during reset');
                return;
            }
            
            // Check if stream tracks are active
            const tracks = cameraStream.getTracks();
            const activeTracks = tracks.filter(track => track.readyState === 'live' && track.enabled);
            
            if (activeTracks.length === 0) {
                console.warn('No active camera tracks found during reset');
                return;
            }
            
            // Wait for video to have metadata if needed
            if (cameraVideo.readyState < 2) {
                await new Promise((resolve, reject) => {
                    const timeout = setTimeout(() => {
                        cameraVideo.removeEventListener('loadedmetadata', handler);
                        reject(new Error('Timeout waiting for video metadata'));
                    }, 2000);
                    
                    const handler = () => {
                        clearTimeout(timeout);
                        cameraVideo.removeEventListener('loadedmetadata', handler);
                        resolve();
                    };
                    
                    cameraVideo.addEventListener('loadedmetadata', handler);
                }).catch(err => {
                    console.warn('Timeout waiting for video metadata:', err);
                });
            }
            
            // Play video if paused
            if (cameraVideo.paused) {
                try {
                    await cameraVideo.play();
                    console.log('✓ Video playing after reset');
                } catch (playError) {
                    console.warn('Error playing video after reset:', playError);
                    // Retry after a short delay
                    setTimeout(async () => {
                        try {
                            await cameraVideo.play();
                            console.log('✓ Video playing after retry');
                        } catch (e) {
                            console.error('Failed to play video on retry:', e);
                        }
                    }, 500);
                }
            }
            
            // Verify video has content - wait up to 2 seconds for dimensions
            if (cameraVideo.videoWidth === 0 || cameraVideo.videoHeight === 0) {
                console.warn('Video has no dimensions, waiting for stream...');
                for (let i = 0; i < 20; i++) {
                    await new Promise(resolve => setTimeout(resolve, 100));
                    if (cameraVideo.videoWidth > 0 && cameraVideo.videoHeight > 0) {
                        console.log('✓ Video dimensions available');
                        break;
                    }
                }
            }
        }
        
        console.log('✓ Camera reset - video feed restored');
    } catch (error) {
        console.error('Error resetting camera:', error);
    }
};

/**
 * Check if browser supports camera
 */
window.checkCameraSupport = function() {
    return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
};

// Cleanup on page unload
window.addEventListener('beforeunload', () => {
    if (typeof window.stopCamera === 'function') {
        window.stopCamera();
    }
});

