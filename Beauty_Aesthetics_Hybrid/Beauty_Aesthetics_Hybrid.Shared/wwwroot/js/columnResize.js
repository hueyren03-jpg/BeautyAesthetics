window.initColumnResize = () => {
    const table = document.querySelector('.resizable-table');
    if (!table) return;

    const ths = table.querySelectorAll('th');

    ths.forEach((th, index) => {
        const handle = th.querySelector('.resize-handle');
        if (!handle) return;

        let startX, startWidth;

        handle.addEventListener('mousedown', e => {
            startX = e.pageX;
            startWidth = th.offsetWidth;

            document.addEventListener('mousemove', onMouseMove);
            document.addEventListener('mouseup', onMouseUp);
        });

        function onMouseMove(e) {
            const newWidth = startWidth + (e.pageX - startX);
            th.style.width = newWidth + 'px';

            table.querySelectorAll(`td:nth-child(${index + 1})`)
                .forEach(td => td.style.width = newWidth + 'px');
        }

        function onMouseUp() {
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
        }
    });
};