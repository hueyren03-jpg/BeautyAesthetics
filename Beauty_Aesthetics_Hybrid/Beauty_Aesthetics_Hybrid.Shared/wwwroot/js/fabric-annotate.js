// Fabric.js Canvas Annotation System
(function () {
    'use strict';

    let fabricCanvas = null;
    let canvasState = {
        isInitialized: false,
        isReady: false,
        currentTool: 'pencil',
        currentColor: '#000000',
        lineThickness: 3,
        fontSize: 20, // Increased default font size for better readability
        isBold: false,
        isItalic: false,
        isUnderline: false,
        history: [],
        historyStep: -1,
        maxHistorySize: 50
    };

    // Debugging and diagnostic state
    let diagnosticState = {
        eventLoggingEnabled: false,
        performanceMonitoringEnabled: false,
        mouseEventLog: [],
        maxEventLogSize: 100,
        performanceMetrics: {
            drawingOperations: [],
            maxMetricsSize: 50
        }
    };

    // Initialize Fabric.js Canvas with retry logic and exponential backoff
    window.initializeFabricCanvas = function (retryCount = 0, maxRetries = 5) {
        console.log(`[INIT] Attempting canvas initialization (attempt ${retryCount + 1}/${maxRetries + 1})`);

        // Check if already initialized
        if (canvasState.isInitialized && fabricCanvas) {
            console.log('[INIT] Canvas already initialized');
            return { success: true, message: 'Canvas already initialized' };
        }

        // Check if Fabric.js is loaded
        if (typeof fabric === 'undefined') {
            console.warn('[INIT] Fabric.js not loaded yet');
            if (retryCount < maxRetries) {
                const delay = 100 * Math.pow(2, retryCount); // Exponential backoff
                console.log(`[INIT] Retrying in ${delay}ms...`);
                setTimeout(() => window.initializeFabricCanvas(retryCount + 1, maxRetries), delay);
                return { success: false, message: 'Fabric.js not loaded, retrying...' };
            } else {
                console.error('[ERROR] Failed to initialize: Fabric.js not loaded after maximum retries');
                return { success: false, error: 'Fabric.js not loaded' };
            }
        }

        // Check if canvas elements exist
        const canvasElement = document.getElementById('drawingCanvas');
        const canvasWrapper = document.getElementById('canvasWrapper');

        if (!canvasElement || !canvasWrapper) {
            console.warn('[INIT] Canvas elements not found in DOM');
            console.warn(`[INIT] canvasElement: ${canvasElement ? 'found' : 'missing'}, canvasWrapper: ${canvasWrapper ? 'found' : 'missing'}`);
            if (retryCount < maxRetries) {
                const delay = 100 * Math.pow(2, retryCount); // Exponential backoff
                console.log(`[INIT] Retrying in ${delay}ms...`);
                setTimeout(() => window.initializeFabricCanvas(retryCount + 1, maxRetries), delay);
                return { success: false, message: 'Canvas elements not found, retrying...' };
            } else {
                console.error('[ERROR] Failed to initialize: Canvas elements not found after maximum retries');
                return { success: false, error: 'Canvas elements not found' };
            }
        }

        try {
            // Get dimensions from wrapper
            const rect = canvasWrapper.getBoundingClientRect();
            
            // Validate dimensions
            if (rect.width === 0 || rect.height === 0) {
                console.warn('[INIT] Canvas wrapper has zero dimensions');
                console.warn(`[INIT] Dimensions: ${rect.width}x${rect.height}`);
                if (retryCount < maxRetries) {
                    const delay = 100 * Math.pow(2, retryCount);
                    console.log(`[INIT] Retrying in ${delay}ms...`);
                    setTimeout(() => window.initializeFabricCanvas(retryCount + 1, maxRetries), delay);
                    return { success: false, message: 'Canvas wrapper has zero dimensions, retrying...' };
                } else {
                    console.error('[ERROR] Failed to initialize: Canvas wrapper has zero dimensions');
                    return { success: false, error: 'Canvas wrapper has invalid dimensions' };
                }
            }

            console.log('[INIT] Initializing Fabric.js canvas with dimensions:', rect.width, 'x', rect.height);

            // Initialize Fabric.js canvas
            fabricCanvas = new fabric.Canvas('drawingCanvas', {
                width: rect.width,
                height: rect.height,
                isDrawingMode: true,
                backgroundColor: 'transparent',
                selection: true,
                defaultCursor: 'crosshair',
                hoverCursor: 'crosshair',
                freeDrawingCursor: 'crosshair'
            });

            // Set initial brush
            fabricCanvas.freeDrawingBrush = new fabric.PencilBrush(fabricCanvas);
            fabricCanvas.freeDrawingBrush.color = canvasState.currentColor;
            fabricCanvas.freeDrawingBrush.width = canvasState.lineThickness;

            // Mark as initialized
            canvasState.isInitialized = true;

            // Style canvas elements
            styleCanvasElements();

            // Force render to ensure upper canvas is created
            fabricCanvas.renderAll();

            // Log canvas structure for debugging
            console.log('Lower canvas:', fabricCanvas.lowerCanvasEl);
            console.log('Upper canvas:', fabricCanvas.upperCanvasEl);
            console.log('Canvas wrapper:', fabricCanvas.wrapperEl);

            // Setup event listeners
            setupEventListeners();

            // Handle window resize
            window.addEventListener('resize', handleResize);

            // Save initial state
            saveState();

            // Mark as ready
            canvasState.isReady = true;

            console.log('[INIT] ✓ Fabric.js canvas initialized successfully');
            console.log('[INIT] Canvas state:', {
                isInitialized: canvasState.isInitialized,
                isReady: canvasState.isReady,
                dimensions: `${rect.width}x${rect.height}`
            });
            return { success: true, message: 'Canvas initialized successfully' };

        } catch (error) {
            console.error('[ERROR] Error during canvas initialization:', error);
            console.error('[ERROR] Error details:', {
                name: error.name,
                message: error.message,
                stack: error.stack
            });
            canvasState.isInitialized = false;
            canvasState.isReady = false;
            
            if (retryCount < maxRetries) {
                const delay = 100 * Math.pow(2, retryCount);
                console.log(`[INIT] Retrying in ${delay}ms...`);
                setTimeout(() => window.initializeFabricCanvas(retryCount + 1, maxRetries), delay);
                return { success: false, message: 'Initialization error, retrying...', error: error.message };
            } else {
                console.error('[ERROR] ✗ Canvas initialization failed after all retries');
                return { success: false, error: error.message };
            }
        }
    };

    // Check if canvas is ready for operations
    window.isCanvasReady = function () {
        const ready = canvasState.isInitialized && canvasState.isReady && fabricCanvas !== null;
        if (!ready) {
            console.log('[STATE] Canvas not ready:', {
                isInitialized: canvasState.isInitialized,
                isReady: canvasState.isReady,
                fabricCanvas: fabricCanvas !== null
            });
        }
        return ready;
    };

    // Style all canvas elements
    function styleCanvasElements() {
        const canvasEl = fabricCanvas.getElement();
        const upperCanvas = fabricCanvas.upperCanvasEl;
        const canvasContainer = fabricCanvas.wrapperEl;

        // Style lower canvas - enable pointer events
        if (canvasEl) {
            canvasEl.style.cursor = 'crosshair';
            canvasEl.style.position = 'absolute';
            canvasEl.style.top = '0';
            canvasEl.style.left = '0';
            canvasEl.style.pointerEvents = 'auto';
            console.log('Lower canvas styled with pointer-events: auto');
        }

        // Style upper canvas (interaction layer) - CRITICAL for drawing
        if (upperCanvas) {
            upperCanvas.style.cursor = 'crosshair';
            upperCanvas.style.position = 'absolute';
            upperCanvas.style.top = '0';
            upperCanvas.style.left = '0';
            upperCanvas.style.pointerEvents = 'auto';
            upperCanvas.style.zIndex = '100';
            console.log('Upper canvas styled with pointer-events: auto');
        }

        // Style wrapper container - enable pointer events
        if (canvasContainer) {
            canvasContainer.style.position = 'absolute';
            canvasContainer.style.top = '0';
            canvasContainer.style.left = '0';
            canvasContainer.style.width = '100%';
            canvasContainer.style.height = '100%';
            canvasContainer.style.pointerEvents = 'auto';
            canvasContainer.style.zIndex = '999';
            console.log('Canvas container styled with pointer-events: auto');
        }

        // Configure canvas wrapper and its children
        const canvasWrapper = document.getElementById('canvasWrapper');
        if (canvasWrapper) {
            // Set wrapper to allow events
            canvasWrapper.style.position = 'relative';
            canvasWrapper.style.pointerEvents = 'auto';
            
            // Set pointer-events: none ONLY on background image
            const backgroundImage = canvasWrapper.querySelector('.canvas-image');
            if (backgroundImage) {
                backgroundImage.style.pointerEvents = 'none';
                console.log('Background image set to pointer-events: none');
            }
            
            // Ensure all canvas elements have pointer-events: auto
            const allCanvasElements = canvasWrapper.querySelectorAll('canvas');
            allCanvasElements.forEach(canvas => {
                canvas.style.pointerEvents = 'auto';
                console.log('Canvas element set to pointer-events: auto:', canvas.className);
            });
            
            // Ensure canvas container wrapper has pointer events enabled
            const canvasContainerDiv = canvasWrapper.querySelector('.canvas-container');
            if (canvasContainerDiv) {
                canvasContainerDiv.style.pointerEvents = 'auto';
                console.log('Canvas container div set to pointer-events: auto');
            }
        }

        console.log('Canvas elements styled - pointer events configured');
    }

    // Setup all event listeners
    function setupEventListeners() {
        // Mouse events for debugging and diagnostics
        fabricCanvas.on('mouse:down', function (options) {
            console.log('Canvas mouse down at:', options.pointer);
            
            // Handle eraser cursor update
            if (eraserActive) {
                const pointer = fabricCanvas.getPointer(options.e);
                updateEraserCursor(pointer.x, pointer.y);
            }
            
            // Log event if diagnostic logging is enabled
            if (diagnosticState.eventLoggingEnabled) {
                logMouseEvent('mouse:down', options);
            }
        });

        fabricCanvas.on('mouse:move', function (options) {
            // Handle eraser cursor movement
            if (eraserActive) {
                const pointer = fabricCanvas.getPointer(options.e);
                updateEraserCursor(pointer.x, pointer.y);
            }
            
            if (fabricCanvas.isDrawingMode && options.e.buttons === 1) {
                console.log('Drawing at:', options.pointer);
                
                // Log event if diagnostic logging is enabled
                if (diagnosticState.eventLoggingEnabled) {
                    logMouseEvent('mouse:move', options);
                }
            }
        });

        fabricCanvas.on('mouse:up', function (options) {
            // Log event if diagnostic logging is enabled
            if (diagnosticState.eventLoggingEnabled) {
                logMouseEvent('mouse:up', options);
            }
        });

        // Handle mouse leaving canvas - hide eraser cursor
        fabricCanvas.on('mouse:out', function (options) {
            if (eraserActive) {
                hideEraserCursor();
            }
        });

        // Handle mouse entering canvas - show eraser cursor again
        fabricCanvas.on('mouse:over', function (options) {
            if (eraserActive && options.pointer) {
                updateEraserCursor(options.pointer.x, options.pointer.y);
            }
        });

        // Save state after drawing - path:created fires when free drawing completes
        fabricCanvas.on('path:created', function (event) {
            console.log('Path created - saving state');

            if (!event.path) {
                saveState();
                return;
            }
            
            // If eraser is active, apply ACTUAL erasing with destination-out
            if (eraserActive) {
                // CRITICAL: This makes it actually ERASE instead of drawing
                event.path.globalCompositeOperation = 'destination-out';
                event.path.selectable = false;
                event.path.evented = false;
                event.path.eraserPath = true; // Mark as eraser path for identification
                event.path.isAnnotationStroke = true; // Common flag for all drawn strokes
                console.log('✓ Eraser path created with destination-out (actual erasing)');
            } else {
                // Normal pen/brush strokes should NOT be selectable or draggable
                event.path.selectable = false;
                event.path.evented = false;
                event.path.hasControls = false;
                event.path.hasBorders = false;
                event.path.lockMovementX = true;
                event.path.lockMovementY = true;
                event.path.isAnnotationStroke = true; // Common flag for all drawn strokes
                console.log('✓ Drawing path locked (non-selectable, non-draggable)');
            }
            
            // Performance monitoring for drawing operations
            if (diagnosticState.performanceMonitoringEnabled) {
                recordDrawingPerformance('path:created', event);
            }
            
            saveState();
        });

        // Save state after object modifications
        fabricCanvas.on('object:modified', function (event) {
            console.log('Object modified - saving state');
            
            // Performance monitoring for object modifications
            if (diagnosticState.performanceMonitoringEnabled) {
                recordDrawingPerformance('object:modified', event);
            }
            
            saveState();
        });

        // Handle text tool - create editable text box on click
        fabricCanvas.on('mouse:down', function (options) {
            if (canvasState.currentTool === 'text' && !fabricCanvas.isDrawingMode) {
                // Don't create text if clicking on existing object
                if (options.target) {
                    console.log('Clicked on existing object, not creating new text');
                    return;
                }
                
                const pointer = fabricCanvas.getPointer(options.e);
                console.log('Creating new text box at:', pointer);
                
                // Create text box
                const textObj = new fabric.IText('Type here...', {
                    left: pointer.x,
                    top: pointer.y,
                    fill: canvasState.currentColor,
                    fontSize: canvasState.fontSize,
                    fontWeight: canvasState.isBold ? 'bold' : 'normal',
                    fontStyle: canvasState.isItalic ? 'italic' : 'normal',
                    underline: canvasState.isUnderline,
                    fontFamily: 'Arial',
                    editable: true,
                    selectable: true
                });
                
                fabricCanvas.add(textObj);
                fabricCanvas.setActiveObject(textObj);
                
                // Auto-enter editing mode
                textObj.enterEditing();
                textObj.selectAll();
                
                console.log('Text box created and ready for editing');
                
                // Auto-switch to select tool when user finishes editing
                textObj.on('editing:exited', function() {
                    console.log('✓ Text editing finished, switching to select tool');
                    canvasState.currentTool = null;
                    updateToolMode(null);
                });
                
                saveState();
            }
        });

        // Enable text editing on double-click
        fabricCanvas.on('mouse:dblclick', function (options) {
            if (options.target && options.target.type === 'i-text') {
                console.log('Double-clicked text, entering edit mode');
                options.target.enterEditing();
                options.target.selectAll();
            }
        });

        // Save state after text editing
        fabricCanvas.on('text:changed', function (e) {
            console.log('Text changed, saving state');
            saveState();
        });

        // Update button states when text selection changes
        fabricCanvas.on('text:selection:changed', function (e) {
            const formatting = getCurrentTextFormatting();
            if (formatting) {
                console.log('Text selection changed, formatting:', formatting);
                // Trigger custom event for Blazor to listen
                window.dispatchEvent(new CustomEvent('text-formatting-changed', { 
                    detail: formatting 
                }));
            }
        });

        // Update button states when entering/exiting text editing
        fabricCanvas.on('text:editing:entered', function (e) {
            const formatting = getCurrentTextFormatting();
            if (formatting) {
                console.log('Entered text editing, formatting:', formatting);
                window.dispatchEvent(new CustomEvent('text-formatting-changed', { 
                    detail: formatting 
                }));
            }
        });

        fabricCanvas.on('selection:created', function (e) {
            if (e.selected && e.selected[0] && 
                (e.selected[0].type === 'i-text' || e.selected[0].type === 'text')) {
                const formatting = getCurrentTextFormatting();
                if (formatting) {
                    console.log('Text selected, formatting:', formatting);
                    window.dispatchEvent(new CustomEvent('text-formatting-changed', { 
                        detail: formatting 
                    }));
                }
            }
        });

        // Handle keyboard events for deleting objects
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Delete' || e.key === 'Backspace') {
                const activeObject = fabricCanvas.getActiveObject();
                if (activeObject && !activeObject.isEditing) {
                    console.log('Deleting object:', activeObject.type);
                    fabricCanvas.remove(activeObject);
                    fabricCanvas.renderAll();
                    saveState();
                    e.preventDefault();
                }
            }
        });
    }

    // Get current text formatting at cursor/selection
    function getCurrentTextFormatting() {
        const activeObject = fabricCanvas.getActiveObject();
        
        if (!activeObject || (activeObject.type !== 'i-text' && activeObject.type !== 'text')) {
            return null;
        }
        
        let formatting = {
            isBold: false,
            isItalic: false,
            isUnderline: false,
            color: '#000000',
            fontSize: 16
        };
        
        // If editing with selection, get styles of selected text
        if (activeObject.isEditing && activeObject.selectionStart !== activeObject.selectionEnd) {
            const styles = activeObject.getSelectionStyles(activeObject.selectionStart, activeObject.selectionEnd);
            
            // Check first character's style (or majority)
            if (styles && styles.length > 0) {
                const firstStyle = styles[0] || {};
                formatting.isBold = firstStyle.fontWeight === 'bold';
                formatting.isItalic = firstStyle.fontStyle === 'italic';
                formatting.isUnderline = !!firstStyle.underline;
                formatting.color = firstStyle.fill || activeObject.fill;
                formatting.fontSize = firstStyle.fontSize || activeObject.fontSize;
            }
        } else {
            // Get formatting of entire text object
            formatting.isBold = activeObject.fontWeight === 'bold';
            formatting.isItalic = activeObject.fontStyle === 'italic';
            formatting.isUnderline = !!activeObject.underline;
            formatting.color = activeObject.fill;
            formatting.fontSize = activeObject.fontSize;
        }
        
        return formatting;
    }

    // Update text styling - handles both partial selection and entire text
    function updateTextStyling() {
        if (!window.isCanvasReady()) {
            console.warn('Canvas not ready for text styling');
            return;
        }
        
        const activeObject = fabricCanvas.getActiveObject();
        console.log('updateTextStyling called, active object:', activeObject?.type);
        
        if (!activeObject) {
            console.log('No active object, skipping text styling');
            return;
        }
        
        // Check if it's a text object (IText type)
        if (activeObject.type !== 'i-text' && activeObject.type !== 'text') {
            console.log('Active object is not text, skipping');
            return;
        }
        
        const styles = {
            fontWeight: canvasState.isBold ? 'bold' : 'normal',
            fontStyle: canvasState.isItalic ? 'italic' : 'normal',
            underline: canvasState.isUnderline,
            fill: canvasState.currentColor,
            fontSize: canvasState.fontSize
        };
        
        console.log('Applying styles:', styles);
        console.log('Is editing:', activeObject.isEditing);
        console.log('Selection:', activeObject.selectionStart, '-', activeObject.selectionEnd);
        
        // Check if text is being edited and has a partial selection
        if (activeObject.isEditing && 
            activeObject.selectionStart !== undefined &&
            activeObject.selectionEnd !== undefined &&
            activeObject.selectionStart !== activeObject.selectionEnd) {
            
            // Apply styles to SELECTED portion only
            console.log('✓ Applying styles to selected text portion');
            activeObject.setSelectionStyles(styles, activeObject.selectionStart, activeObject.selectionEnd);
            
        } else {
            // Apply styles to ENTIRE text
            console.log('✓ Applying styles to entire text');
            activeObject.set(styles);
        }
        
        fabricCanvas.requestRenderAll();
        saveState();
        console.log('✓ Text styling completed');
    }

    // Debounced resize handler
    let resizeTimeout = null;
    const RESIZE_DEBOUNCE_MS = 100;

    function handleResize() {
        if (!window.isCanvasReady()) return;

        // Clear existing timeout
        if (resizeTimeout) {
            clearTimeout(resizeTimeout);
        }

        // Debounce resize to avoid excessive calls
        resizeTimeout = setTimeout(() => {
            console.log('Handling canvas resize...');
            
            const canvasWrapper = document.getElementById('canvasWrapper');
            const backgroundImage = document.getElementById('backgroundImage');
            
            if (!canvasWrapper) {
                console.warn('Canvas wrapper not found during resize');
                return;
            }

            try {
                // Get new dimensions from wrapper
                const rect = canvasWrapper.getBoundingClientRect();
                
                // Validate dimensions
                if (rect.width === 0 || rect.height === 0) {
                    console.warn('Canvas wrapper has zero dimensions during resize');
                    return;
                }

                console.log('Resizing canvas to:', rect.width, 'x', rect.height);

                // Store current zoom and viewport transform to preserve annotations
                const currentZoom = fabricCanvas.getZoom();
                const currentVpTransform = fabricCanvas.viewportTransform.slice();

                // Update canvas dimensions
                fabricCanvas.setDimensions({ 
                    width: rect.width, 
                    height: rect.height 
                });

                // Restore zoom and viewport transform
                fabricCanvas.setZoom(currentZoom);
                fabricCanvas.viewportTransform = currentVpTransform;

                // If background image exists, ensure it maintains aspect ratio
                if (backgroundImage && !backgroundImage.classList.contains('canvas-image-hidden')) {
                    // Background image aspect ratio is maintained by CSS
                    // Just ensure it's properly positioned
                    backgroundImage.style.maxWidth = '100%';
                    backgroundImage.style.maxHeight = '100%';
                    backgroundImage.style.objectFit = 'contain';
                }

                // Re-render canvas with preserved annotations
                fabricCanvas.renderAll();
                
                console.log('Canvas resized successfully');
            } catch (error) {
                console.error('Error during canvas resize:', error);
            }
        }, RESIZE_DEBOUNCE_MS);
    }

    // Save canvas state to history
    function saveState() {
        if (!fabricCanvas) {
            console.warn('Cannot save state: canvas not initialized');
            return;
        }

        try {
            // Temporarily hide eraser cursor from serialization
            const tempEraserCursor = eraserCursor;
            const cursorPosition = eraserCursor ? { left: eraserCursor.left, top: eraserCursor.top } : null;
            
            if (eraserCursor) {
                fabricCanvas.remove(eraserCursor);
                eraserCursor = null;
            }
            
            // Serialize canvas to JSON
            const canvasJSON = JSON.stringify(fabricCanvas.toJSON());
            
            // Restore eraser cursor if it was active
            if (tempEraserCursor && cursorPosition) {
                eraserCursor = tempEraserCursor;
                eraserCursor.set(cursorPosition);
                fabricCanvas.add(eraserCursor);
                eraserCursor.bringToFront();
                // Don't render here, let normal canvas cycle handle it
            }
            
            // If we're not at the end of history, truncate forward history
            if (canvasState.historyStep < canvasState.history.length - 1) {
                canvasState.history = canvasState.history.slice(0, canvasState.historyStep + 1);
                console.log('Truncated forward history');
            }
            
            // Add new state to history
            canvasState.history.push(canvasJSON);
            canvasState.historyStep = canvasState.history.length - 1;
            
            // Limit history size to prevent memory issues (50 items max)
            if (canvasState.history.length > canvasState.maxHistorySize) {
                const removeCount = canvasState.history.length - canvasState.maxHistorySize;
                canvasState.history.splice(0, removeCount);
                canvasState.historyStep -= removeCount;
                console.log(`History limit reached, removed ${removeCount} old states`);
            }
            
            console.log(`State saved - History: ${canvasState.historyStep + 1}/${canvasState.history.length}`);
        } catch (error) {
            console.error('Error saving canvas state:', error);
        }
    }

    // ==================== PIXEL-PERFECT ERASER IMPLEMENTATION ====================
    
    let eraserActive = false;
    let eraserCursor = null;
    let eraserUpdateQueued = false;
    
    function enableCustomEraser() {
        eraserActive = true;
        
        // Create visual eraser cursor
        createEraserCursor();
        
        console.log('Pixel-perfect eraser enabled');
    }
    
    function disableCustomEraser() {
        eraserActive = false;
        
        // Remove visual eraser cursor
        removeEraserCursor();
        
        console.log('Pixel-perfect eraser disabled');
    }
    
    function createEraserCursor() {
        if (!fabricCanvas) {
            console.warn('[ERASER] Cannot create cursor: canvas not initialized');
            return;
        }
        
        // Remove existing cursor if any
        removeEraserCursor();
        
        // Validate thickness
        const thickness = canvasState.lineThickness || 3;
        const eraserRadius = Math.max(1, thickness * 3 / 2); // Ensure minimum radius of 1
        
        try {
            eraserCursor = new fabric.Circle({
                radius: eraserRadius,
                fill: 'rgba(255, 255, 255, 0.8)', // White semi-transparent fill
                stroke: '#94a3b8', // Light gray border
                strokeWidth: 2,
                strokeDashArray: [5, 5],
                left: -1000,
                top: -1000,
                selectable: false,
                evented: false,
                excludeFromExport: true,
                hasControls: false,
                hasBorders: false,
                lockMovementX: true,
                lockMovementY: true,
                objectCaching: false, // Disable caching for smooth updates
                hoverCursor: 'none', // Don't change cursor on hover
                shadow: new fabric.Shadow({
                    color: 'rgba(0, 0, 0, 0.3)',
                    blur: 5,
                    offsetX: 0,
                    offsetY: 0
                })
            });
            
            fabricCanvas.add(eraserCursor);
            eraserCursor.bringToFront();
            fabricCanvas.requestRenderAll();
            
            console.log('[ERASER] Cursor created with radius:', eraserRadius);
        } catch (error) {
            console.error('[ERASER] Error creating cursor:', error);
            eraserCursor = null;
        }
    }
    
    function removeEraserCursor() {
        if (eraserCursor && fabricCanvas) {
            fabricCanvas.remove(eraserCursor);
            eraserCursor = null;
            fabricCanvas.requestRenderAll();
        }
    }
    
    function hideEraserCursor() {
        if (eraserCursor && fabricCanvas) {
            eraserCursor.set({
                left: -1000,
                top: -1000
            });
            eraserCursor.setCoords();
            fabricCanvas.requestRenderAll();
        }
    }
    
    function updateEraserCursor(x, y) {
        if (!eraserCursor || !fabricCanvas) return;
        
        // Update cursor position immediately (no render yet)
        eraserCursor.set({
            left: x - eraserCursor.radius,
            top: y - eraserCursor.radius
        });
        eraserCursor.setCoords();
        
        // Throttle rendering using requestAnimationFrame
        if (!eraserUpdateQueued) {
            eraserUpdateQueued = true;
            requestAnimationFrame(() => {
                if (fabricCanvas && eraserCursor) {
                    fabricCanvas.requestRenderAll();
                }
                eraserUpdateQueued = false;
            });
        }
    }

    // Update tool mode
    function updateToolMode(tool) {
        if (!window.isCanvasReady()) {
            console.warn('[TOOL] Canvas not ready for tool update');
            return false;
        }

        console.log('[TOOL] Updating tool mode to:', tool);
        canvasState.currentTool = tool;
        
        // Disable custom eraser when switching tools
        if (tool !== 'eraser') {
            disableCustomEraser();
        }

        if (tool === 'pencil' || tool === 'brush') {
            fabricCanvas.isDrawingMode = true;
            
            // Re-enable selection (in case coming from eraser)
            fabricCanvas.selection = true;
            
            fabricCanvas.freeDrawingBrush = new fabric.PencilBrush(fabricCanvas);
            fabricCanvas.freeDrawingBrush.color = canvasState.currentColor;
            
            // Set cursor to crosshair for drawing tools
            fabricCanvas.defaultCursor = 'crosshair';
            fabricCanvas.hoverCursor = 'crosshair';
            fabricCanvas.freeDrawingCursor = 'crosshair';

            if (tool === 'brush') {
                fabricCanvas.freeDrawingBrush.width = canvasState.lineThickness * 2;
                console.log('Brush mode activated, width:', canvasState.lineThickness * 2);
            } else {
                fabricCanvas.freeDrawingBrush.width = canvasState.lineThickness;
                console.log('Pencil mode activated, width:', canvasState.lineThickness);
            }
        } else if (tool === 'eraser') {
            // Enable drawing mode for pixel-perfect erasing
            fabricCanvas.isDrawingMode = true;
            
            // Disable selection to prevent selection box
            fabricCanvas.selection = false;
            
            // Create eraser brush with FULL OPACITY for effective erasing
            fabricCanvas.freeDrawingBrush = new fabric.PencilBrush(fabricCanvas);
            fabricCanvas.freeDrawingBrush.width = canvasState.lineThickness * 3;
            // Use FULL opacity (1.0) so erasing works in ONE PASS
            // Light gray for visual feedback, but 100% opacity for effective erasing
            fabricCanvas.freeDrawingBrush.color = 'rgba(220, 220, 220, 1.0)'; // Light gray, 100% opacity
            
            // Set cursor to crosshair for eraser
            fabricCanvas.defaultCursor = 'crosshair';
            fabricCanvas.hoverCursor = 'crosshair';
            fabricCanvas.freeDrawingCursor = 'crosshair';
            
            // Enable custom eraser mode (for visual cursor)
            enableCustomEraser();
            console.log('Eraser activated with FULL opacity for one-pass erasing, width:', canvasState.lineThickness * 3);
        } else if (tool === 'text') {
            fabricCanvas.isDrawingMode = false;
            
            // Re-enable selection (in case coming from eraser)
            fabricCanvas.selection = true;
            
            // Re-enable object selection ONLY for interactive objects (e.g., text),
            // not for free-drawn strokes or eraser cursor
            fabricCanvas.forEachObject(function(obj) {
                if (obj === eraserCursor || obj.isAnnotationStroke) {
                    // Keep strokes and eraser cursor non-selectable
                    obj.selectable = false;
                    obj.evented = false;
                } else {
                    obj.selectable = true;
                    obj.evented = true;
                }
            });
            
            fabricCanvas.defaultCursor = 'text';
            fabricCanvas.hoverCursor = 'text';
            console.log('Text mode activated');
        } else {
            fabricCanvas.isDrawingMode = false;
            
            // Re-enable selection (in case coming from eraser)
            fabricCanvas.selection = true;
            
            // Re-enable object selection ONLY for interactive objects (e.g., text),
            // not for free-drawn strokes or eraser cursor
            fabricCanvas.forEachObject(function(obj) {
                if (obj === eraserCursor || obj.isAnnotationStroke) {
                    obj.selectable = false;
                    obj.evented = false;
                } else {
                    obj.selectable = true;
                    obj.evented = true;
                }
            });
            
            fabricCanvas.defaultCursor = 'default';
            fabricCanvas.hoverCursor = 'move';
            console.log('Selection mode activated');
        }

        fabricCanvas.renderAll();
        return true;
    }

    // ==================== DIAGNOSTIC HELPER FUNCTIONS ====================

    // Log mouse events for debugging
    function logMouseEvent(eventType, options) {
        const eventData = {
            timestamp: Date.now(),
            type: eventType,
            pointer: options.pointer ? { x: options.pointer.x, y: options.pointer.y } : null,
            target: options.target ? options.target.type : null,
            button: options.e ? options.e.button : null,
            isDrawingMode: fabricCanvas.isDrawingMode,
            currentTool: canvasState.currentTool
        };

        diagnosticState.mouseEventLog.push(eventData);

        // Limit log size to prevent memory issues
        if (diagnosticState.mouseEventLog.length > diagnosticState.maxEventLogSize) {
            diagnosticState.mouseEventLog.shift();
        }

        console.log('[EVENT]', eventType, eventData);
    }

    // Record drawing performance metrics
    function recordDrawingPerformance(operationType, event) {
        const metric = {
            timestamp: Date.now(),
            operation: operationType,
            objectCount: fabricCanvas.getObjects().length,
            canvasSize: {
                width: fabricCanvas.width,
                height: fabricCanvas.height
            }
        };

        // Add specific details based on operation type
        if (event && event.path) {
            metric.pathComplexity = event.path.path ? event.path.path.length : 0;
        }

        diagnosticState.performanceMetrics.drawingOperations.push(metric);

        // Limit metrics size
        if (diagnosticState.performanceMetrics.drawingOperations.length > diagnosticState.performanceMetrics.maxMetricsSize) {
            diagnosticState.performanceMetrics.drawingOperations.shift();
        }

        console.log('[PERFORMANCE]', operationType, metric);
    }

    // Record general performance metrics
    function recordPerformanceMetric(operation, duration, additionalData = {}) {
        const metric = {
            timestamp: Date.now(),
            operation: operation,
            duration: duration,
            ...additionalData
        };

        diagnosticState.performanceMetrics.drawingOperations.push(metric);

        // Limit metrics size
        if (diagnosticState.performanceMetrics.drawingOperations.length > diagnosticState.performanceMetrics.maxMetricsSize) {
            diagnosticState.performanceMetrics.drawingOperations.shift();
        }

        console.log('[PERFORMANCE]', operation, `${duration.toFixed(2)}ms`, additionalData);
    }

    // ==================== GLOBAL API FUNCTIONS ====================

    // Undo - navigate backward in history stack
    window.annotationUndo = function () {
        console.log('[UNDO] Undo operation requested');
        
        if (!window.isCanvasReady()) {
            console.warn('[UNDO] Cannot undo: canvas not ready');
            return false;
        }

        // Check if we can undo (must have at least 2 states and not at the beginning)
        if (canvasState.history.length < 2 || canvasState.historyStep <= 0) {
            console.log('[UNDO] Cannot undo: at beginning of history');
            console.log('[UNDO] History state:', {
                historyLength: canvasState.history.length,
                currentStep: canvasState.historyStep
            });
            return false;
        }

        try {
            // Move back one step in history
            canvasState.historyStep--;
            const previousState = canvasState.history[canvasState.historyStep];
            
            console.log(`[UNDO] Loading state ${canvasState.historyStep + 1}/${canvasState.history.length}`);
            
            // Save eraser cursor state
            const wasEraserActive = eraserActive;
            
            // Load previous state without triggering save
            fabricCanvas.loadFromJSON(previousState, function () {
                // Recreate eraser cursor if it was active
                if (wasEraserActive) {
                    createEraserCursor();
                    
                    // Make sure objects are non-selectable for eraser mode
                    fabricCanvas.forEachObject(function(obj) {
                        if (obj !== eraserCursor) {
                            obj.selectable = false;
                            obj.evented = false;
                        }
                    });
                }
                
                fabricCanvas.renderAll();
                console.log('[UNDO] ✓ Undo completed successfully');
            });
            
            return true;
        } catch (error) {
            console.error('[ERROR] Error during undo:', error);
            console.error('[ERROR] Error details:', {
                name: error.name,
                message: error.message,
                stack: error.stack
            });
            return false;
        }
    };

    // Redo - navigate forward in history stack
    window.annotationRedo = function () {
        console.log('[REDO] Redo operation requested');
        
        if (!window.isCanvasReady()) {
            console.warn('[REDO] Cannot redo: canvas not ready');
            return false;
        }

        // Check if we can redo (must not be at the end of history)
        if (canvasState.historyStep >= canvasState.history.length - 1) {
            console.log('[REDO] Cannot redo: at end of history');
            console.log('[REDO] History state:', {
                historyLength: canvasState.history.length,
                currentStep: canvasState.historyStep
            });
            return false;
        }

        try {
            // Move forward one step in history
            canvasState.historyStep++;
            const nextState = canvasState.history[canvasState.historyStep];
            
            console.log(`[REDO] Loading state ${canvasState.historyStep + 1}/${canvasState.history.length}`);
            
            // Save eraser cursor state
            const wasEraserActive = eraserActive;
            
            // Load next state without triggering save
            fabricCanvas.loadFromJSON(nextState, function () {
                // Recreate eraser cursor if it was active
                if (wasEraserActive) {
                    createEraserCursor();
                    
                    // Make sure objects are non-selectable for eraser mode
                    fabricCanvas.forEachObject(function(obj) {
                        if (obj !== eraserCursor) {
                            obj.selectable = false;
                            obj.evented = false;
                        }
                    });
                }
                
                fabricCanvas.renderAll();
                console.log('[REDO] ✓ Redo completed successfully');
            });
            
            return true;
        } catch (error) {
            console.error('[ERROR] Error during redo:', error);
            console.error('[ERROR] Error details:', {
                name: error.name,
                message: error.message,
                stack: error.stack
            });
            return false;
        }
    };

    // Reset canvas - clear all annotations (but keep history so it's undoable!)
    // Export annotated canvas as base64 image
    window.exportAnnotatedCanvas = function () {
        console.log('[EXPORT] Export annotated canvas requested');
        
        if (!fabricCanvas) {
            console.error('[EXPORT] Canvas not initialized');
            return null;
        }
        
        if (!window.isCanvasReady()) {
            console.warn('[EXPORT] Canvas not ready');
            return null;
        }
        
        try {
            // Get canvas dimensions
            const canvasWidth = fabricCanvas.getWidth();
            const canvasHeight = fabricCanvas.getHeight();
            
            // Calculate multiplier to limit max width to 1200px for reasonable file size
            let multiplier = 1;
            const maxWidth = 1200;
            
            if (canvasWidth > maxWidth) {
                multiplier = maxWidth / canvasWidth;
                console.log(`[EXPORT] Scaling down canvas from ${canvasWidth}px to ${maxWidth}px (multiplier: ${multiplier.toFixed(2)})`);
            }
            
            // Check for before-after mode first
            const beforeImage = document.getElementById('beforeImage');
            const afterImage = document.getElementById('afterImage');
            
            // If both before and after images exist, handle before-after mode
            if (beforeImage && beforeImage.src && afterImage && afterImage.src) {
                console.log('[EXPORT] Before/After images found, combining with annotations...');
                
                // Return a Promise since image loading is async
                return new Promise((resolve, reject) => {
                    // Create a temporary canvas to combine both images + annotations
                    const tempCanvas = document.createElement('canvas');
                    const tempCtx = tempCanvas.getContext('2d');
                    const exportWidth = Math.round(canvasWidth * multiplier);
                    const exportHeight = Math.round(canvasHeight * multiplier);
                    tempCanvas.width = exportWidth;
                    tempCanvas.height = exportHeight;
                    
                    // Load both images
                    let beforeImgLoaded = false;
                    let afterImgLoaded = false;
                    const beforeImg = new Image();
                    const afterImg = new Image();
                    beforeImg.crossOrigin = 'anonymous';
                    afterImg.crossOrigin = 'anonymous';
                    
                    const tryExport = function() {
                        if (!beforeImgLoaded || !afterImgLoaded) return;
                        
                        try {
                            // Draw white background first
                            tempCtx.fillStyle = '#ffffff';
                            tempCtx.fillRect(0, 0, exportWidth, exportHeight);
                            
                            // Each image takes exactly half the width
                            const halfWidth = exportWidth / 2;
                            
                            // Calculate dimensions for before image (left side)
                            // Use object-fit: cover behavior - fill entire space, crop if needed
                            const beforeAspect = beforeImg.width / beforeImg.height;
                            const targetAspect = halfWidth / exportHeight;
                            
                            let beforeDrawWidth, beforeDrawHeight, beforeDrawX, beforeDrawY;
                            
                            if (beforeAspect > targetAspect) {
                                // Image is wider - fit to height, crop sides
                                beforeDrawHeight = exportHeight;
                                beforeDrawWidth = exportHeight * beforeAspect;
                                beforeDrawX = -(beforeDrawWidth - halfWidth) / 2; // Center horizontally (negative = crop left/right)
                                beforeDrawY = 0;
                            } else {
                                // Image is taller - fit to width, crop top/bottom
                                beforeDrawWidth = halfWidth;
                                beforeDrawHeight = halfWidth / beforeAspect;
                                beforeDrawX = 0;
                                beforeDrawY = -(beforeDrawHeight - exportHeight) / 2; // Center vertically (negative = crop top/bottom)
                            }
                            
                            // Calculate dimensions for after image (right side)
                            // Use object-fit: cover behavior - fill entire space, crop if needed
                            const afterAspect = afterImg.width / afterImg.height;
                            
                            let afterDrawWidth, afterDrawHeight, afterDrawX, afterDrawY;
                            
                            if (afterAspect > targetAspect) {
                                // Image is wider - fit to height, crop sides
                                afterDrawHeight = exportHeight;
                                afterDrawWidth = exportHeight * afterAspect;
                                afterDrawX = halfWidth - (afterDrawWidth - halfWidth) / 2; // Center horizontally in right half
                                afterDrawY = 0;
                            } else {
                                // Image is taller - fit to width, crop top/bottom
                                afterDrawWidth = halfWidth;
                                afterDrawHeight = halfWidth / afterAspect;
                                afterDrawX = halfWidth;
                                afterDrawY = -(afterDrawHeight - exportHeight) / 2; // Center vertically
                            }
                            
                            // Save context and clip left half for before image
                            tempCtx.save();
                            tempCtx.rect(0, 0, halfWidth, exportHeight);
                            tempCtx.clip();
                            tempCtx.drawImage(beforeImg, beforeDrawX, beforeDrawY, beforeDrawWidth, beforeDrawHeight);
                            tempCtx.restore();
                            console.log(`[EXPORT] Before image drawn: ${beforeDrawWidth}x${beforeDrawHeight} at (${beforeDrawX}, ${beforeDrawY})`);
                            
                            // Save context and clip right half for after image
                            tempCtx.save();
                            tempCtx.rect(halfWidth, 0, halfWidth, exportHeight);
                            tempCtx.clip();
                            tempCtx.drawImage(afterImg, afterDrawX, afterDrawY, afterDrawWidth, afterDrawHeight);
                            tempCtx.restore();
                            console.log(`[EXPORT] After image drawn: ${afterDrawWidth}x${afterDrawHeight} at (${afterDrawX}, ${afterDrawY})`);
                            
                            // Export annotations from Fabric canvas
                            const annotationDataURL = fabricCanvas.toDataURL({
                                format: 'png',
                                quality: 1.0,
                                multiplier: multiplier
                            });
                            
                            // Load annotation canvas and draw it on top
                            const annotationImg = new Image();
                            annotationImg.onload = function() {
                                // Draw annotations on top of both images
                                tempCtx.drawImage(annotationImg, 0, 0, exportWidth, exportHeight);
                                
                                // Export combined canvas
                                const base64Image = tempCanvas.toDataURL('image/png', 0.9);
                                
                                const sizeKB = Math.round(base64Image.length / 1024);
                                console.log(`[EXPORT] Before/After canvas exported successfully (${sizeKB} KB)`);
                                
                                if (sizeKB > 3000) {
                                    console.warn(`[EXPORT] Large image size: ${sizeKB} KB - may affect performance`);
                                }
                                
                                resolve(base64Image);
                            };
                            
                            annotationImg.onerror = function() {
                                console.error('[EXPORT] Error loading annotation canvas, exporting images only');
                                const fallbackExport = tempCanvas.toDataURL('image/png', 0.9);
                                resolve(fallbackExport);
                            };
                            
                            annotationImg.src = annotationDataURL;
                        } catch (error) {
                            console.error('[EXPORT] Error combining before/after images:', error);
                            const fallbackExport = fabricCanvas.toDataURL({
                                format: 'png',
                                quality: 0.9,
                                multiplier: multiplier
                            });
                            resolve(fallbackExport);
                        }
                    };
                    
                    beforeImg.onload = function() {
                        beforeImgLoaded = true;
                        tryExport();
                    };
                    
                    beforeImg.onerror = function() {
                        console.warn('[EXPORT] Before image failed to load');
                        beforeImgLoaded = true;
                        tryExport();
                    };
                    
                    afterImg.onload = function() {
                        afterImgLoaded = true;
                        tryExport();
                    };
                    
                    afterImg.onerror = function() {
                        console.warn('[EXPORT] After image failed to load');
                        afterImgLoaded = true;
                        tryExport();
                    };
                    
                    beforeImg.src = beforeImage.src;
                    afterImg.src = afterImage.src;
                });
            }
            
            // Get background image element (for standard single image mode)
            const backgroundImage = document.getElementById('backgroundImage');
            
            // If background image exists and is visible, combine it with annotations
            if (backgroundImage && backgroundImage.src && !backgroundImage.classList.contains('canvas-image-hidden')) {
                console.log('[EXPORT] Background image found, combining with annotations...');
                
                // Return a Promise since image loading is async
                return new Promise((resolve, reject) => {
                    // Create a temporary canvas to combine background + annotations
                    const tempCanvas = document.createElement('canvas');
                    const tempCtx = tempCanvas.getContext('2d');
                    const exportWidth = Math.round(canvasWidth * multiplier);
                    const exportHeight = Math.round(canvasHeight * multiplier);
                    tempCanvas.width = exportWidth;
                    tempCanvas.height = exportHeight;
                    
                    // Create image object to ensure it's fully loaded
                    const img = new Image();
                    img.crossOrigin = 'anonymous';
                    
                    img.onload = function() {
                        try {
                            // Calculate how to draw the background image to match the display
                            // The background image should fill the canvas while maintaining aspect ratio
                            const imgAspect = img.width / img.height;
                            const canvasAspect = canvasWidth / canvasHeight;
                            
                            let drawWidth = exportWidth;
                            let drawHeight = exportHeight;
                            let drawX = 0;
                            let drawY = 0;
                            
                            // Maintain aspect ratio of background image (same as CSS object-fit: contain)
                            if (imgAspect > canvasAspect) {
                                // Image is wider - fit to height
                                drawHeight = exportHeight;
                                drawWidth = exportHeight * imgAspect;
                                drawX = (exportWidth - drawWidth) / 2;
                            } else {
                                // Image is taller - fit to width
                                drawWidth = exportWidth;
                                drawHeight = exportWidth / imgAspect;
                                drawY = (exportHeight - drawHeight) / 2;
                            }
                            
                            // Draw white/transparent background first (for proper rendering)
                            tempCtx.fillStyle = '#ffffff';
                            tempCtx.fillRect(0, 0, exportWidth, exportHeight);
                            
                            // Draw background image
                            tempCtx.drawImage(img, drawX, drawY, drawWidth, drawHeight);
                            console.log('[EXPORT] Background image drawn to export canvas');
                            
                            // Export annotations from Fabric canvas
                            const annotationDataURL = fabricCanvas.toDataURL({
                                format: 'png',
                                quality: 1.0, // Full quality for annotations
                                multiplier: multiplier
                            });
                            
                            // Load annotation canvas and draw it on top
                            const annotationImg = new Image();
                            annotationImg.onload = function() {
                                // Draw annotations on top of background
                                tempCtx.drawImage(annotationImg, 0, 0, exportWidth, exportHeight);
                                
                                // Export combined canvas
                                const base64Image = tempCanvas.toDataURL('image/png', 0.9);
                                
                                const sizeKB = Math.round(base64Image.length / 1024);
                                console.log(`[EXPORT] Canvas exported successfully with background image (${sizeKB} KB)`);
                                
                                if (sizeKB > 3000) {
                                    console.warn(`[EXPORT] Large image size: ${sizeKB} KB - may affect performance`);
                                }
                                
                                resolve(base64Image);
                            };
                            
                            annotationImg.onerror = function() {
                                console.error('[EXPORT] Error loading annotation canvas, exporting background only');
                                // Fallback: export background image only
                                const fallbackExport = tempCanvas.toDataURL('image/png', 0.9);
                                resolve(fallbackExport);
                            };
                            
                            annotationImg.src = annotationDataURL;
                        } catch (error) {
                            console.error('[EXPORT] Error combining images:', error);
                            // Fallback: export annotations only
                            const fallbackExport = fabricCanvas.toDataURL({
                                format: 'png',
                                quality: 0.9,
                                multiplier: multiplier
                            });
                            resolve(fallbackExport);
                        }
                    };
                    
                    img.onerror = function() {
                        console.warn('[EXPORT] Background image failed to load, exporting annotations only');
                        // Fallback: export annotations only
                        const fallbackExport = fabricCanvas.toDataURL({
                            format: 'png',
                            quality: 0.9,
                            multiplier: multiplier
                        });
                        resolve(fallbackExport);
                    };
                    
                    img.src = backgroundImage.src;
                });
            } else {
                // No background image or it's hidden - export annotations only
                console.log('[EXPORT] No background image found, exporting annotations only');
                // Wrap in Promise for consistency
                return new Promise((resolve) => {
                    const base64Image = fabricCanvas.toDataURL({
                        format: 'png',
                        quality: 0.9,
                        multiplier: multiplier
                    });
                    
                    const sizeKB = Math.round(base64Image.length / 1024);
                    console.log(`[EXPORT] Canvas exported successfully (${sizeKB} KB)`);
                    
                    if (sizeKB > 3000) {
                        console.warn(`[EXPORT] Large image size: ${sizeKB} KB - may affect performance`);
                    }
                    
                    resolve(base64Image);
                });
            }
        } catch (error) {
            console.error('[EXPORT] Error exporting canvas:', error);
            // Return rejected Promise for proper error handling
            return Promise.reject(error);
        }
    };

    window.annotationReset = function () {
        console.log('[RESET] Reset operation requested');
        
        if (!window.isCanvasReady()) {
            console.warn('[RESET] Cannot reset: canvas not ready');
            return false;
        }

        try {
            console.log('[RESET] Clearing all annotations (undoable reset)');
            
            // Remove eraser cursor before clearing
            const wasEraserActive = eraserActive;
            const tempEraserCursor = eraserCursor;
            if (eraserCursor) {
                fabricCanvas.remove(eraserCursor);
                eraserCursor = null;
            }
            
            // Clear all objects from canvas
            fabricCanvas.clear();
            
            // Recreate eraser cursor if it was active
            if (wasEraserActive) {
                createEraserCursor();
                // Hide it off-screen initially
                hideEraserCursor();
            }
            
            // DON'T reset history - just save the empty state
            // This makes Reset undoable with Ctrl+Z!
            saveState();
            
            // Force render
            fabricCanvas.requestRenderAll();
            
            console.log('[RESET] ✓ Canvas reset completed successfully (can be undone)');
            return true;
        } catch (error) {
            console.error('[ERROR] Error during reset:', error);
            console.error('[ERROR] Error details:', {
                name: error.name,
                message: error.message,
                stack: error.stack
            });
            return false;
        }
    };

    // Check if undo is available
    window.canUndo = function () {
        return window.isCanvasReady() && 
               canvasState.history.length >= 2 && 
               canvasState.historyStep > 0;
    };

    // Check if redo is available
    window.canRedo = function () {
        return window.isCanvasReady() && 
               canvasState.historyStep < canvasState.history.length - 1;
    };

    // Get history info for debugging
    window.getHistoryInfo = function () {
        return {
            historyLength: canvasState.history.length,
            currentStep: canvasState.historyStep,
            canUndo: window.canUndo(),
            canRedo: window.canRedo(),
            maxHistorySize: canvasState.maxHistorySize
        };
    };

    // Update tool
    window.setAnnotationTool = function (tool) {
        console.log('[API] setAnnotationTool called with:', tool);
        
        // Validate canvas is ready before tool activation
        if (!window.isCanvasReady()) {
            console.warn('[API] Canvas not ready, cannot activate tool:', tool);
            console.warn('[API] Storing tool for later activation');
            // Store the tool for later activation
            canvasState.currentTool = tool;
            return false;
        }
        
        // Update tool state
        canvasState.currentTool = tool;
        
        // Activate the tool immediately
        const success = updateToolMode(tool);
        
        if (success) {
            console.log('[API] ✓ Tool activated successfully:', tool);
        } else {
            console.error('[ERROR] ✗ Failed to activate tool:', tool);
        }
        
        return success;
    };

    // Update color - applies immediately to brush AND selected text
    window.setAnnotationColor = function (color) {
        console.log('[API] Setting annotation color to:', color);
        canvasState.currentColor = color;
        
        if (window.isCanvasReady()) {
            // Update brush color if drawing (but NOT for eraser - eraser uses fixed light gray)
            if (fabricCanvas.freeDrawingBrush && !eraserActive) {
                fabricCanvas.freeDrawingBrush.color = color;
                console.log('[API] ✓ Brush color updated to:', color);
            } else if (eraserActive) {
                // Eraser is active, keep the light gray color
                console.log('[API] Eraser is active, keeping light gray brush color');
            }
            
            // Update selected text color
            updateTextStyling();
        } else {
            console.warn('[API] Canvas not ready, color stored for later');
        }
    };

    // Update thickness - applies immediately with correct multiplier
    window.setAnnotationThickness = function (thickness) {
        console.log('Setting annotation thickness to:', thickness);
        canvasState.lineThickness = thickness;
        
        if (window.isCanvasReady()) {
            // Update brush width if in drawing mode
            if (fabricCanvas.freeDrawingBrush) {
                // Apply correct multiplier based on current tool
                if (canvasState.currentTool === 'brush') {
                    fabricCanvas.freeDrawingBrush.width = thickness * 2;
                    console.log('Brush width updated to:', thickness * 2, '(thickness * 2)');
                } else if (canvasState.currentTool === 'eraser') {
                    fabricCanvas.freeDrawingBrush.width = thickness * 3;
                    console.log('Eraser width updated to:', thickness * 3, '(thickness * 3)');
                } else {
                    fabricCanvas.freeDrawingBrush.width = thickness;
                    console.log('Pencil width updated to:', thickness);
                }
            }
            
            // Update eraser cursor size if eraser is active
            if (eraserActive) {
                createEraserCursor(); // Recreate with new size
            }
            
            // Force canvas re-render to apply changes
            fabricCanvas.renderAll();
        }
    };

    // Update font size - applies immediately to selected text
    window.setAnnotationFontSize = function (size) {
        console.log('[API] Setting annotation font size to:', size);
        canvasState.fontSize = size;
        
        // Apply to selected text immediately
        if (window.isCanvasReady()) {
            updateTextStyling();
        }
    };

    // Get current text formatting (for updating button states)
    window.getCurrentTextFormatting = function () {
        return getCurrentTextFormatting();
    };

    // Setup listener for text formatting changes and connect to Blazor
    window.setupTextFormattingListener = function (dotNetRef) {
        console.log('✓ Setting up text formatting listener with Blazor callback');
        
        window.addEventListener('text-formatting-changed', function(e) {
            const formatting = e.detail;
            console.log('📝 Formatting changed, notifying Blazor:', formatting);
            
            try {
                dotNetRef.invokeMethodAsync('UpdateTextFormattingState', 
                    formatting.isBold, 
                    formatting.isItalic, 
                    formatting.isUnderline
                );
            } catch (error) {
                console.error('Error calling Blazor method:', error);
            }
        });
    };

    // Setup canvas change listener for tracking unsaved changes
    window.setupCanvasChangeListener = function (dotNetRef) {
        console.log('✓ Setting up canvas change listener with Blazor callback');
        
        if (!fabricCanvas) {
            console.warn('Canvas not initialized yet for change listener');
            return;
        }
        
        // Track when canvas content changes
        fabricCanvas.on('object:added', function() {
            try {
                dotNetRef.invokeMethodAsync('OnCanvasContentChanged');
            } catch (error) {
                console.error('Error calling Blazor OnCanvasContentChanged:', error);
            }
        });
        
        fabricCanvas.on('object:modified', function() {
            try {
                dotNetRef.invokeMethodAsync('OnCanvasContentChanged');
            } catch (error) {
                console.error('Error calling Blazor OnCanvasContentChanged:', error);
            }
        });
        
        fabricCanvas.on('path:created', function() {
            try {
                dotNetRef.invokeMethodAsync('OnCanvasContentChanged');
            } catch (error) {
                console.error('Error calling Blazor OnCanvasContentChanged:', error);
            }
        });
        
        fabricCanvas.on('object:removed', function() {
            try {
                dotNetRef.invokeMethodAsync('OnCanvasContentChanged');
            } catch (error) {
                console.error('Error calling Blazor OnCanvasContentChanged:', error);
            }
        });
        
        console.log('✓ Canvas change listener setup complete');
    };

    // Update text styles - now reads current formatting first for toggle behavior
    window.setAnnotationBold = function (bold) {
        canvasState.isBold = bold;
        updateTextStyling();
    };

    window.setAnnotationItalic = function (italic) {
        canvasState.isItalic = italic;
        updateTextStyling();
    };

    window.setAnnotationUnderline = function (underline) {
        canvasState.isUnderline = underline;
        updateTextStyling();
    };

    // Trigger canvas resize manually (for sidebar resize, etc.)
    window.resizeFabricCanvas = function () {
        console.log('Manual canvas resize triggered');
        handleResize();
    };

    // Zoom controls
    window.zoomIn = function () {
        if (!window.isCanvasReady()) return;
        const currentZoom = fabricCanvas.getZoom();
        const newZoom = Math.min(currentZoom * 1.1, 3); // Max 3x zoom
        fabricCanvas.setZoom(newZoom);
        fabricCanvas.renderAll();
    };

    window.zoomOut = function () {
        if (!window.isCanvasReady()) return;
        const currentZoom = fabricCanvas.getZoom();
        const newZoom = Math.max(currentZoom / 1.1, 0.5); // Min 0.5x zoom
        fabricCanvas.setZoom(newZoom);
        fabricCanvas.renderAll();
    };

    window.resetZoom = function () {
        if (!window.isCanvasReady()) return;
        fabricCanvas.setZoom(1);
        fabricCanvas.viewportTransform = [1, 0, 0, 1, 0, 0];
        fabricCanvas.renderAll();
    };

    // Mouse wheel zoom
    if (fabricCanvas) {
        fabricCanvas.on('mouse:wheel', function(opt) {
            const delta = opt.e.deltaY;
            let zoom = fabricCanvas.getZoom();
            zoom *= 0.999 ** delta;
            if (zoom > 3) zoom = 3;
            if (zoom < 0.5) zoom = 0.5;
            fabricCanvas.setZoom(zoom);
            opt.e.preventDefault();
            opt.e.stopPropagation();
        });
    }

    // ==================== DEBUGGING AND DIAGNOSTIC API ====================

    // Get complete canvas state for debugging
    window.getCanvasState = function () {
        const state = {
            // Canvas initialization state
            initialization: {
                isInitialized: canvasState.isInitialized,
                isReady: canvasState.isReady,
                canvasExists: fabricCanvas !== null
            },
            
            // Current tool and settings
            currentSettings: {
                tool: canvasState.currentTool,
                color: canvasState.currentColor,
                lineThickness: canvasState.lineThickness,
                fontSize: canvasState.fontSize,
                isBold: canvasState.isBold,
                isItalic: canvasState.isItalic,
                isUnderline: canvasState.isUnderline
            },
            
            // Canvas dimensions and objects
            canvas: fabricCanvas ? {
                width: fabricCanvas.width,
                height: fabricCanvas.height,
                objectCount: fabricCanvas.getObjects().length,
                isDrawingMode: fabricCanvas.isDrawingMode,
                zoom: fabricCanvas.getZoom()
            } : null,
            
            // History state
            history: {
                length: canvasState.history.length,
                currentStep: canvasState.historyStep,
                canUndo: window.canUndo(),
                canRedo: window.canRedo(),
                maxSize: canvasState.maxHistorySize
            },
            
            // Diagnostic state
            diagnostics: {
                eventLoggingEnabled: diagnosticState.eventLoggingEnabled,
                performanceMonitoringEnabled: diagnosticState.performanceMonitoringEnabled,
                eventLogSize: diagnosticState.mouseEventLog.length,
                performanceMetricsSize: diagnosticState.performanceMetrics.drawingOperations.length
            }
        };

        console.log('[DEBUG] Canvas State:', state);
        return state;
    };

    // Enable/disable event logging
    window.setEventLogging = function (enabled) {
        diagnosticState.eventLoggingEnabled = enabled;
        console.log('[DEBUG] Event logging', enabled ? 'enabled' : 'disabled');
        
        if (!enabled) {
            // Clear event log when disabling
            diagnosticState.mouseEventLog = [];
            console.log('[DEBUG] Event log cleared');
        }
        
        return diagnosticState.eventLoggingEnabled;
    };

    // Get mouse event log
    window.getEventLog = function (limit = null) {
        const log = limit ? 
            diagnosticState.mouseEventLog.slice(-limit) : 
            diagnosticState.mouseEventLog;
        
        console.log('[DEBUG] Event Log:', log);
        return log;
    };

    // Clear event log
    window.clearEventLog = function () {
        diagnosticState.mouseEventLog = [];
        console.log('[DEBUG] Event log cleared');
        return true;
    };

    // Enable/disable performance monitoring
    window.setPerformanceMonitoring = function (enabled) {
        diagnosticState.performanceMonitoringEnabled = enabled;
        console.log('[DEBUG] Performance monitoring', enabled ? 'enabled' : 'disabled');
        
        if (!enabled) {
            // Clear performance metrics when disabling
            diagnosticState.performanceMetrics.drawingOperations = [];
            console.log('[DEBUG] Performance metrics cleared');
        }
        
        return diagnosticState.performanceMonitoringEnabled;
    };

    // Get performance metrics
    window.getPerformanceMetrics = function (limit = null) {
        const metrics = limit ? 
            diagnosticState.performanceMetrics.drawingOperations.slice(-limit) : 
            diagnosticState.performanceMetrics.drawingOperations;
        
        // Calculate statistics
        const stats = {
            totalOperations: metrics.length,
            operations: metrics
        };

        // Calculate average duration for operations with duration
        const durationsOnly = metrics.filter(m => m.duration !== undefined);
        if (durationsOnly.length > 0) {
            const totalDuration = durationsOnly.reduce((sum, m) => sum + m.duration, 0);
            stats.averageDuration = totalDuration / durationsOnly.length;
            stats.minDuration = Math.min(...durationsOnly.map(m => m.duration));
            stats.maxDuration = Math.max(...durationsOnly.map(m => m.duration));
        }

        console.log('[DEBUG] Performance Metrics:', stats);
        return stats;
    };

    // Clear performance metrics
    window.clearPerformanceMetrics = function () {
        diagnosticState.performanceMetrics.drawingOperations = [];
        console.log('[DEBUG] Performance metrics cleared');
        return true;
    };

    // Get canvas objects information for debugging
    window.getCanvasObjects = function () {
        if (!window.isCanvasReady()) {
            console.warn('[DEBUG] Canvas not ready');
            return null;
        }

        const objects = fabricCanvas.getObjects().map((obj, index) => ({
            index: index,
            type: obj.type,
            left: obj.left,
            top: obj.top,
            width: obj.width,
            height: obj.height,
            fill: obj.fill,
            stroke: obj.stroke,
            strokeWidth: obj.strokeWidth
        }));

        console.log('[DEBUG] Canvas Objects:', objects);
        return objects;
    };

    // Run diagnostic check
    window.runDiagnostics = function () {
        console.log('========================================');
        console.log('CANVAS DIAGNOSTICS REPORT');
        console.log('========================================');
        
        const state = window.getCanvasState();
        
        console.log('\n1. INITIALIZATION STATUS:');
        console.log('   - Initialized:', state.initialization.isInitialized);
        console.log('   - Ready:', state.initialization.isReady);
        console.log('   - Canvas exists:', state.initialization.canvasExists);
        
        console.log('\n2. CURRENT SETTINGS:');
        console.log('   - Tool:', state.currentSettings.tool);
        console.log('   - Color:', state.currentSettings.color);
        console.log('   - Line thickness:', state.currentSettings.lineThickness);
        console.log('   - Font size:', state.currentSettings.fontSize);
        
        if (state.canvas) {
            console.log('\n3. CANVAS STATE:');
            console.log('   - Dimensions:', state.canvas.width, 'x', state.canvas.height);
            console.log('   - Object count:', state.canvas.objectCount);
            console.log('   - Drawing mode:', state.canvas.isDrawingMode);
            console.log('   - Zoom:', state.canvas.zoom);
        }
        
        console.log('\n4. HISTORY STATE:');
        console.log('   - History length:', state.history.length);
        console.log('   - Current step:', state.history.currentStep);
        console.log('   - Can undo:', state.history.canUndo);
        console.log('   - Can redo:', state.history.canRedo);
        
        console.log('\n5. DOM ELEMENTS:');
        const canvasElement = document.getElementById('drawingCanvas');
        const canvasWrapper = document.getElementById('canvasWrapper');
        console.log('   - Canvas element:', canvasElement ? 'found' : 'missing');
        console.log('   - Canvas wrapper:', canvasWrapper ? 'found' : 'missing');
        
        if (fabricCanvas) {
            console.log('\n6. FABRIC.JS ELEMENTS:');
            console.log('   - Lower canvas:', fabricCanvas.lowerCanvasEl ? 'exists' : 'missing');
            console.log('   - Upper canvas:', fabricCanvas.upperCanvasEl ? 'exists' : 'missing');
            console.log('   - Wrapper element:', fabricCanvas.wrapperEl ? 'exists' : 'missing');
            
            if (fabricCanvas.upperCanvasEl) {
                const upperStyle = window.getComputedStyle(fabricCanvas.upperCanvasEl);
                console.log('   - Upper canvas pointer-events:', upperStyle.pointerEvents);
                console.log('   - Upper canvas z-index:', upperStyle.zIndex);
            }
        }
        
        console.log('\n========================================');
        console.log('END DIAGNOSTICS REPORT');
        console.log('========================================\n');
        
        return state;
    };

    // Clean up accumulated eraser paths periodically
    window.cleanupEraserPaths = function() {
        if (!window.isCanvasReady()) return;
        
        const objects = fabricCanvas.getObjects();
        let eraserPathCount = 0;
        
        // Count eraser paths
        objects.forEach(obj => {
            if (obj.eraserPath === true) {
                eraserPathCount++;
            }
        });
        
        console.log(`[CLEANUP] Found ${eraserPathCount} eraser path objects`);
        
        // If too many eraser paths, consider consolidating (optional)
        // This is a safety measure to prevent memory bloat
        if (eraserPathCount > 100) {
            console.warn('[CLEANUP] High number of eraser paths detected. Consider resetting canvas.');
        }
        
        return eraserPathCount;
    };

    // Dispose canvas and cleanup resources
    window.disposeFabricCanvas = function () {
        console.log('[DISPOSE] Disposing Fabric.js canvas');

        if (fabricCanvas) {
            try {
                console.log('[DISPOSE] Cleaning up canvas resources...');
                
                // Remove event listeners
                window.removeEventListener('resize', handleResize);

                // Clear resize timeout if exists
                if (resizeTimeout) {
                    clearTimeout(resizeTimeout);
                    resizeTimeout = null;
                }
                
                // Clean up eraser state
                if (eraserCursor) {
                    eraserCursor = null;
                }
                eraserActive = false;
                eraserUpdateQueued = false;

                // Clear all objects
                fabricCanvas.clear();

                // Dispose canvas
                fabricCanvas.dispose();
                fabricCanvas = null;

                // Reset state
                canvasState.isInitialized = false;
                canvasState.isReady = false;
                canvasState.history = [];
                canvasState.historyStep = -1;

                console.log('[DISPOSE] ✓ Canvas disposed successfully');
                return { success: true, message: 'Canvas disposed successfully' };
            } catch (error) {
                console.error('[ERROR] Error disposing canvas:', error);
                console.error('[ERROR] Error details:', {
                    name: error.name,
                    message: error.message,
                    stack: error.stack
                });
                return { success: false, error: error.message };
            }
        } else {
            console.log('[DISPOSE] No canvas to dispose');
            return { success: true, message: 'No canvas to dispose' };
        }
    };

    // Auto-initialize when DOM is ready (only if canvas elements exist)
    function autoInitialize() {
        // Check if canvas elements exist before attempting initialization
        const canvasElement = document.getElementById('drawingCanvas');
        const canvasWrapper = document.getElementById('canvasWrapper');
        
        if (canvasElement && canvasWrapper) {
            console.log('[AUTO-INIT] Canvas elements found, initializing...');
            window.initializeFabricCanvas();
        } else {
            console.log('[AUTO-INIT] Canvas elements not found, skipping initialization (page may not require canvas)');
        }
    }
    
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', autoInitialize);
    } else {
        autoInitialize();
    }

})();
