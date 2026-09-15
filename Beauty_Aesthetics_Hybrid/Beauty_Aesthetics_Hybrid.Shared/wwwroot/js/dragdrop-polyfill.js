(function () {
    let activeDragElement = null;
    let dragStarted = false;
    let startX = 0;
    let startY = 0;
    let currentDropZone = null;
    let dragDataTransfer = null;

    // Detect pointer down on draggable items
    document.addEventListener('pointerdown', function (e) {
        if (e.button !== 0) return; // Only drag on left click

        const draggable = e.target.closest('[draggable="true"]');
        if (!draggable) return;

        activeDragElement = draggable;
        startX = e.clientX;
        startY = e.clientY;
        dragStarted = false;
        currentDropZone = null;
        dragDataTransfer = new DataTransfer();

        // Enable touch-action: none to prevent scrolling during drag
        if (draggable.style.touchAction !== 'none') {
            draggable.style.touchAction = 'none';
        }
    }, { passive: true });

    // Track movement
    document.addEventListener('pointermove', function (e) {
        if (!activeDragElement) return;

        const dx = e.clientX - startX;
        const dy = e.clientY - startY;

        if (!dragStarted) {
            // Drag threshold of 8 pixels
            if (Math.abs(dx) > 8 || Math.abs(dy) > 8) {
                dragStarted = true;
                
                const dragStartEvent = new DragEvent('dragstart', {
                    bubbles: true,
                    cancelable: true,
                    clientX: e.clientX,
                    clientY: e.clientY,
                    screenX: e.screenX,
                    screenY: e.screenY,
                    dataTransfer: dragDataTransfer
                });
                activeDragElement.dispatchEvent(dragStartEvent);
            }
        }

        if (dragStarted) {
            // Temporarily disable pointer events on the dragged element so we can hit-test underneath it
            const originalPointerEvents = activeDragElement.style.pointerEvents;
            activeDragElement.style.pointerEvents = 'none';
            const elementUnderPointer = document.elementFromPoint(e.clientX, e.clientY);
            activeDragElement.style.pointerEvents = originalPointerEvents;

            const dropZone = elementUnderPointer;

            if (dropZone !== currentDropZone) {
                if (currentDropZone) {
                    const dragLeaveEvent = new DragEvent('dragleave', {
                        bubbles: true,
                        cancelable: true,
                        clientX: e.clientX,
                        clientY: e.clientY,
                        screenX: e.screenX,
                        screenY: e.screenY,
                        dataTransfer: dragDataTransfer
                    });
                    currentDropZone.dispatchEvent(dragLeaveEvent);
                }

                if (dropZone) {
                    const dragEnterEvent = new DragEvent('dragenter', {
                        bubbles: true,
                        cancelable: true,
                        clientX: e.clientX,
                        clientY: e.clientY,
                        screenX: e.screenX,
                        screenY: e.screenY,
                        dataTransfer: dragDataTransfer
                    });
                    dropZone.dispatchEvent(dragEnterEvent);
                }

                currentDropZone = dropZone;
            }

            if (currentDropZone) {
                const dragOverEvent = new DragEvent('dragover', {
                    bubbles: true,
                    cancelable: true,
                    clientX: e.clientX,
                    clientY: e.clientY,
                    screenX: e.screenX,
                    screenY: e.screenY,
                    dataTransfer: dragDataTransfer
                });
                currentDropZone.dispatchEvent(dragOverEvent);
            }
        }
    });

    // Handle drag drop and dragend
    document.addEventListener('pointerup', function (e) {
        if (!activeDragElement) return;

        if (dragStarted) {
            const originalPointerEvents = activeDragElement.style.pointerEvents;
            activeDragElement.style.pointerEvents = 'none';
            const elementUnderPointer = document.elementFromPoint(e.clientX, e.clientY);
            activeDragElement.style.pointerEvents = originalPointerEvents;

            const dropZone = elementUnderPointer;

            if (dropZone) {
                const dropEvent = new DragEvent('drop', {
                    bubbles: true,
                    cancelable: true,
                    clientX: e.clientX,
                    clientY: e.clientY,
                    screenX: e.screenX,
                    screenY: e.screenY,
                    dataTransfer: dragDataTransfer
                });
                dropZone.dispatchEvent(dropEvent);
            }

            const dragEndEvent = new DragEvent('dragend', {
                bubbles: true,
                cancelable: true,
                clientX: e.clientX,
                clientY: e.clientY,
                screenX: e.screenX,
                screenY: e.screenY,
                dataTransfer: dragDataTransfer
            });
            activeDragElement.dispatchEvent(dragEndEvent);
        }

        activeDragElement = null;
        dragStarted = false;
        currentDropZone = null;
        dragDataTransfer = null;
    });

    // Handle cancel (e.g. escaping, losing focus)
    document.addEventListener('pointercancel', function (e) {
        if (!activeDragElement) return;

        if (dragStarted) {
            const dragEndEvent = new DragEvent('dragend', {
                bubbles: true,
                cancelable: true,
                clientX: e.clientX,
                clientY: e.clientY,
                screenX: e.screenX,
                screenY: e.screenY,
                dataTransfer: dragDataTransfer
            });
            activeDragElement.dispatchEvent(dragEndEvent);
        }

        activeDragElement = null;
        dragStarted = false;
        currentDropZone = null;
        dragDataTransfer = null;
    });

    // Prevent native drag events from firing to avoid interference from buggy WebView engine
    document.addEventListener('dragstart', function (e) {
        if (e.isTrusted) {
            e.preventDefault();
        }
    }, { capture: true, passive: false });

    // Prevent text selection while dragging
    document.addEventListener('selectstart', function (e) {
        if (activeDragElement && dragStarted) {
            e.preventDefault();
        }
    }, { capture: true, passive: false });
})();
