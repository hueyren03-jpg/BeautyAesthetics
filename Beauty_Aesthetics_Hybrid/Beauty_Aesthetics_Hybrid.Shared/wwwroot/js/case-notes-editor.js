
(function () {
    'use strict';

    // Expose quillEditor globally for Blazor to check initialization state
    window.quillEditor = null;
    let quillEditor = null;
    
    // Track image event listeners for proper cleanup
    const imageEventListeners = new Map();
    
    // Emoji list - organized by categories
    // To add more emojis, simply add them to this array
    const emojiList = [
        '😀', '😃', '😄', '😁', '😅', '😂', '🤣', '😊', '😇', '🙂', '🙃', '😉', '😌', '😍', '🥰', '😘',
        '😗', '😙', '😚', '😋', '😛', '😝', '😜', '🤪', '🤨', '🧐', '🤓', '😎', '🤩', '🥳', '🥸', '😏',
        '😒', '😞', '😔', '😟', '😕', '🙁', '☹️', '😣', '😖', '😫', '😩', '🥺', '😢', '😭', '😤', '😠',
        '😡', '🤬', '🤯', '😳', '🥵', '🥶', '😱', '😨', '😰', '😥', '😓', '🤗', '🤔', '🤭', '🤫', '🤥',
        '😶', '😐', '😑', '😬', '🙄', '😯', '😦', '😧', '😮', '😲', '🥱', '😴', '🤤', '😪', '😵', '🤐',
        '🥴', '🤢', '🤮', '🤧', '😷', '🤒', '🤕', '🤑', '🤠', '👍', '👎', '👌', '✌️', '🤞', '🤝', '👏',
        '🙌', '👐', '🤲', '🙏', '✍️', '💪', '🦾', '🦿', '🦵', '🦶', '👂', '🦻', '👃', '🧠', '🫀', '🫁',
        '❤️', '🧡', '💛', '💚', '💙', '💜', '🖤', '🤍', '🤎', '💔', '❤️‍🔥', '❤️‍🩹', '❣️', '💕', '💞', '💓',
        '💗', '💖', '💘', '💝', '💟', '☮️', '✝️', '☪️', '🕉️', '☸️', '✡️', '🔯', '🕎', '☯️', '☦️', '🛐',
        '⭐', '🌟', '✨', '⚡', '🔥', '💥', '☄️', '🌈', '☀️', '🌤️', '⛅', '🌥️', '☁️', '🌦️', '🌧️', '⛈️',
        '🌩️', '🌨️', '❄️', '☃️', '⛄', '🌬️', '💨', '💧', '💦', '☔', '☂️', '🌊', '🌫️'
    ];

    // Initialize Quill Editor
    window.initializeQuillEditor = function () {
        console.log('[QUILL] initializeQuillEditor called');
        
        const editorContainer = document.getElementById('editor');

        if (!editorContainer) {
            console.error('[QUILL] ✗ Editor container not found');
            return false;
        }

        console.log('[QUILL] ✓ Editor container found');

        // Check if Quill is loaded
        if (typeof Quill === 'undefined') {
            console.error('[QUILL] ✗ Quill library not loaded');
            return false;
        }

        console.log('[QUILL] ✓ Quill library loaded');

        try {
            // Destroy existing editor if any
            if (quillEditor || window.quillEditor) {
                console.log('[QUILL] Destroying existing editor instance');
                if (quillEditor && typeof quillEditor.off === 'function') {
                    try {
                        quillEditor.off('text-change');
                    } catch (e) {
                        console.warn('[QUILL] Could not remove listeners:', e);
                    }
                }
                quillEditor = null;
                window.quillEditor = null;
            }

            // Clear the editor container to ensure clean slate
            console.log('[QUILL] Clearing editor container...');
            editorContainer.innerHTML = '';

            // Small delay to ensure DOM is cleared
            setTimeout(() => {
                console.log('[QUILL] Creating new Quill instance...');
            }, 0);

            // Register custom font sizes
            const Size = Quill.import('attributors/style/size');
            Size.whitelist = ['10px', '12px', '14px', '16px', '18px', '20px', '24px', '28px', '32px', '36px'];
            Quill.register(Size, true);

            // Initialize Quill with custom toolbar
            quillEditor = new Quill('#editor', {
                theme: 'snow',
                placeholder: 'Write your case notes here...',
                modules: {
                    toolbar: {
                        container: [
                            ['bold', 'italic', 'underline'],
                            [{ 'size': ['10px', '12px', '14px', '16px', '18px', '20px', '24px', '28px', '32px', '36px'] }],
                            [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                            [{ 'header': [1, 2, 3, false] }],
                            ['image'],
                            ['emoji']
                        ],
                        handlers: {
                            // Ensure headings / Normal are not affected by previous explicit size formats
                            header: function(value) {
                                const quill = this.quill;
                                const range = quill.getSelection();

                                if (!range) {
                                    return;
                                }

                                const length = range.length === 0 ? 1 : range.length;

                                if (value) {
                                    // Applying Heading 1/2/3: clear explicit size then set header
                                    quill.formatText(range.index, length, 'size', false);
                                    quill.formatLine(range.index, length, 'header', value);
                                } else {
                                    // Selecting Normal: remove header AND explicit size to return to default text
                                    quill.formatLine(range.index, length, 'header', false);
                                    quill.formatText(range.index, length, 'size', false);
                                }
                            },
                            emoji: function() {
                                window.toggleEmojiPicker();
                            },
                            image: function() {
                                selectLocalImage();
                            }
                        }
                    },
                    history: {
                        delay: 1000,
                        maxStack: 50,
                        userOnly: true
                    }
                }
            });

            // Replace Quill SVG icons with Bootstrap icons for better appearance
            // All icons set to 18px for compact design
            const iconReplacements = [
                { selector: '.ql-bold', icon: '<i class="bi bi-type-bold" style="font-size: 18px !important; line-height: 1 !important; display: inline-flex; align-items: center; justify-content: center;"></i>' },
                { selector: '.ql-italic', icon: '<i class="bi bi-type-italic" style="font-size: 18px !important; line-height: 1 !important; display: inline-flex; align-items: center; justify-content: center;"></i>' },
                { selector: '.ql-underline', icon: '<i class="bi bi-type-underline" style="font-size: 18px !important; line-height: 1 !important; display: inline-flex; align-items: center; justify-content: center;"></i>' },
                { selector: '.ql-list[value="ordered"]', icon: '<i class="bi bi-list-ol" style="font-size: 18px !important; line-height: 1 !important; display: inline-flex; align-items: center; justify-content: center;"></i>' },
                { selector: '.ql-list[value="bullet"]', icon: '<i class="bi bi-list-ul" style="font-size: 18px !important; line-height: 1 !important; display: inline-flex; align-items: center; justify-content: center;"></i>' },
                { selector: '.ql-image', icon: '<i class="bi bi-image" style="font-size: 18px !important; line-height: 1 !important; display: inline-flex; align-items: center; justify-content: center;"></i>' }
            ];

            iconReplacements.forEach(({ selector, icon }) => {
                const button = document.querySelector(selector);
                if (button) {
                    button.innerHTML = icon;
                    console.log(`[QUILL] ✓ Replaced ${selector} with Bootstrap icon at 18px`);
                }
            });

            // Style the custom emoji button - keep same size for consistency
            const emojiButton = document.querySelector('.ql-emoji');
            if (emojiButton) {
                emojiButton.innerHTML = '<span style="font-size: 20px; line-height: 1; display: inline-flex; align-items: center; justify-content: center;">😊</span>';
            }

            // Apply custom toolbar styling - ALL CUSTOMIZATION IN ONE PLACE
            customizeToolbar();
            
            // Re-apply toolbar styles after Quill fully initializes (multiple attempts to ensure it sticks)
            setTimeout(() => {
                customizeToolbar();
            }, 50);
            
            setTimeout(() => {
                customizeToolbar();
            }, 200);
            
            // Add delete image button to toolbar (initially hidden)
            addDeleteImageButtonToToolbar();

            // Enable image dragging and resizing
            enableImageDragging();

            // Sync global reference
            window.quillEditor = quillEditor;

            // Listen for content changes to track unsaved changes
            quillEditor.on('text-change', function(delta, oldDelta, source) {
                if (source === 'user') {
                    // Notify Blazor that content has changed
                    const event = new CustomEvent('editorContentChanged');
                    document.dispatchEvent(event);
                }
            });

            // Verify toolbar was created
            const toolbar = document.querySelector('.ql-toolbar');
            const container = document.querySelector('.ql-container');
            
            if (toolbar && container) {
                console.log('[QUILL] ✓ Editor initialized successfully');
                console.log('[QUILL] ✓ Toolbar found:', toolbar !== null);
                console.log('[QUILL] ✓ Container found:', container !== null);
                return true;
            } else {
                console.error('[QUILL] ✗ Toolbar or container missing after initialization');
                console.error('[QUILL]   Toolbar:', toolbar);
                console.error('[QUILL]   Container:', container);
                return false;
            }
        } catch (error) {
            console.error('[QUILL] ✗ Error initializing editor:', error);
            console.error('[QUILL]   Error stack:', error.stack);
            return false;
        }
    };

  
    function customizeToolbar() {
        if (!quillEditor) return;

        const toolbar = document.querySelector('.ql-toolbar');
        if (!toolbar) return;

        // Prevent repeated style application which caused visible shaking
        if (toolbar.dataset.customized === 'true') {
            return;
        }
        toolbar.dataset.customized = 'true';

        // ========== TOOLBAR CONTAINER CONFIGURATION ==========
        Object.assign(toolbar.style, {
            minHeight: '52px',              // Toolbar height
            padding: '8px 12px',            // Slightly softer padding
            background: '#ffffff',          // White background to match page cards
            position: 'sticky',             // Keep toolbar visible when scrolling
            top: '0',                       // Stick to top
            zIndex: '100',                  // Layer above content
            flexShrink: '0',                // Don't shrink when space limited
            alignItems: 'center',           // Ensure all items are vertically centered
            display: 'flex',                // Flex layout
            gap: '6px',                     // Gap between groups
            boxShadow: '0 6px 18px rgba(15, 23, 42, 0.06)', // Soft shadow
            border: '1px solid #e2e8f0',    // Subtle border
            overflowX: 'auto',              // Enable horizontal scrolling
            whiteSpace: 'nowrap',           // Prevent wrapping
            maxWidth: '100%',               // Ensure it doesn't overflow parent
            scrollbarWidth: 'none',         // Hide scrollbar (Firefox)
            msOverflowStyle: 'none',        // Hide scrollbar (IE/Edge)
            webkitOverflowScrolling: 'touch' // Smooth scrolling on iOS
        });
        
        // ========== TOOLBAR FORMATS (BUTTON GROUPS) CONFIGURATION ==========
        const formats = toolbar.querySelectorAll('.ql-formats');
        formats.forEach(format => {
            Object.assign(format.style, {
                display: 'inline-flex',
                alignItems: 'center',
                gap: '4px',
                marginRight: '4px'
            });
        });

        // ========== TOOLBAR BUTTONS CONFIGURATION ==========
        const buttons = toolbar.querySelectorAll('button:not(.ql-emoji):not(.ql-delete-image)');
        buttons.forEach(button => {
            Object.assign(button.style, {
                width: '36px',                  // Slightly more compact
                height: '36px',                 // Match picker height
                padding: '0',                   // No padding for better icon centering
                borderRadius: '8px',            // Softer, less pill, closer to card/tab style
                border: '1px solid #e2e8f0',    // Soft border
                backgroundColor: '#ffffff',     // White button background
                transition: 'none',             // Remove animation for snappier rendering
                display: 'flex',                // Flexbox for centering
                alignItems: 'center',           // Vertical center
                justifyContent: 'center',       // Horizontal center
                verticalAlign: 'middle',        // Additional alignment
                lineHeight: '1',                // Prevent line-height issues
                margin: '0',                    // No margins
                boxShadow: '0 1px 3px rgba(15, 23, 42, 0.06)' // Subtle depth
            });

            // ========== ICON SIZE CONFIGURATION ==========
            const svg = button.querySelector('svg');
            if (svg) {
                Object.assign(svg.style, {
                    width: '18px',              // Slightly smaller, fits pill buttons better
                    height: '18px',
                    display: 'block',
                    margin: 'auto'
                });

                // Make icon stroke lines bolder for better visibility
                const strokes = svg.querySelectorAll('.ql-stroke');
                strokes.forEach(stroke => {
                    stroke.style.strokeWidth = '2.5';  // Slightly bolder stroke
                });
            }
        });

        // ========== DROPDOWN PICKER CONFIGURATION (e.g., "Normal" selector) ==========
        // Fix the picker container alignment - MUST match button height exactly
        const pickers = toolbar.querySelectorAll('.ql-picker');
        pickers.forEach(picker => {
            Object.assign(picker.style, {
                display: 'inline-flex',     // Use flexbox
                alignItems: 'center',       // Vertically center
                verticalAlign: 'middle',    // Force vertical alignment
                height: '36px',             // Match button height
                minHeight: '36px',
                lineHeight: '1',            // Prevent line-height issues
                margin: '0',                // No margins
                width: 'auto',              // Let width adjust to content
                minWidth: 'auto',           // No minimum width constraint
                maxWidth: 'none'            // No maximum width constraint
            });
        });

        installPickerDropdownOverlay(toolbar);

        // Fix the picker label (the visible dropdown button)
        const pickerLabels = toolbar.querySelectorAll('.ql-picker-label');
        pickerLabels.forEach(label => {
            // Hide Quill's default SVG arrow
            const arrowSvg = label.querySelector('svg');
            if (arrowSvg) {
                arrowSvg.style.display = 'none';
            }
            
            // Add Bootstrap icon if not already present
            if (!label.querySelector('.ql-bootstrap-arrow')) {
                const arrowIcon = document.createElement('i');
                arrowIcon.className = 'bi bi-caret-down-fill ql-bootstrap-arrow';
                arrowIcon.style.cssText = 'font-size: 12px; margin-left: 4px; flex-shrink: 0; display: inline-block; line-height: 1; color: #64748b;';
                label.appendChild(arrowIcon);
            }
            
            Object.assign(label.style, {
                height: '36px',                 // Match buttons
                minHeight: '36px',
                maxHeight: '36px',
                padding: '0 12px 0 10px',       // Slightly softer padding
                fontSize: '13px',
                fontWeight: '500',
                borderRadius: '6px',            // Medium rounded corners
                border: '1px solid #e2e8f0',    // Soft border
                backgroundColor: '#ffffff',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'flex-start',
                lineHeight: '1',
                verticalAlign: 'middle',
                margin: '0',
                boxSizing: 'border-box',
                width: 'auto',
                minWidth: 'auto',
                maxWidth: 'none',
                whiteSpace: 'nowrap',
                gap: '4px',
                boxShadow: '0 1px 3px rgba(15, 23, 42, 0.06)'
            });
        });

        // ========== FONT SIZE PICKER CUSTOMIZATION ==========
        // Customize the size picker to show actual px values
        const sizePicker = toolbar.querySelector('.ql-size');
        if (sizePicker) {
            const sizeLabel = sizePicker.querySelector('.ql-picker-label');
            if (sizeLabel) {
                // Set default label
                sizeLabel.setAttribute('data-label', '16px');
            }

            // Customize dropdown options to show px values using data-label
            const sizeOptions = sizePicker.querySelectorAll('.ql-picker-item');
            sizeOptions.forEach(option => {
                const value = option.getAttribute('data-value');
                if (value) {
                    // Use data-label attribute instead of textContent to avoid duplication
                    option.setAttribute('data-label', value);
                    // Clear any existing text content
                    option.textContent = '';
                }
            });
        }

        // ========== EMOJI BUTTON CONFIGURATION ==========
        // Emoji button needs exact same height as other buttons
        const emojiBtn = toolbar.querySelector('.ql-emoji');
        if (emojiBtn) {
            Object.assign(emojiBtn.style, {
                width: '38px',              // Match button width exactly - increased
                height: '38px',             // Match button height exactly - increased
                padding: '0',               // No padding
                margin: '0',                // No margin
                fontSize: '24px',           // Emoji size - increased for better visibility
                display: 'inline-flex',     // Use flexbox for centering
                alignItems: 'center',       // Vertically center emoji
                justifyContent: 'center',   // Horizontally center emoji
                lineHeight: '1',            // Prevent line-height from affecting height
                verticalAlign: 'middle',    // Additional alignment
                borderRadius: '6px',        // Match button border radius
                boxSizing: 'border-box'     // Include padding in height
            });
        }
        
        // ========== DELETE IMAGE BUTTON CONFIGURATION ==========
        // Delete button should match other buttons exactly
        // IMPORTANT: Button should be hidden by default, only shown when image is selected
        const deleteBtn = toolbar.querySelector('.ql-delete-image');
        if (deleteBtn) {
            // ALWAYS hide button by default - it will be shown when image is selected
            Object.assign(deleteBtn.style, {
                width: '38px',              // Match button width exactly
                height: '38px',             // Match button height exactly
                padding: '0',               // No padding
                margin: '0 0 0 8px',        // Left margin only for spacing
                display: 'none',            // Always hidden by default
                visibility: 'hidden',        // Additional hiding mechanism
                alignItems: 'center',       // Vertical center
                justifyContent: 'center',   // Horizontal center
                lineHeight: '1',            // Prevent line-height issues
                verticalAlign: 'middle',    // Additional alignment
                borderRadius: '6px',        // Match button border radius
                boxSizing: 'border-box'     // Include padding in height
            });
            
            // Style the icon inside delete button to match other icons
            const deleteIcon = deleteBtn.querySelector('i');
            if (deleteIcon) {
                Object.assign(deleteIcon.style, {
                    fontSize: '18px',           // Match other toolbar icons (18px)
                    lineHeight: '1',            // Prevent line-height issues
                    display: 'inline-flex',     // Inline flex for centering
                    alignItems: 'center',       // Vertical center
                    justifyContent: 'center'    // Horizontal center
                });
            }
        }

        // Re-apply styles after a short delay to ensure they stick (Quill may override them)
        setTimeout(() => {
            // Re-apply picker label styles
            const pickerLabels = toolbar.querySelectorAll('.ql-picker-label');
            pickerLabels.forEach(label => {
                // Hide Quill's default SVG arrow
                const arrowSvg = label.querySelector('svg');
                if (arrowSvg) {
                    arrowSvg.style.setProperty('display', 'none', 'important');
                }
                
                // Add Bootstrap icon if not already present
                if (!label.querySelector('.ql-bootstrap-arrow')) {
                    const arrowIcon = document.createElement('i');
                    arrowIcon.className = 'bi bi-caret-down-fill ql-bootstrap-arrow';
                    arrowIcon.style.cssText = 'font-size: 12px !important; margin-left: 4px !important; flex-shrink: 0 !important; display: inline-block !important; line-height: 1 !important; color: #64748b !important;';
                    label.appendChild(arrowIcon);
                }
                
                label.style.setProperty('padding', '0 10px 0 6px', 'important'); // Right padding for Bootstrap icon
                label.style.setProperty('font-size', '13px', 'important');
                label.style.setProperty('margin', '0', 'important');
                label.style.setProperty('width', 'auto', 'important');
                label.style.setProperty('white-space', 'nowrap', 'important');
                label.style.setProperty('flex-shrink', '1', 'important');
                label.style.setProperty('flex-grow', '0', 'important');
                label.style.setProperty('gap', '4px', 'important');
                label.style.setProperty('justify-content', 'flex-start', 'important'); // Align content to start
            });
            
            // Re-apply picker container styles
            const pickers = toolbar.querySelectorAll('.ql-picker');
            pickers.forEach(picker => {
                picker.style.setProperty('margin', '0', 'important');
                picker.style.setProperty('width', 'auto', 'important');
                picker.style.setProperty('min-width', 'auto', 'important');
                picker.style.setProperty('max-width', 'none', 'important');
                picker.style.setProperty('flex-shrink', '1', 'important');
                picker.style.setProperty('flex-grow', '0', 'important');
            });
            
            // Re-apply formats margin
            const formats = toolbar.querySelectorAll('.ql-formats');
            formats.forEach(format => {
                format.style.setProperty('margin-right', '4px', 'important');
            });
            
            // Re-apply toolbar gap
            toolbar.style.setProperty('gap', '4px', 'important');
            
            // Ensure delete button is hidden if no image is selected
            const deleteBtn = toolbar.querySelector('.ql-delete-image');
            if (deleteBtn) {
                // Only show if there's a selected image
                const hasSelectedImage = selectedImage !== null;
                if (!hasSelectedImage) {
                    deleteBtn.style.setProperty('display', 'none', 'important');
                    deleteBtn.style.setProperty('visibility', 'hidden', 'important');
                } else {
                    deleteBtn.style.setProperty('display', 'flex', 'important');
                    deleteBtn.style.setProperty('visibility', 'visible', 'important');
                }
            }
        }, 100);
        
        console.log('[QUILL] Toolbar customized - all styles applied via JavaScript');
    }

    function installPickerDropdownOverlay(toolbar) {
        if (!toolbar || toolbar.dataset.pickerOverlayInstalled === 'true') return;

        const editor = document.querySelector('#editor');
        if (!editor) return;

        toolbar.dataset.pickerOverlayInstalled = 'true';
        editor.classList.add('case-note-editor-overlay-host');

        const pickerState = new WeakMap();
        let activePicker = null;

        const restorePicker = (picker) => {
            if (!picker) return;

            const state = pickerState.get(picker);
            if (!state) return;

            const { options, parent, nextSibling, originalStyle, addedClasses } = state;
            options.classList.remove('case-note-floating-picker');
            addedClasses.forEach(className => options.classList.remove(className));
            options.style.cssText = originalStyle;

            if (nextSibling && nextSibling.parentNode === parent) {
                parent.insertBefore(options, nextSibling);
            } else {
                parent.appendChild(options);
            }

            pickerState.delete(picker);

            if (activePicker === picker) {
                activePicker = null;
            }
        };

        const showPickerOverlay = (picker) => {
            const label = picker.querySelector('.ql-picker-label');
            const options = pickerState.get(picker)?.options || picker.querySelector('.ql-picker-options');

            if (!label || !options) return;

            if (activePicker && activePicker !== picker) {
                restorePicker(activePicker);
            }

            if (!pickerState.has(picker)) {
                const addedClasses = Array.from(picker.classList)
                    .filter(className => className !== 'ql-picker' && className !== 'ql-expanded');

                pickerState.set(picker, {
                    options,
                    parent: options.parentNode,
                    nextSibling: options.nextSibling,
                    originalStyle: options.getAttribute('style') || '',
                    addedClasses
                });

                addedClasses.forEach(className => options.classList.add(className));
                options.classList.add('case-note-floating-picker');
                document.body.appendChild(options);
            }

            const labelRect = label.getBoundingClientRect();
            const viewportWidth = window.innerWidth || document.documentElement.clientWidth || 0;
            const desiredWidth = Math.max(labelRect.width, Math.min(options.scrollWidth || 112, viewportWidth - 16));
            const left = Math.min(
                Math.max(8, labelRect.left),
                Math.max(8, viewportWidth - desiredWidth - 8)
            );

            Object.assign(options.style, {
                position: 'fixed',
                top: `${labelRect.bottom + 4}px`,
                left: `${left}px`,
                minWidth: `${desiredWidth}px`,
                maxWidth: `${Math.max(96, viewportWidth - 16)}px`,
                maxHeight: '220px',
                display: 'block',
                visibility: 'visible',
                opacity: '1',
                overflowY: 'auto',
                zIndex: '2147483647',
                pointerEvents: 'auto'
            });

            activePicker = picker;
        };

        const updateOverlay = () => {
            const expandedPicker = toolbar.querySelector('.ql-picker.ql-expanded');

            if (expandedPicker) {
                showPickerOverlay(expandedPicker);
                return;
            }

            if (activePicker) {
                restorePicker(activePicker);
            }
        };

        const scheduleOverlayUpdate = () => {
            window.setTimeout(updateOverlay, 0);
            window.setTimeout(updateOverlay, 60);
        };

        toolbar.addEventListener('click', scheduleOverlayUpdate, true);
        toolbar.addEventListener('touchend', scheduleOverlayUpdate, { capture: true, passive: true });

        document.addEventListener('click', (event) => {
            if (!activePicker) return;

            const activeOptions = pickerState.get(activePicker)?.options;
            if (toolbar.contains(event.target) || activeOptions?.contains(event.target)) {
                scheduleOverlayUpdate();
                return;
            }

            window.setTimeout(() => restorePicker(activePicker), 0);
        }, true);

        window.addEventListener('resize', () => restorePicker(activePicker));

        toolbar.querySelectorAll('.ql-picker').forEach(picker => {
            new MutationObserver(scheduleOverlayUpdate).observe(picker, {
                attributes: true,
                attributeFilter: ['class']
            });
        });
    }
    
    // Add delete image button to toolbar
    function addDeleteImageButtonToToolbar() {
        if (!quillEditor) return;
        
        const toolbar = document.querySelector('.ql-toolbar');
        if (!toolbar) return;
        
        // Remove existing delete button if any
        const existingBtn = toolbar.querySelector('.ql-delete-image');
        if (existingBtn) {
            existingBtn.remove();
        }
        
        // Create delete image button
        const deleteImageBtn = document.createElement('button');
        deleteImageBtn.className = 'ql-delete-image';
        deleteImageBtn.type = 'button';
        deleteImageBtn.innerHTML = '<i class="bi bi-trash3"></i>';
        deleteImageBtn.title = 'Delete selected image';
        deleteImageBtn.setAttribute('aria-label', 'Delete selected image');
        
        // Style the button - MUST MATCH OTHER TOOLBAR BUTTONS
        Object.assign(deleteImageBtn.style, {
            width: '38px',              // Match button width exactly
            height: '38px',             // Match button height exactly
            padding: '0',               // No padding
            margin: '0 0 0 8px',        // Left margin only for spacing
            borderRadius: '6px',
            border: 'none',
            background: 'transparent',
            cursor: 'pointer',
            display: 'none',            // Hidden by default
            alignItems: 'center',
            justifyContent: 'center',
            transition: 'all 0.2s ease',
            lineHeight: '1',            // Prevent line-height issues
            verticalAlign: 'middle',    // Additional alignment
            boxSizing: 'border-box'     // Include padding in height
        });
        
        // Style the icon inside delete button to match other icons
        const deleteIcon = deleteImageBtn.querySelector('i');
        if (deleteIcon) {
            Object.assign(deleteIcon.style, {
                fontSize: '18px',           // Match other toolbar icons
                lineHeight: '1',            // Prevent line-height issues
                display: 'inline-flex',     // Inline flex for centering
                alignItems: 'center',
                justifyContent: 'center'
            });
        }
        
        // Delete functionality
        deleteImageBtn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            
            if (!selectedImage) return;
            
            try {
                // Use the same delete logic as the overlay button
                const allImages = quillEditor.root.querySelectorAll('img');
                const targetIndex = Array.from(allImages).indexOf(selectedImage);
                
                if (targetIndex === -1) {
                    throw new Error('Image not found');
                }
                
                // Get Quill delta to find position
                const delta = quillEditor.getContents();
                let currentImageIndex = 0;
                let positionBeforeImage = 0;
                
                // Traverse delta to find the target image
                for (let i = 0; i < delta.ops.length; i++) {
                    const op = delta.ops[i];
                    
                    if (op.insert && typeof op.insert === 'object' && op.insert.image) {
                        if (currentImageIndex === targetIndex) {
                            // Found our target! Delete it
                            quillEditor.deleteText(positionBeforeImage, 1);
                            console.log('[QUILL] ✓ Image deleted successfully via toolbar button');
                            
                            // Clear selection
                            selectedImage = null;
                            deleteImageBtn.style.display = 'none';
                            deleteImageBtn.style.visibility = 'hidden';
                            
                            // Re-enable dragging for remaining images
                            setTimeout(() => {
                                const allImages = quillEditor.root.querySelectorAll('img');
                                allImages.forEach(img => {
                                    img.dataset.processed = 'false';
                                });
                                enableImageDragging();
                            }, 100);
                            return;
                        }
                        currentImageIndex++;
                        positionBeforeImage += 1;
                    } else if (typeof op.insert === 'string') {
                        positionBeforeImage += op.insert.length;
                    } else if (op.insert && typeof op.insert === 'object') {
                        positionBeforeImage += 1;
                    }
                }
                
                // If not found in delta, use DOM fallback
                throw new Error('Image not found in delta');
            } catch (error) {
                console.warn('[QUILL] Using DOM fallback for deletion:', error.message);
                try {
                    // Remove from DOM
                    const img = selectedImage;
                    selectedImage = null;
                    deleteImageBtn.style.display = 'none';
                    deleteImageBtn.style.visibility = 'hidden';
                    
                    img.remove();
                    
                    // Sync Quill state
                    const html = quillEditor.root.innerHTML;
                    quillEditor.root.innerHTML = html;
                    
                    // Re-enable dragging
                    setTimeout(() => {
                        const allImages = quillEditor.root.querySelectorAll('img');
                        allImages.forEach(img => {
                            img.dataset.processed = 'false';
                        });
                        enableImageDragging();
                    }, 100);
                    
                    console.log('[QUILL] ✓ Image removed via DOM fallback');
                } catch (e) {
                    console.error('[QUILL] ✗ Error in fallback deletion:', e);
                }
            }
        });
        
        // Add button to toolbar (after emoji button)
        const emojiBtn = toolbar.querySelector('.ql-emoji');
        if (emojiBtn) {
            emojiBtn.parentNode.insertBefore(deleteImageBtn, emojiBtn.nextSibling);
        } else {
            toolbar.appendChild(deleteImageBtn);
        }
        
        // Store reference globally for show/hide functionality
        window.deleteImageToolbarBtn = deleteImageBtn;
        
        console.log('[QUILL] ✓ Delete image button added to toolbar');
    }

    // Custom image handler - Select local image file
    function selectLocalImage() {
        const input = document.createElement('input');
        input.setAttribute('type', 'file');
        input.setAttribute('accept', 'image/*');
        input.click();

        input.onchange = () => {
            const file = input.files[0];
            if (file) {
                // Validate file type
                const validTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/webp'];
                if (!validTypes.includes(file.type)) {
                    alert('Please select a valid image file (JPEG, PNG, GIF, or WebP)');
                    return;
                }

                // Check file size (max 5MB)
                if (file.size > 5 * 1024 * 1024) {
                    const sizeMB = (file.size / (1024 * 1024)).toFixed(2);
                    alert(`Image size (${sizeMB}MB) exceeds the maximum allowed size of 5MB`);
                    return;
                }

                // Convert to base64 and insert
                const reader = new FileReader();
                reader.onload = (e) => {
                    try {
                        const range = quillEditor.getSelection(true);
                        quillEditor.insertEmbed(range.index, 'image', e.target.result);
                        quillEditor.setSelection(range.index + 1);
                        
                        // IMPORTANT: Trigger content changed event so Blazor detects unsaved changes
                        const event = new CustomEvent('editorContentChanged');
                        document.dispatchEvent(event);
                        console.log('[QUILL] Content changed event triggered after local image upload');
                        
                        // Make the newly inserted image draggable
                        // Use requestAnimationFrame for more reliable timing
                        requestAnimationFrame(() => {
                            requestAnimationFrame(() => {
                                enableImageDragging();
                            });
                        });
                        
                        console.log('[QUILL] Image inserted successfully');
                    } catch (error) {
                        console.error('[QUILL] Error inserting image:', error);
                        alert('Failed to insert image. Please try again.');
                    }
                };
                reader.onerror = (error) => {
                    console.error('[QUILL] Error reading image file:', error);
                    alert('Failed to read image file. Please try again.');
                };
                reader.readAsDataURL(file);
            }
        };
    }

    // Enable image dragging and resizing
    function enableImageDragging() {
        if (!quillEditor) return;

        const images = quillEditor.root.querySelectorAll('img');
        images.forEach(img => {
            // Clean up existing listeners and handlers if image was already processed
            if (img.dataset.processed === 'true') {
                // Remove existing class to force re-initialization
                img.classList.remove('resizable-image', 'has-resize-handles', 'show-handles', 'image-selected');
                // Clean up event listeners
                if (imageEventListeners.has(img)) {
                    const listeners = imageEventListeners.get(img);
                    listeners.forEach(({ target, event, handler }) => {
                        target.removeEventListener(event, handler);
                    });
                    imageEventListeners.delete(img);
                }
            }
            
            img.classList.add('resizable-image');
            img.style.maxWidth = '100%';
            img.style.height = 'auto';
            img.draggable = false; // Disable native dragging
            img.style.cursor = 'move';
            img.style.position = 'relative';
            
            // Always add handlers (they'll be cleaned up above if already exist)
            addResizeHandles(img);
            addImageSelection(img);
            
            // Make image interactive (resizable and draggable)
            makeImageInteractive(img);
            
            // Mark as processed
            img.dataset.processed = 'true';
        });
    }

    // Add resize handles as pseudo-elements via data attributes
    function addResizeHandles(img) {
        // Add a wrapper span that Quill can handle
        const parent = img.parentElement;
        
        // Create corner indicators using CSS classes
        img.classList.add('has-resize-handles');
        
        // Show handles on hover
        img.addEventListener('mouseenter', () => {
            img.classList.add('show-handles');
        });
        
        img.addEventListener('mouseleave', (e) => {
            // Only hide if not resizing or dragging
            if (!img.dataset.resizing && !img.dataset.dragging) {
                img.classList.remove('show-handles');
            }
        });
        
        // Update cursor based on mouse position - increased corner size for easier grabbing
        img.addEventListener('mousemove', (e) => {
            if (img.dataset.resizing || img.dataset.dragging) return;
            
            const rect = img.getBoundingClientRect();
            const cornerSize = 30; // Increased from 20 to 30 for easier corner grabbing
            
            const isNearTopLeft = e.clientX < rect.left + cornerSize && e.clientY < rect.top + cornerSize;
            const isNearTopRight = e.clientX > rect.right - cornerSize && e.clientY < rect.top + cornerSize;
            const isNearBottomLeft = e.clientX < rect.left + cornerSize && e.clientY > rect.bottom - cornerSize;
            const isNearBottomRight = e.clientX > rect.right - cornerSize && e.clientY > rect.bottom - cornerSize;
            
            // Set appropriate cursor
            if (isNearTopLeft || isNearBottomRight) {
                img.style.cursor = 'nwse-resize';
            } else if (isNearTopRight || isNearBottomLeft) {
                img.style.cursor = 'nesw-resize';
            } else {
                img.style.cursor = 'move';
            }
        });
        
        console.log('[QUILL] Added resize handles to image');
    }
    
    // Track currently selected image
    let selectedImage = null;
    
    // Add click selection functionality for images
    function addImageSelection(img) {
        // Click handler to select/deselect image
        const clickHandler = (e) => {
            // Don't select if user is resizing or dragging
            if (img.dataset.resizing === 'true' || img.dataset.dragging === 'true') {
                return;
            }
            
            // Check if clicking on toolbar delete button - don't select in this case
            if (e.target && e.target.closest('.ql-delete-image')) {
                return;
            }
            
            // Stop propagation to prevent deselect handler from firing
            e.stopPropagation();
            e.stopImmediatePropagation();
            
            // Deselect previous image
            if (selectedImage && selectedImage !== img) {
                selectedImage.classList.remove('image-selected');
                selectedImage.classList.remove('show-handles');
            }
            
            // Toggle selection
            if (selectedImage === img) {
                // Deselect current image
                img.classList.remove('image-selected');
                img.classList.remove('show-handles');
                selectedImage = null;
                
                // Hide toolbar delete button
                if (window.deleteImageToolbarBtn) {
                    window.deleteImageToolbarBtn.style.display = 'none';
                    window.deleteImageToolbarBtn.style.visibility = 'hidden';
                }
            } else {
                // Select this image
                img.classList.add('image-selected');
                img.classList.add('show-handles');
                selectedImage = img;
                
                // Show toolbar delete button
                if (window.deleteImageToolbarBtn) {
                    window.deleteImageToolbarBtn.style.display = 'flex';
                    window.deleteImageToolbarBtn.style.visibility = 'visible';
                }
            }
        };
        
        img.addEventListener('click', clickHandler, true);
        
        // Store listener for cleanup
        if (!imageEventListeners.has(img)) {
            imageEventListeners.set(img, []);
        }
        imageEventListeners.get(img).push(
            { target: img, event: 'click', handler: clickHandler }
        );
    }
    
    // Deselect image when clicking outside
    function deselectImageOnOutsideClick(e) {
        if (!selectedImage) return;
        
        // Check if click is on the image itself or toolbar delete button
        const isClickOnImage = selectedImage.contains(e.target);
        const isClickOnToolbarDeleteBtn = window.deleteImageToolbarBtn && window.deleteImageToolbarBtn.contains(e.target);
        
        // Don't deselect if clicking on the image or toolbar delete button
        if (isClickOnImage || isClickOnToolbarDeleteBtn) {
            return;
        }
        
        // Deselect the image
        selectedImage.classList.remove('image-selected');
        selectedImage.classList.remove('show-handles');
        selectedImage = null;
        
        // Hide toolbar delete button
        if (window.deleteImageToolbarBtn) {
            window.deleteImageToolbarBtn.style.display = 'none';
            window.deleteImageToolbarBtn.style.visibility = 'hidden';
        }
    }
    
    // Add global click listener to deselect when clicking outside (only once)
    if (!window.imageDeselectHandlerAdded) {
        document.addEventListener('click', deselectImageOnOutsideClick);
        window.imageDeselectHandlerAdded = true;
    }

    // Add delete button overlay for easy image deletion
    function addDeleteButton(img) {
        // Remove existing delete button if any
        const existingBtn = img.parentElement?.querySelector('.image-delete-btn');
        if (existingBtn) {
            existingBtn.remove();
        }
        
        // Create delete button
        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'image-delete-btn';
        deleteBtn.innerHTML = '<i class="bi bi-x-lg"></i>';
        deleteBtn.title = 'Delete image';
        deleteBtn.setAttribute('aria-label', 'Delete image');
        
        // Style the button
        deleteBtn.style.cssText = `
            position: absolute;
            top: -10px;
            right: -10px;
            width: 28px;
            height: 28px;
            background: linear-gradient(135deg, #ef4444 0%, #dc2626 100%);
            color: white;
            border: 2px solid white;
            border-radius: 50%;
            cursor: pointer;
            display: none;
            align-items: center;
            justify-content: center;
            z-index: 1001;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
            transition: all 0.2s ease;
            padding: 0;
            font-size: 14px;
        `;
        
        // Delete button visibility is controlled by image selection (click-based, not hover)
        // Button will be shown/hidden via addImageSelection function
        deleteBtn.style.display = 'none';
        deleteBtn.style.opacity = '0';
        
        // Delete functionality
        deleteBtn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            
            try {
                // Simplified approach: Get all images and find the target
                const allImages = quillEditor.root.querySelectorAll('img');
                const targetIndex = Array.from(allImages).indexOf(img);
                
                if (targetIndex === -1) {
                    throw new Error('Image not found');
                }
                
                // Get Quill delta to find position
                const delta = quillEditor.getContents();
                let currentImageIndex = 0;
                let positionBeforeImage = 0;
                
                // Traverse delta to find the target image
                for (let i = 0; i < delta.ops.length; i++) {
                    const op = delta.ops[i];
                    
                    if (op.insert && typeof op.insert === 'object' && op.insert.image) {
                        if (currentImageIndex === targetIndex) {
                            // Found our target! Delete it
                            quillEditor.deleteText(positionBeforeImage, 1);
                            console.log('[QUILL] ✓ Image deleted successfully');
                            return;
                        }
                        currentImageIndex++;
                        positionBeforeImage += 1;
                    } else if (typeof op.insert === 'string') {
                        positionBeforeImage += op.insert.length;
                    } else if (op.insert && typeof op.insert === 'object') {
                        positionBeforeImage += 1;
                    }
                }
                
                // If not found in delta, use DOM fallback
                throw new Error('Image not found in delta');
            } catch (error) {
                console.warn('[QUILL] Using DOM fallback for deletion:', error.message);
                // Fallback: Remove directly from DOM and update Quill
                try {
                    img.remove();
                    deleteBtn.remove();
                    
                    // Sync Quill state by getting and setting HTML
                    const html = quillEditor.root.innerHTML;
                    quillEditor.root.innerHTML = html;
                    
                    console.log('[QUILL] ✓ Image removed via DOM fallback');
                } catch (e) {
                    console.error('[QUILL] ✗ Error in fallback deletion:', e);
                }
            }
        });
        
        // Add button to parent container - improved positioning
        let parent = img.parentElement;
        
        // Ensure parent is a paragraph or editor container
        if (!parent || (parent.tagName !== 'P' && !parent.classList.contains('ql-editor'))) {
            // Wrap image in a paragraph if needed
            const wrapper = document.createElement('p');
            wrapper.style.position = 'relative';
            wrapper.style.display = 'block';
            wrapper.style.margin = '10px 0';
            if (img.parentNode) {
                img.parentNode.insertBefore(wrapper, img);
            }
            wrapper.appendChild(img);
            parent = wrapper;
        }
        
        // Make parent relative for absolute positioning of delete button
        if (getComputedStyle(parent).position === 'static') {
            parent.style.position = 'relative';
        }
        
        // Append delete button to parent
        parent.appendChild(deleteBtn);
        
        // Store reference to delete button on image for easy access
        img.dataset.deleteButtonId = 'btn-' + Math.random().toString(36).substr(2, 9);
        deleteBtn.id = img.dataset.deleteButtonId;
        
        console.log('[QUILL] ✓ Delete button added to image');
    }

    // Make image resizable and draggable
    function makeImageInteractive(img) {
        let isResizing = false;
        let isDragging = false;
        let startX, startY, startWidth, startHeight;
        let aspectRatio;
        let resizeCorner = null;
        let dragOffsetX, dragOffsetY;
        let mouseDownX = 0;
        let mouseDownY = 0;
        let isSelectionClick = false;
        
        // Helper to find Quill index from mouse coordinates
        const getQuillIndexFromPoint = (x, y) => {
            if (!quillEditor) return null;
            
            // Use standard API to find DOM node and offset
            let range;
            if (document.caretRangeFromPoint) {
                range = document.caretRangeFromPoint(x, y);
            } else if (document.caretPositionFromPoint) {
                const pos = document.caretPositionFromPoint(x, y);
                if (pos) {
                    range = document.createRange();
                    range.setStart(pos.offsetNode, pos.offset);
                    range.collapse(true);
                }
            }
            
            if (!range) return null;
            
            // Convert DOM node to Quill Blot
            const blot = Quill.find(range.startContainer, true);
            if (!blot) return null;
            
            // Get index in Quill
            // We need to handle text nodes vs container nodes
            let index = blot.offset(quillEditor.scroll);
            
            // If it's a text node, add the offset within the text
            if (range.startContainer.nodeType === Node.TEXT_NODE) {
                // Find the leaf blot for this text node
                const leaf = Quill.find(range.startContainer);
                if (leaf) {
                    index = leaf.offset(quillEditor.scroll) + range.startOffset;
                }
            }
            
            return index;
        };

        const mouseDownHandler = (e) => {
            const rect = img.getBoundingClientRect();
            const cornerSize = 30; // Detection area size
            
            // Detect which corner was clicked
            const isNearTopLeft = e.clientX < rect.left + cornerSize && e.clientY < rect.top + cornerSize;
            const isNearTopRight = e.clientX > rect.right - cornerSize && e.clientY < rect.top + cornerSize;
            const isNearBottomLeft = e.clientX < rect.left + cornerSize && e.clientY > rect.bottom - cornerSize;
            const isNearBottomRight = e.clientX > rect.right - cornerSize && e.clientY > rect.bottom - cornerSize;
            
            if (isNearTopLeft || isNearTopRight || isNearBottomLeft || isNearBottomRight) {
                // Start resizing
                isResizing = true;
                img.dataset.resizing = 'true';
                
                if (isNearTopLeft) resizeCorner = 'nw';
                else if (isNearTopRight) resizeCorner = 'ne';
                else if (isNearBottomLeft) resizeCorner = 'sw';
                else if (isNearBottomRight) resizeCorner = 'se';
                
                startX = e.clientX;
                startY = e.clientY;
                startWidth = img.offsetWidth;
                startHeight = img.offsetHeight;
                aspectRatio = startWidth / startHeight;

                e.preventDefault();
                e.stopPropagation();

                console.log(`[QUILL] Started resizing from ${resizeCorner} corner`);
            } else {
                // Track mouse down for click vs drag detection
                mouseDownX = e.clientX;
                mouseDownY = e.clientY;
                isSelectionClick = true;
                // Don't prevent default yet
            }
        };

        const mouseMoveHandler = (e) => {
            // Check if user is dragging (moved more than 5px)
            if (isSelectionClick && !isResizing) {
                const moveDistance = Math.sqrt(
                    Math.pow(e.clientX - mouseDownX, 2) + 
                    Math.pow(e.clientY - mouseDownY, 2)
                );
                
                if (moveDistance > 5) {
                    // User is dragging, not just clicking
                    isSelectionClick = false;
                    isDragging = true;
                    img.dataset.dragging = 'true';
                    
                    // Add dragging visual feedback
                    img.style.opacity = '0.5';
                    img.style.cursor = 'grabbing';
                    
                    console.log('[QUILL] Started dragging image');
                }
            }
            
            if (isResizing && resizeCorner) {
                e.preventDefault();
                
                let deltaX = e.clientX - startX;
                let deltaY = e.clientY - startY;
                let newWidth = startWidth;
                let newHeight = startHeight;

                // Calculate new dimensions based on corner
                switch(resizeCorner) {
                    case 'se': // Bottom-right
                        newWidth = startWidth + deltaX;
                        break;
                    case 'sw': // Bottom-left
                        newWidth = startWidth - deltaX;
                        break;
                    case 'ne': // Top-right
                        newWidth = startWidth + deltaX;
                        break;
                    case 'nw': // Top-left
                        newWidth = startWidth - deltaX;
                        break;
                }

                // Maintain aspect ratio
                newHeight = newWidth / aspectRatio;

                // Apply constraints
                const minWidth = 50;
                const maxWidth = quillEditor.root.offsetWidth;

                if (newWidth >= minWidth && newWidth <= maxWidth) {
                    img.style.width = newWidth + 'px';
                    img.style.height = newHeight + 'px';
                }
            } else if (isDragging) {
                e.preventDefault();
                
                // Show drop indicator
                const index = getQuillIndexFromPoint(e.clientX, e.clientY);
                
                if (index !== null) {
                    // Get bounds of the character at this index to draw cursor
                    const bounds = quillEditor.getBounds(index);
                    if (bounds) {
                        // Create or update cursor indicator
                        let indicator = document.getElementById('quill-drag-indicator');
                        if (!indicator) {
                            indicator = document.createElement('div');
                            indicator.id = 'quill-drag-indicator';
                            indicator.style.position = 'absolute';
                            indicator.style.width = '2px';
                            indicator.style.backgroundColor = '#0d6efd'; // Bootstrap primary blue
                            indicator.style.pointerEvents = 'none';
                            indicator.style.zIndex = '9999';
                            // Add blinking animation
                            indicator.style.animation = 'blink 1s infinite';
                            
                            // Add keyframes if needed
                            if (!document.getElementById('quill-drag-anim')) {
                                const style = document.createElement('style');
                                style.id = 'quill-drag-anim';
                                style.textContent = '@keyframes blink { 0% { opacity: 1; } 50% { opacity: 0; } 100% { opacity: 1; } }';
                                document.head.appendChild(style);
                            }
                            
                            quillEditor.container.appendChild(indicator);
                        }
                        
                        // Position indicator relative to editor container
                        indicator.style.left = bounds.left + 'px';
                        indicator.style.top = bounds.top + 'px';
                        indicator.style.height = bounds.height + 'px';
                        indicator.style.display = 'block';
                    }
                }
            }
        };

        const mouseUpHandler = (e) => {
            // Reset selection click flag
            isSelectionClick = false;
            
            // Remove drop indicator
            const indicator = document.getElementById('quill-drag-indicator');
            if (indicator) {
                indicator.remove();
            }
            
            if (isResizing) {
                isResizing = false;
                resizeCorner = null;
                delete img.dataset.resizing;
                img.classList.remove('show-handles');
                console.log('[QUILL] Resize complete');
            } else if (isDragging) {
                try {
                    isDragging = false;
                    delete img.dataset.dragging;
                    
                    // Reset visual feedback
                    img.style.opacity = '1';
                    img.style.cursor = 'move';
                    
                    // Perform the move
                    const newIndex = getQuillIndexFromPoint(e.clientX, e.clientY);
                    
                    if (newIndex !== null) {
                        // Get current image index
                        const blot = Quill.find(img);
                        if (blot) {
                            const oldIndex = quillEditor.getIndex(blot);
                            
                            // Don't move if dropped on itself (roughly)
                            if (newIndex === oldIndex || newIndex === oldIndex + 1) {
                                console.log('[QUILL] Dropped at same location');
                                return;
                            }
                            
                            // Get image attributes to preserve them
                            const src = img.getAttribute('src');
                            const width = img.style.width;
                            const height = img.style.height;
                            
                            console.log(`[QUILL] Moving image from ${oldIndex} to ${newIndex}`);
                            
                            let insertIndex = newIndex;
                            let deleteIndex = oldIndex;
                            
                            if (insertIndex > deleteIndex) {
                                // Moving down
                                quillEditor.insertEmbed(insertIndex, 'image', src, 'user');
                                quillEditor.deleteText(deleteIndex, 1, 'user');
                            } else {
                                // Moving up
                                quillEditor.insertEmbed(insertIndex, 'image', src, 'user');
                                quillEditor.deleteText(deleteIndex + 1, 1, 'user');
                            }
                            
                            // Restore selection at new position
                            quillEditor.setSelection(insertIndex + 1);
                            
                            // Re-apply styles to the new image instance
                            setTimeout(() => {
                                // Find the image at the new location
                                // We can't easily get it by index immediately because of async rendering potentially
                                // But we can try to find the image that matches our src and is roughly at the right place
                                // Or just re-enable dragging for all images
                                const allImages = quillEditor.root.querySelectorAll('img');
                                allImages.forEach(image => {
                                    if (image.getAttribute('src') === src && !image.dataset.processed) {
                                        image.style.width = width;
                                        image.style.height = height;
                                    }
                                    // Reset processed flag to force re-binding events
                                    image.dataset.processed = 'false';
                                });
                                enableImageDragging();
                            }, 50);
                            
                            console.log('[QUILL] ✓ Image moved successfully');
                        }
                    }
                    
                    img.classList.remove('show-handles');
                } catch (error) {
                    console.error('[QUILL] Error moving image:', error);
                }
            }
        };

        // Add event listeners
        img.addEventListener('mousedown', mouseDownHandler);
        document.addEventListener('mousemove', mouseMoveHandler);
        document.addEventListener('mouseup', mouseUpHandler);

        // Store listeners for cleanup
        if (!imageEventListeners.has(img)) {
            imageEventListeners.set(img, []);
        }
        imageEventListeners.get(img).push(
            { target: img, event: 'mousedown', handler: mouseDownHandler },
            { target: document, event: 'mousemove', handler: mouseMoveHandler },
            { target: document, event: 'mouseup', handler: mouseUpHandler }
        );
    }

    // Get editor content as HTML
    window.getEditorContent = function () {
        if (!quillEditor) {
            console.warn('[QUILL] Editor not initialized');
            return '';
        }

        try {
            const html = quillEditor.root.innerHTML;
            // Return empty string if editor only contains default paragraph
            if (html === '<p><br></p>' || html.trim() === '') {
                return '';
            }
            return html;
        } catch (error) {
            console.error('[QUILL] Error getting content:', error);
            return '';
        }
    };

    // Check if editor has content (text or images)
    window.hasEditorContent = function () {
        if (!quillEditor) {
            return false;
        }

        try {
            // Check for text content
            const text = quillEditor.getText().trim();
            if (text.length > 0) {
                return true;
            }
            
            // Check for images
            const images = quillEditor.root.querySelectorAll('img');
            if (images.length > 0) {
                return true;
            }
            
            return false;
        } catch (error) {
            console.error('[QUILL] Error checking content:', error);
            return false;
        }
    };

    // Alias for hasEditorContent (used by Blazor)
    window.checkEditorHasContent = function () {
        return window.hasEditorContent();
    };

    // Clear editor content
    window.clearEditor = function () {
        if (!quillEditor) {
            console.warn('[QUILL] Editor not initialized');
            return false;
        }

        try {
            quillEditor.setText('');
            console.log('[QUILL] Editor cleared');
            return true;
        } catch (error) {
            console.error('[QUILL] Error clearing editor:', error);
            return false;
        }
    };

    // Insert emoji at cursor position
    window.insertEmoji = function (emoji) {
        if (!quillEditor) {
            console.warn('[QUILL] Editor not initialized');
            return false;
        }

        try {
            const range = quillEditor.getSelection();
            if (range) {
                quillEditor.insertText(range.index, emoji);
                quillEditor.setSelection(range.index + emoji.length);
            } else {
                const length = quillEditor.getLength();
                quillEditor.insertText(length - 1, emoji);
                quillEditor.setSelection(length + emoji.length - 1);
            }
            console.log('[QUILL] Emoji inserted:', emoji);
            return true;
        } catch (error) {
            console.error('[QUILL] Error inserting emoji:', error);
            return false;
        }
    };

    // Insert base64 image into editor
    window.insertImageFromBase64 = function (base64Data) {
        if (!quillEditor) {
            console.error('[QUILL] Editor not initialized');
            return false;
        }

        try {
            console.log('[QUILL] Inserting base64 image into editor');
            
            // Get current selection or insert at end
            const range = quillEditor.getSelection() || { index: quillEditor.getLength() };
            
            // Insert the image
            quillEditor.insertEmbed(range.index, 'image', base64Data);
            
            // Move cursor after the image
            quillEditor.setSelection(range.index + 1);
            
            // IMPORTANT: Trigger content changed event so Blazor detects unsaved changes
            const event = new CustomEvent('editorContentChanged');
            document.dispatchEvent(event);
            console.log('[QUILL] Content changed event triggered after image insertion');
            
            // Style and enable dragging for the newly inserted image
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    // Find the newly inserted image
                    const images = quillEditor.root.querySelectorAll('img');
                    const newImage = images[images.length - 1]; // Get the last image (newly inserted)
                    
                    if (newImage) {
                        // Load image to get its natural dimensions
                        const img = new Image();
                        img.onload = function() {
                            // Preserve the original size from annotation canvas
                            // The image is already exported at the correct size (max 1200px from fabric-annotate.js)
                            // So we just need to display it at its natural size
                            newImage.style.maxWidth = '100%'; // Allow full width in editor
                            newImage.style.width = 'auto'; // Use natural width
                            newImage.style.height = 'auto'; // Maintain aspect ratio
                            newImage.style.display = 'block';
                            newImage.style.margin = '10px 0';
                            
                            // Set the width to match the natural width, but don't exceed editor width
                            const editorWidth = quillEditor.root.offsetWidth;
                            if (img.naturalWidth > editorWidth) {
                                newImage.style.width = '100%';
                            } else {
                                newImage.style.width = img.naturalWidth + 'px';
                            }
                            
                            console.log(`[QUILL] Image styled with original size (${img.naturalWidth}x${img.naturalHeight})`);
                        };
                        img.src = base64Data;
                    }
                    
                    enableImageDragging();
                    console.log('[QUILL] Image inserted and dragging enabled');
                });
            });
            
            return true;
        } catch (error) {
            console.error('[QUILL] Error inserting image:', error);
            return false;
        }
    };

    // Get list of available emojis
    window.getEmojiList = function () {
        return emojiList;
    };

    // Toggle emoji picker (called from Blazor)
    window.toggleEmojiPicker = function() {
        const event = new CustomEvent('toggleEmojiPicker');
        document.dispatchEvent(event);
    };

    // Cleanup all image event listeners
    function cleanupImageListeners() {
        imageEventListeners.forEach((listeners, img) => {
            listeners.forEach(({ target, event, handler }) => {
                target.removeEventListener(event, handler);
            });
        });
        imageEventListeners.clear();
        console.log('[QUILL] Image event listeners cleaned up');
    }

    // Dispose editor
    window.disposeQuillEditor = function () {
        if (quillEditor) {
            cleanupImageListeners();
            quillEditor = null;
            window.quillEditor = null;
            console.log('[QUILL] Editor disposed');
        }
    };

    // Set editor content (for loading existing notes)
    window.setEditorContent = function (html) {
        if (!quillEditor) {
            console.warn('[QUILL] Editor not initialized');
            return false;
        }

        try {
            quillEditor.root.innerHTML = html || '';
            
            // Enable dragging for loaded images
            // Use requestAnimationFrame for more reliable timing
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    enableImageDragging();
                });
            });
            
            console.log('[QUILL] Editor content set');
            return true;
        } catch (error) {
            console.error('[QUILL] Error setting content:', error);
            return false;
        }
    };

    // Enable/Disable editor
    window.setEditorEnabled = function (enabled) {
        if (!quillEditor) {
            console.warn('[QUILL] Editor not initialized');
            return false;
        }

        try {
            quillEditor.enable(enabled);
            console.log('[QUILL] Editor', enabled ? 'enabled' : 'disabled');
            return true;
        } catch (error) {
            console.error('[QUILL] Error setting editor state:', error);
            return false;
        }
    };

    // Keyboard event handler
    const keyboardHandler = function(event) {
        // Ctrl+S or Cmd+S to save
        if ((event.ctrlKey || event.metaKey) && event.key === 's') {
            event.preventDefault();
            const saveEvent = new CustomEvent('quillSaveShortcut');
            document.dispatchEvent(saveEvent);
            console.log('[QUILL] Save shortcut triggered');
        }
        
        // ESC to close emoji picker
        if (event.key === 'Escape') {
            const closeEvent = new CustomEvent('closeEmojiPicker');
            document.dispatchEvent(closeEvent);
        }
    };

    // Beforeunload handler - warn user if they have unsaved content
    const beforeUnloadHandler = function(event) {
        try {
            if (quillEditor && typeof window.hasEditorContent === 'function' && window.hasEditorContent()) {
                event.preventDefault();
                event.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
                return event.returnValue;
            }
        } catch (error) {
            console.error('[QUILL] Error in beforeunload handler:', error);
        }
    };

    // Add event listeners
    document.addEventListener('keydown', keyboardHandler);
    window.addEventListener('beforeunload', beforeUnloadHandler);

    // Cleanup function
    window.removeEditorEventListeners = function() {
        document.removeEventListener('keydown', keyboardHandler);
        window.removeEventListener('beforeunload', beforeUnloadHandler);
        console.log('[QUILL] Event listeners removed');
    };

    // Fallback event listener setup (for browsers that don't support modules)
    window.setupCaseNotePageListeners = function(dotNetRef) {
        window.caseNotePageRef = dotNetRef;
        
        const saveHandler = function() {
            if (window.caseNotePageRef) {
                window.caseNotePageRef.invokeMethodAsync('TriggerSave')
                    .catch(err => console.error('Error:', err));
            }
        };
        
        const toggleHandler = function() {
            if (window.caseNotePageRef) {
                window.caseNotePageRef.invokeMethodAsync('ToggleEmojiPickerFromJS')
                    .catch(err => console.error('Error:', err));
            }
        };
        
        const closeHandler = function() {
            if (window.caseNotePageRef) {
                window.caseNotePageRef.invokeMethodAsync('CloseEmojiPickerFromJS')
                    .catch(err => console.error('Error:', err));
            }
        };
        
        const contentChangedHandler = function() {
            if (window.caseNotePageRef) {
                window.caseNotePageRef.invokeMethodAsync('OnEditorContentChanged')
                    .catch(err => console.error('Error:', err));
            }
        };
        
        document.addEventListener('quillSaveShortcut', saveHandler);
        document.addEventListener('toggleEmojiPicker', toggleHandler);
        document.addEventListener('closeEmojiPicker', closeHandler);
        document.addEventListener('editorContentChanged', contentChangedHandler);
        
        window.caseNotePageEventHandlers = {
            saveHandler: saveHandler,
            toggleHandler: toggleHandler,
            closeHandler: closeHandler,
            contentChangedHandler: contentChangedHandler
        };
        
        console.log('[QUILL] Fallback listeners setup with content change detection');
    };
    
    window.removeCaseNotePageListeners = function() {
        if (window.caseNotePageEventHandlers) {
            document.removeEventListener('quillSaveShortcut', window.caseNotePageEventHandlers.saveHandler);
            document.removeEventListener('toggleEmojiPicker', window.caseNotePageEventHandlers.toggleHandler);
            document.removeEventListener('closeEmojiPicker', window.caseNotePageEventHandlers.closeHandler);
            document.removeEventListener('editorContentChanged', window.caseNotePageEventHandlers.contentChangedHandler);
            window.caseNotePageEventHandlers = null;
        }
        window.caseNotePageRef = null;
        console.log('[QUILL] Fallback listeners removed');
    };

    // Helper function to check if editor is properly initialized
    window.isQuillEditorReady = function() {
        return window.quillEditor !== null && 
               document.querySelector('.ql-toolbar') !== null &&
               document.querySelector('.ql-container') !== null;
    };

    console.log('[QUILL] Case notes editor script loaded with keyboard shortcuts');
})();
