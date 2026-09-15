/**
 * Case Notes Page Interop Module
 * 
 * Purpose:
 * - Handles JavaScript-to-Blazor communication via custom events
 * - Provides proper event listener setup and cleanup
 * - Avoids eval() for better security and performance
 * 
 * Events:
 * - quillSaveShortcut: Triggered by Ctrl/Cmd+S keyboard shortcut
 * - toggleEmojiPicker: Triggered by emoji toolbar button
 * - closeEmojiPicker: Triggered by ESC key or clicking outside picker
 */

let dotNetRef = null;
let saveShortcutListener = null;
let toggleEmojiListener = null;
let closeEmojiListener = null;
let contentChangedListener = null;

/**
 * Setup event listeners for Blazor interop
 * @param {Object} dotNetReference - DotNetObjectReference from Blazor
 */
export function setupEventListeners(dotNetReference) {
    // Validate input
    if (!dotNetReference) {
        console.error('[INTEROP] Invalid dotNetReference provided');
        return;
    }

    // Clean up existing listeners first
    if (dotNetRef) {
        console.warn('[INTEROP] Listeners already exist, cleaning up first');
        removeEventListeners();
    }

    dotNetRef = dotNetReference;
    
    // Save shortcut listener (Ctrl+S / Cmd+S)
    saveShortcutListener = function(event) {
        if (event.type === 'quillSaveShortcut' && dotNetRef) {
            dotNetRef.invokeMethodAsync('TriggerSave')
                .catch(err => console.error('[INTEROP] Error triggering save:', err));
        }
    };
    
    // Toggle emoji picker listener
    toggleEmojiListener = function(event) {
        if (event.type === 'toggleEmojiPicker' && dotNetRef) {
            dotNetRef.invokeMethodAsync('ToggleEmojiPickerFromJS')
                .catch(err => console.error('[INTEROP] Error toggling emoji picker:', err));
        }
    };
    
    // Close emoji picker listener
    closeEmojiListener = function(event) {
        if (event.type === 'closeEmojiPicker' && dotNetRef) {
            dotNetRef.invokeMethodAsync('CloseEmojiPickerFromJS')
                .catch(err => console.error('[INTEROP] Error closing emoji picker:', err));
        }
    };
    
    // Content changed listener
    contentChangedListener = function(event) {
        if (event.type === 'editorContentChanged' && dotNetRef) {
            dotNetRef.invokeMethodAsync('OnEditorContentChanged')
                .catch(err => console.error('[INTEROP] Error notifying content change:', err));
        }
    };
    
    // Add event listeners
    document.addEventListener('quillSaveShortcut', saveShortcutListener);
    document.addEventListener('toggleEmojiPicker', toggleEmojiListener);
    document.addEventListener('closeEmojiPicker', closeEmojiListener);
    document.addEventListener('editorContentChanged', contentChangedListener);
    
    console.log('[INTEROP] Event listeners setup complete');
}

/**
 * Remove all event listeners and cleanup references
 * Should be called when the component is disposed
 */
export function removeEventListeners() {
    let cleanedUp = false;

    if (saveShortcutListener) {
        document.removeEventListener('quillSaveShortcut', saveShortcutListener);
        saveShortcutListener = null;
        cleanedUp = true;
    }
    if (toggleEmojiListener) {
        document.removeEventListener('toggleEmojiPicker', toggleEmojiListener);
        toggleEmojiListener = null;
        cleanedUp = true;
    }
    if (closeEmojiListener) {
        document.removeEventListener('closeEmojiPicker', closeEmojiListener);
        closeEmojiListener = null;
        cleanedUp = true;
    }
    if (contentChangedListener) {
        document.removeEventListener('editorContentChanged', contentChangedListener);
        contentChangedListener = null;
        cleanedUp = true;
    }
    
    if (dotNetRef) {
        dotNetRef = null;
        cleanedUp = true;
    }
    
    if (cleanedUp) {
        console.log('[INTEROP] Event listeners removed and references cleaned up');
    } else {
        console.log('[INTEROP] No listeners to remove');
    }
}

/**
 * Change the font size of selected text or set default font size
 * @param {string} size - Font size in px (e.g., '16px', '20px')
 * @returns {boolean} - True if successful, false otherwise
 */
export function changeFontSize(size) {
    if (!window.quillEditor) {
        console.error('[INTEROP] Quill editor not initialized');
        return false;
    }

    try {
        const selection = window.quillEditor.getSelection();
        
        if (selection && selection.length > 0) {
            // Apply to selected text
            window.quillEditor.formatText(selection.index, selection.length, 'size', size);
            console.log(`[INTEROP] Applied font size ${size} to selected text`);
        } else {
            // Set as default for next typing
            window.quillEditor.format('size', size);
            console.log(`[INTEROP] Set default font size to ${size}`);
        }
        
        return true;
    } catch (error) {
        console.error('[INTEROP] Error changing font size:', error);
        return false;
    }
}

/**
 * Get available font sizes
 * @returns {Array<string>} - Array of available font sizes
 */
export function getAvailableFontSizes() {
    return ['10px', '12px', '14px', '16px', '18px', '20px', '24px', '28px', '32px', '36px'];
}


