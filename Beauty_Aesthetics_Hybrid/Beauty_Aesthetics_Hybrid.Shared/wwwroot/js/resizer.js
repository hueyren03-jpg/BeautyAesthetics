(function() {
    function initializeResizer() {
        // Try to find elements in prescription builder page first
        let resizer = document.querySelector('.prescription-builder-page .panel-resizer');
        let leftPanel = document.querySelector('.prescription-builder-page .drug-list-panel');
        let rightPanel = document.querySelector('.prescription-builder-page .selected-drugs-panel');
        let container = document.querySelector('.prescription-builder-page .prescription-content');
        
        // If not found, try prescription page
        if (!resizer || !leftPanel || !rightPanel || !container) {
            resizer = document.querySelector('.panel-resizer');
            leftPanel = document.querySelector('.drug-list-panel');
            rightPanel = document.querySelector('.selected-drugs-panel');
            container = document.querySelector('.prescription-content');
        }

        if (!resizer || !leftPanel || !rightPanel || !container) {
            // Retry after a short delay if elements not found
            setTimeout(initializeResizer, 100);
            return;
        }

        let isResizing = false;

        resizer.addEventListener('mousedown', function(e) {
            e.preventDefault();
            isResizing = true;
            document.body.style.cursor = 'col-resize';
            document.body.style.userSelect = 'none';
        });

        document.addEventListener('mousemove', function(e) {
            if (!isResizing) return;

            const containerRect = container.getBoundingClientRect();
            const containerWidth = containerRect.width;
            const mouseX = e.clientX - containerRect.left;

            // Calculate percentage
            let leftPercentage = (mouseX / containerWidth) * 100;

            // Constrain between 30% and 70%
            leftPercentage = Math.max(30, Math.min(70, leftPercentage));

            leftPanel.style.width = leftPercentage + '%';
            rightPanel.style.flex = '1';
        });

        document.addEventListener('mouseup', function() {
            if (isResizing) {
                isResizing = false;
                document.body.style.cursor = '';
                document.body.style.userSelect = '';
            }
        });
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeResizer);
    } else {
        initializeResizer();
    }
})();
